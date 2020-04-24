using System.Drawing;
using System.Threading.Tasks;

namespace MongoGeolocation
{
    public interface IMappingServiceDriver
    {
        Task<PointF> GetCoordinates(string address, string city, string state, string zip);
    }
}