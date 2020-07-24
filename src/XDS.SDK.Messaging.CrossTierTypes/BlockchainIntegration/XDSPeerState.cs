using System;
using System.Collections.Generic;
using System.Text;

namespace XDS.SDK.Messaging.CrossTierTypes.BlockchainIntegration
{
    [Flags]
    public enum XDSPeerState : uint
    {
        NotSet = 0,
        Connecting = 1,
        Connected = 2,
        VersionHandshake = 4,
        AddrReceived = 128,
        Disconnecting = 1024,
        Failed = 2048,
        Disposed = 8192
    }
}
