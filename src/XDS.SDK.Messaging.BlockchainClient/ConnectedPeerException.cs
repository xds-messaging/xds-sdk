using System;

namespace XDS.SDK.Messaging.BlockchainClient
{
    /// <summary>
    ///     This exception indicates an expected exception inside the network peer, which the ConnectedPeer class handles
    ///     mostly by itself.
    /// </summary>
    public class ConnectedPeerException : Exception
    {
        public ConnectedPeerException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public bool NoConnectionAvailable;
    }
}