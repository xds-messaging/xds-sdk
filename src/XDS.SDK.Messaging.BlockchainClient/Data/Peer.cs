using System;
using System.Net;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.SDK.Messaging.BlockchainClient.Data
{
    public class Peer : IId
    {
        public IPAddress IPAddress { get; set; }
        public int ProtocolPort { get; set; }

        public PeerServices PeerServices { get; set; } // serialize
        public DateTime LastSeen { get; set; } // serialize

        /// <summary>
        ///     LastError can be:
        ///     - A date our program has set.
        ///     - DateTime.MaxValue: We explicitly cleared the last error.
        ///     - DateTime.MinValue (or epoch): A default value.
        /// </summary>
        public DateTime LastError { get; set; } // serialize

        public ulong BytesSent { get; set; } // serialize
        public ulong BytesReceived { get; set; } // serialize
        public int Priority { get; set; } // serialize

        public bool HasLastError
        {
            get { return this.LastError != DateTime.MaxValue; }
        }

        public string Id { get; set; }


        public override string ToString()
        {
            return $"{this.IPAddress}:{this.ProtocolPort}";
        }
    }
}