using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XDS.SDK.Cryptography.NoTLS;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public interface IConnection
    {
        bool IsConnected { get; }

        Task<bool> ConnectAsync(string remoteDnsHost, int remotePort, Func<byte[], Transport, Task<string>> receiver = null);

        Task DisconnectAsync();

        Task<List<IEnvelope>> SendRequestAsync(byte[] request);

    }
}
