using System.Net;

namespace XDS.SDK.Messaging.BlockchainClient
{
    public interface IMessageRelayAddressReceiver
    {
        void ReceiveMessageRelayRecordAsync(IPAddress ipAddress, int port, PeerServices peerServices, string userAgent);
    }
}
