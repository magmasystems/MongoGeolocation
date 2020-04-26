using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace MongoGeolocation.MappingService
{
    public abstract class MappingDriverBase : IMappingServiceDriver
    {
        protected string APIToken { get; }
        protected HttpClient HttpClient { get; }

        protected MappingDriverBase(IConfiguration config)
        {
            this.HttpClient = new HttpClient();

            var apiKey = config["MappingService:apiKey"];
            if (string.IsNullOrEmpty(apiKey))
                throw new ApplicationException("The apiKey is missing from the MappingService section of the config file");

            this.APIToken = apiKey;
        }

        public virtual Task<PointF> GetCoordinates(string address, string city, string state, string zip)
        {
            throw new NotImplementedException();
        }
    }
}