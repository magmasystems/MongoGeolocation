using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace MongoGeolocation
{
    internal class Hospital
    {
        /*
           {
                "_id" : ObjectId("5ea1a13a35b0fafc65e2025c"),
                "Facility ID" : 44022,
                "Facility Name" : "CONWAY BEHAVIORAL HEALTH",
                "Address" : "2255 STURGIS ROAD",
                "City" : "CONWAY",
                "State" : "AR",
                "ZIP Code" : 72034,
                "Location" : ""
            }
         */
        
        [BsonId]
        public ObjectId _id { get; set; }
        
        [BsonElement("Facility ID")]
        public object FacilityId { get; set; }
        
        [BsonElement("Facility Name")]
        public string FacilityName { get; set; }
        
        [BsonElement("Address")]
        public string Address { get; set; }
        
        [BsonElement("City")]
        public string City { get; set; }
        
        [BsonElement("State")]
        public string State { get; set; }
        
        [BsonElement("ZIP Code")]
        public int ZipCode { get; set; }
        
        [BsonElement("Location")]
        public string Location { get; set; }
        
        [BsonElement("Point")]
        public GeoJsonPoint<GeoJson2DGeographicCoordinates> Point { get; set; }
    }
}