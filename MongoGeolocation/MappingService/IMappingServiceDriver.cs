using System.Drawing;
using System.Threading.Tasks;

namespace MongoGeolocation.MappingService
{
    public interface IMappingServiceDriver
    {
        Task<PointF> GetCoordinates(string address, string city, string state, string zip);
    }
}