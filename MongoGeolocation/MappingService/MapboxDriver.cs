using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace MongoGeolocation.MappingService
{
    public class MapboxDriver : IMappingServiceDriver
    {
        private string APIToken { get; }
        private HttpClient HttpClient { get; }
        
        /*
          GET
          https://api.mapbox.com/geocoding/v5/mapbox.places/374%20STOCKHOLM%20STREET%2C%20BROOKLYN%2C%20NY%2011237.json?types=address&access_token=pk.eyJ1IjoibWFnbWFzeXN0ZW1zIiwiYSI6ImNrOWN5Z2RuYzA5N2ozZHM0NzlmbHZhdTcifQ.sDsBbaO9vjIr4qGCO8_oXg&limit=1
         
          Response
                {
                "type": "FeatureCollection",
                "query": [
                    "374",
                    "stockholm",
                    "street",
                    "brooklyn",
                    "ny",
                    "11237"
                ],
                "features": [
                    {
                        "id": "address.2624371683002466",
                        "type": "Feature",
                        "place_type": [
                            "address"
                        ],
                        "relevance": 1,
                        "properties": {
                            "accuracy": "rooftop"
                        },
                        "text": "Stockholm Street",
                        "place_name": "374 Stockholm Street, Brooklyn, New York 11237, United States",
                        "center": [
                            -73.917578,
                            40.704559
                        ],
                        "geometry": {
                            "type": "Point",
                            "coordinates": [
                                -73.917578,
                                40.704559
                            ]
                        },
                        "address": "374",
                        "context": [
                            {
                                "id": "neighborhood.297402",
                                "text": "Bushwick"
                            },
                            {
                                "id": "locality.6335122455180360",
                                "wikidata": "Q18419",
                                "text": "Brooklyn"
                            },
                            {
                                "id": "postcode.11009487807941920",
                                "text": "11237"
                            },
                            {
                                "id": "place.15278078705964500",
                                "wikidata": "Q60",
                                "text": "New York"
                            },
                            {
                                "id": "region.10003493535855570",
                                "short_code": "US-NY",
                                "wikidata": "Q1384",
                                "text": "New York"
                            },
                            {
                                "id": "country.19352517729256050",
                                "short_code": "us",
                                "wikidata": "Q30",
                                "text": "United States"
                            }
                        ]
                    }
                ],
                "attribution": "NOTICE: © 2020 Mapbox and its suppliers. All rights reserved. Use of this data is subject to the Mapbox Terms of Service (https://www.mapbox.com/about/maps/). This response and the information it contains may not be retained. POI(s) provided by Foursquare."
            }
         */

        public MapboxDriver(IConfiguration config)
        {
            this.HttpClient = new HttpClient();

            var apiKey = config["MappingService:apiKey"];
            if (string.IsNullOrEmpty(apiKey))
                throw new ApplicationException("The apiKey is missing from the MappingService section of the config file");

            this.APIToken = apiKey;
        }

        public async Task<PointF> GetCoordinates(string address, string city, string state, string zip)
        {
            var mapboxResponse = await this.GetGeocoding(address, city, state, zip);
            if (mapboxResponse == null)
                return PointF.Empty;
            
            return new PointF
            {
                X = (float) mapboxResponse.Features[0].Geometry.Coordinates[0], 
                Y = (float) mapboxResponse.Features[0].Geometry.Coordinates[1]
            };
        }

        private async Task<MapboxGeolocationResponse> GetGeocoding(string address, string city, string state, string zip)
        {
            var url = this.CreateUrl(address, city, state, zip);

            var response = await this.HttpClient.GetAsync(new Uri(url));
            if (!response.IsSuccessStatusCode) 
                return null;

            var mapboxResponse = JsonConvert.DeserializeObject<MapboxGeolocationResponse>(await response.Content.ReadAsStringAsync());
            return mapboxResponse;
        }

        private string CreateUrl(string address, string city, string state, string zip)
        {
            var encodedAddress = HttpUtility.UrlEncode($"{address}, {city}, {state} {zip}");
            var url =
                $"https://api.mapbox.com/geocoding/v5/mapbox.places/{encodedAddress}.json?types=address&access_token={this.APIToken}&limit=1";
            return url;
        }
    }

    public class MapboxGeolocationResponse
    {
        [JsonProperty("type")] public string Type { get; set; }

        [JsonProperty("query")] public List<string> Query { get; set; }

        [JsonProperty("features")] public List<Feature> Features { get; set; }

        [JsonProperty("attribution")] public string Attribution { get; set; }


        public class Feature
        {
            [JsonProperty("id")] public string Id { get; set; }

            [JsonProperty("type")] public string Type { get; set; }

            [JsonProperty("place_type")] public List<string> PlaceType { get; set; }

            [JsonProperty("relevance")] public long Relevance { get; set; }

            [JsonProperty("properties")] public Properties Properties { get; set; }

            [JsonProperty("text")] public string Text { get; set; }

            [JsonProperty("place_name")] public string PlaceName { get; set; }

            [JsonProperty("center")] public List<double> Center { get; set; }

            [JsonProperty("geometry")] public Geometry Geometry { get; set; }

            [JsonProperty("address")] public string Address { get; set; }

            [JsonProperty("context")] public List<GeoContext> Context { get; set; }
        }

        public class GeoContext
        {
            [JsonProperty("id")] public string Id { get; set; }

            [JsonProperty("text")] public string Text { get; set; }

            [JsonProperty("wikidata", NullValueHandling = NullValueHandling.Ignore)]
            public string Wikidata { get; set; }

            [JsonProperty("short_code", NullValueHandling = NullValueHandling.Ignore)]
            public string ShortCode { get; set; }
        }

        public class Geometry
        {
            [JsonProperty("type")] public string Type { get; set; }

            [JsonProperty("coordinates")] public List<double> Coordinates { get; set; }
        }

        public class Properties
        {
            [JsonProperty("accuracy")] public string Accuracy { get; set; }
        }
    }
}