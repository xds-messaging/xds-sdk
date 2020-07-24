using System;
using System.Collections.Generic;
using System.Text;

namespace XDS.SDK.Messaging.CrossTierTypes.BlockchainIntegration
{
    [Flags]
    public enum XDSPeerServices : ulong
    {
        None = 0,

        /// <summary>
        ///     This node can be asked for full blocks instead of just headers.
        /// </summary>
        Network = 1,

        /// <summary>
        ///     BIP 0064. P2P protocol extension that performs UTXO lookups given a set of outpoints.
        /// </summary>
        GetUtxo = 2,

        /// <summary>
        ///     Bloom means the node is capable and willing to handle bloom-filtered connections.
        ///     BIP 0111. Bitcoin Core nodes used to support this by default, without advertising this bit,
        ///     but no longer do as of protocol version 70011 (= NO_BLOOM_VERSION)
        /// </summary>
        Bloom = 4,

        /// <summary>
        ///     BIP0144. Indicates that a node can be asked for blocks and transactions including witness data.
        /// </summary>
        Witness = 8,

        /// <summary>
        ///     BIP 159.
        /// </summary>
        NetworkLimited = 1024,

        /// <summary>
        ///     XDS decentralized messaging (Server).
        /// </summary>
        MessageRelay = 4096,

        /// <summary>
        ///     XDS decentralized messaging (Client)
        /// </summary>
        MessagingClient = 8192
    }
}
