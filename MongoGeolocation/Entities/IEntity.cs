using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace MongoGeolocation.Entities
{
    public interface IEntity
    {
        [BsonId]
        public ObjectId _id { get; set; }
    }

    public interface IGeoEntity : IEntity
    {
        [BsonElement("Point")]
        public GeoJsonPoint<GeoJson2DGeographicCoordinates> Point { get; set; }
    }
}