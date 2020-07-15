using XDS.SDK.Messaging.CrossTierTypes.FStore;

namespace XDS.SDK.Messaging.BlockchainClient
{
    public class ChatClientConfiguration : IChatClientConfiguration
    {
        public const int DefaultPort = 38333;

        public const uint NetworkMagic = 0x58445331u;

        public const uint ProtocolVersion = 70012u;

        public const PeerServices PeerServices = BlockchainClient.PeerServices.Network;
        public static byte[] NetworkMagicBytes = {0x31, 0x53, 0x44, 0x58};

        public static PeerServices OwnPeerServices = PeerServices.NetworkLimited | PeerServices.MessagingClient;

        public readonly byte[] SessionNonce = Tools.GetRandomNonce();
        public string[] SeedNodes;

        public string UserAgentName { get; set; }
    }

    public interface IChatClientConfiguration
    {
        string UserAgentName { get; set; }
    }
}