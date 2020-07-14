using System;
using XDS.SDK.Messaging.BlockchainClient;

namespace XDS.SDK.Messaging.MessageHostClient
{
    /// <summary>
    ///     This exception indicates an expected exception inside the network peer, which the ConnectedPeer class handles
    ///     mostly by itself.
    /// </summary>
    public class MessageRelayConnectionException : ConnectedPeerException
    {
        public MessageRelayConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}