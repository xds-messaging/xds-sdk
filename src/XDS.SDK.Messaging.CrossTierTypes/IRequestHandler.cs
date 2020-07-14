using System.Threading.Tasks;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public interface IRequestHandler
    {
        Task<byte[]> ProcessRequestAsync(byte[] rawRequest, string clientInformation);
    }
}