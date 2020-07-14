using System.Collections.Generic;
using System.Threading.Tasks;
using XDS.SDK.Cryptography.NoTLS;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public interface INetworkClient
    {
        Task<List<IRequestCommandData>> SendRequestAsync(byte[] request, Transport transport);
        Task<string> Receive(byte[] rawRequest, Transport transport);
    }
}
