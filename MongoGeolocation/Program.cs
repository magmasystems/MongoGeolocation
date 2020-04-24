using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoGeolocation.Entities;
using MongoGeolocation.MappingService;
using MongoGeolocation.Mongo.Geolocation;

namespace MongoGeolocation
{
    internal class Program
    {
        private static IConfiguration Configuration { get; set; }
        private IMappingServiceDriver MappingService { get; set; }
        private MongoGeolocationDriver<Hospital> MongoGeolocationDriver { get; }
        
        private IMongoCollection<Hospital> Hospitals { get; }
        
        static async Task Main(string[] args)
        {
            // https://pradeeploganathan.com/dotnet/configuration-in-a-net-core-console-application/
            Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();
            
            var app = new Program();

            if (args.Length > 0)
            {
                if (args[0] == "update")
                {
                    await app.UpdateAllHospitalsGeolocationAsync();
                }
            }
            else
            {
                await app.Run();
            }

            
            Console.WriteLine("Press ENTER to quit");
            Console.ReadLine();
        }

        private Program()
        {
            this.MongoGeolocationDriver = new MongoGeolocationDriver<Hospital>(Configuration);
            this.Hospitals = this.MongoGeolocationDriver.GetCollection();
            
            this.CreateMappingService();
        }
        
        private void CreateMappingService()
        {
            var mappingServiceType = Configuration["MappingService:type"];
            if (!string.IsNullOrEmpty(mappingServiceType))
            {
                var type = Type.GetType(mappingServiceType);
                if (type != null)
                {
                    this.MappingService = Activator.CreateInstance(type, Configuration) as IMappingServiceDriver;
                }
            }
            
            this.MappingService ??= new MapboxDriver(Configuration);
        }

        private async Task Run()
        {
            // Make sure that the Geospatial index exists on the hospital data
            await this.MongoGeolocationDriver.CreateGeospatialIndexAsync(h => h.Point);
            
            // For the radius search
            var miles = 3.0;
            var sMiles = Configuration["App:miles"];
            if (!string.IsNullOrEmpty(sMiles))
                miles = double.Parse(sMiles);
            
            // The downloaded hospital data has a column that has a nullable text field that can a string like this:
            // POINT (-73.908273, 40.982746)
            // We just want to take that and parse it into a lat/lng coordinate
            var locationRegex = new Regex(@"^POINT \((?<lat>[-+]*\d*\.\d*) (?<lng>[-+]*\d*\.\d*)\)$");
            
            using var hospitals = await this.Hospitals.FindAsync("{}");
            var list = await hospitals.ToListAsync();
            
            // Only consider all of the hospitals in Brooklyn
            foreach (var h in list.Where(h => h.ZipCode >= 11200 && h.ZipCode < 11300).OrderBy(h => h.ZipCode))
            {
                var lat = 0.0;
                var lng = 0.0;
                
                // Use the regular expression to get the coordinates from the "Location" field
                if (!string.IsNullOrEmpty(h.Location))
                {
                    var match = locationRegex.Match(h.Location);
                    if (match.Success)
                    {
                        lat = double.Parse(match.Groups["lat"].Value);
                        lng = double.Parse(match.Groups["lng"].Value);
                    }
                }
                
                // Print out the hospital info along with the parsed coordinates
                Console.WriteLine($"{h.FacilityName}, {h.Address}, {h.City}, {h.State}, {h.ZipCode}, {h.Location}, {lat}, {lng}");
                
                // Print out all of the hospitals that are within 'x' miles from this hospital
                try
                {
                    var hospitalsWithinRadius = await this.MongoGeolocationDriver.FindNearAsync(h2 => h2.Point, lat, lng, miles);
                    foreach (var h2 in hospitalsWithinRadius.Where(h2 => h2._id != h._id))
                    {
                        Console.WriteLine($"    Within {miles} miles: {h2.FacilityName}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return;
                }
            }
        }

        /// <summary>
        /// This gets called only if we start the application with the "update" argument.
        /// This will go through all of the hospitals in the Mongo collection and update their geocoordinates.
        /// </summary>
        private async Task UpdateAllHospitalsGeolocationAsync()
        {
            await this.MongoGeolocationDriver.UpdateAllGeolocationsAsync(async hospital =>
                await this.MappingService.GetCoordinates(hospital.Address, hospital.City, hospital.State, hospital.ZipCode.ToString())
            );
        }
    }
}