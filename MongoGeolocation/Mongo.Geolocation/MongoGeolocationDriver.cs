using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using MongoGeolocation.Entities;

namespace MongoGeolocation.Mongo.Geolocation
{
    public class MongoGeolocationDriver<TEntity> where TEntity : IGeoEntity
    {
        #region Variables
        private string CollectionName { get; }
        private IMongoDatabase MongoDatabase { get; }
        private MongoClient MongoClient { get; }
        private IMongoCollection<TEntity> MongoCollection { get; }
        #endregion
        
        #region Constructors
        public MongoGeolocationDriver(IConfiguration config)
        {
            var mongoConfig = config.GetSection("Mongo");
            var connectionString = mongoConfig["connectionString"];
            this.MongoClient = new MongoClient(connectionString);

            this.MongoDatabase = this.MongoClient.GetDatabase(mongoConfig["databaseName"]);
            this.CollectionName = mongoConfig["collectionName"];
            this.MongoCollection = this.GetCollection();
        }
        #endregion

        #region Methods
        public IMongoCollection<TEntity> GetCollection()
            => this.MongoDatabase.GetCollection<TEntity>(this.CollectionName);
        
        public async Task<IEnumerable<TEntity>> FindNearAsync(Expression<Func<TEntity, object>> field, double latitude, double longitude, double maxDistanceInMiles)
        {
            const double METERS_PER_MILE = 1609.34;

            var point = GeoJson.Point(GeoJson.Geographic(latitude, longitude));
            var filter = Builders<TEntity>.Filter.NearSphere(field, point, maxDistanceInMiles * METERS_PER_MILE);
            var cursor = await this.MongoCollection.FindAsync<TEntity>(filter);
            return await cursor.ToListAsync();
        }
        
        public async Task CreateGeospatialIndexAsync(Expression<Func<TEntity, object>> field)
        {
            var indexFound = false;
            var expr = field.Body as MemberExpression;
            var memberName = expr?.Member.Name;
            
            await this.MongoCollection.Indexes.ListAsync().Result.ForEachAsync(idx =>
            {
                var key = idx["key"].ToBsonDocument().Elements.First().Name;
                if (memberName != null && key.Equals(memberName, StringComparison.OrdinalIgnoreCase)) 
                    indexFound = true;
            });

            if (indexFound)
                return;
            
            var keys = Builders<TEntity>.IndexKeys.Geo2DSphere(field);
            await this.MongoCollection.Indexes.CreateOneAsync(new CreateIndexModel<TEntity>(keys));
        }
        
        public async Task UpdateGeolocationAsync(TEntity entity, PointF point)
        {
            entity.Point = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(new GeoJson2DGeographicCoordinates(point.X, point.Y));
            await this.MongoCollection.ReplaceOneAsync(h => h._id == entity._id, entity);
        }
        
        public async Task UpdateAllGeolocationsAsync(Func<TEntity, Task<PointF>> callbackGetCoordinates)
        {
            using var entities = await this.MongoCollection.FindAsync("{}");

            foreach (var entity in (await entities.ToListAsync()).Where(e => e.Point == null))
            {
                this.UpdateEntityGeolocation(entity, callbackGetCoordinates);
            }
        }
        
        private async void UpdateEntityGeolocation(TEntity entity, Func<TEntity, Task<PointF>> callbackGetCoordinates)
        {
            if (entity.Point != null)
                return;
            
            var point = await callbackGetCoordinates(entity);
            if (point.IsEmpty)
                return;
            
            await this.UpdateGeolocationAsync(entity, point);
        }
        #endregion
    }
}