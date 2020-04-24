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
    class Program
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
                    await app.UpdateAllHospitalsGeolocation();
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
            await this.MongoGeolocationDriver.CreateGeospatialIndex(h => h.Point);
            
            var locationRegex = new Regex(@"^POINT \((?<lat>[-+]*\d*\.\d*) (?<lng>[-+]*\d*\.\d*)\)$");
            
            using var hospitals = await this.Hospitals.FindAsync("{}");
            var list = await hospitals.ToListAsync();
            foreach (var h in list.Where(h => h.ZipCode >= 11200 && h.ZipCode < 11300).OrderBy(h => h.ZipCode))
            {
                var lat = 0.0;
                var lng = 0.0;
                
                if (!string.IsNullOrEmpty(h.Location))
                {
                    var match = locationRegex.Match(h.Location);
                    if (match.Success)
                    {
                        lat = double.Parse(match.Groups["lat"].Value);
                        lng = double.Parse(match.Groups["lng"].Value);
                    }
                }
                
                Console.WriteLine($"{h.FacilityName}, {h.Address}, {h.City}, {h.State}, {h.ZipCode}, {h.Location}, {lat}, {lng}");

                var miles = 3.0;
                var sMiles = Configuration["App:miles"];
                if (!string.IsNullOrEmpty(sMiles))
                    miles = double.Parse(sMiles);
                
                try
                {
                    var hospitalsWithinRadius = await this.MongoGeolocationDriver.FindNear(h2 => h2.Point, lat, lng, miles);
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

        private async Task UpdateAllHospitalsGeolocation()
        {
            using var hospitals = await this.Hospitals.FindAsync("{}");
            var i = 0;
            
            foreach (var h in await hospitals.ToListAsync())
            { 
                i++;
                if (h.Point != null)
                    continue;
                
                this.UpdateHospitalGeolocation(h);
                Console.WriteLine($"Updating record {i}");
            }
        }
        
        private async void UpdateHospitalGeolocation(Hospital hospital)
        {
            if (hospital.Point != null)
                return;
            
            var point = await this.MappingService.GetCoordinates(hospital.Address, hospital.City, hospital.State, hospital.ZipCode.ToString());
            if (point.IsEmpty)
                return;
            
            await this.MongoGeolocationDriver.UpdateGeolocation(hospital, point);
        }
    }
}