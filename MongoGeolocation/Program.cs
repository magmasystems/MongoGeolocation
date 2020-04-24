using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;

namespace MongoGeolocation
{
    class Program
    {
        private static IConfiguration Configuration { get; set; }
        private IMappingServiceDriver MappingService { get; set; }
        private MongoClient MongoClient { get; }
        private IMongoDatabase MongoDatabase { get; }
        private string CollectionName { get; }
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
            var mongoConfig = Configuration.GetSection("Mongo");
            var connectionString = mongoConfig["connectionString"];
            this.MongoClient = new MongoClient(connectionString);

            this.MongoDatabase = this.MongoClient.GetDatabase(mongoConfig["databaseName"]);
            this.CollectionName = mongoConfig["collectionName"];
            this.Hospitals = this.MongoDatabase.GetCollection<Hospital>(this.CollectionName);
            
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
                    this.MappingService = Activator.CreateInstance(type) as IMappingServiceDriver;
                }
            }
            
            this.MappingService ??= new MapboxDriver();
        }

        private async Task Run()
        {
            await this.CreateGeospatialIndex(this.Hospitals, h => h.Point);
            
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

                const double miles = 3.0;
                try
                {
                    var hospitalsWithinRadius = await this.FindNear<Hospital>(this.CollectionName, h2 => h2.Point, lat, lng, miles);
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

            hospital.Point = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(new GeoJson2DGeographicCoordinates(point.X, point.Y));
            await this.Hospitals.ReplaceOneAsync(h => h._id == hospital._id, hospital);
        }

        private async Task CreateGeospatialIndex<TEntity>(IMongoCollection<TEntity> collection, Expression<Func<TEntity, object>> field)
        {
            var indexFound = false;
            var expr = field.Body as MemberExpression;
            var memberName = expr?.Member.Name;
            
            await collection.Indexes.ListAsync().Result.ForEachAsync(idx =>
            {
                var key = idx["key"].ToBsonDocument().Elements.First().Name;
                if (memberName != null && key.Equals(memberName, StringComparison.OrdinalIgnoreCase)) 
                    indexFound = true;
            });

            if (indexFound)
                return;
            
            var keys = Builders<TEntity>.IndexKeys.Geo2DSphere(field);
            await collection.Indexes.CreateOneAsync(new CreateIndexModel<TEntity>(keys));
        }

        private async Task<List<TEntity>> FindNear<TEntity>(string collectionName, Expression<Func<TEntity, object>> field, double latitude, double longitude, double maxDistanceInMiles)
        {
            var METERS_PER_MILE = 1609.34;

            var collection = this.MongoDatabase.GetCollection<TEntity>(collectionName);
                
            var point = GeoJson.Point(GeoJson.Geographic(latitude, longitude));
            var filter = Builders<TEntity>.Filter.NearSphere(field, point, maxDistanceInMiles * METERS_PER_MILE);
            var list = await collection.FindAsync<TEntity>(filter);
            return await list.ToListAsync();
        }
    }
}