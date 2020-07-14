using System;
using System.Net;

namespace XDS.SDK.Messaging.BlockchainClient
{
    public static class PayloadFactory
    {
        public static BitcoinVersionPayload CreateVersionPayload(string userAgent, byte[] nonce, IPEndPoint sender,
            IPEndPoint receiver)
        {
            var now = DateTimeOffset.UtcNow;
            var bitcoinVersionPayload = new BitcoinVersionPayload(
                new BitcoinVarString(userAgent),
                nonce,
                new BitcoinNetworkAddressPayload(now, ChatClientConfiguration.PeerServices, sender.Address, sender.Port),
                new BitcoinNetworkAddressPayload(now, PeerServices.Network, receiver.Address, receiver.Port));
            return bitcoinVersionPayload;
        }
    }
}