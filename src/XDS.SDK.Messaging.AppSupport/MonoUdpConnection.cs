using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.AppSupport.NetStandard
{
    public class MonoUdpConnection : IUdpConnection
    {
        readonly AsyncLock lockObj = new AsyncLock();
        readonly List<IEnvelope> successResponse;

        public MonoUdpConnection()
        {
            this.successResponse = new List<IEnvelope>();
        }

        UdpClient udpClient;
        CancellationTokenSource _cts;
        Func<byte[], Transport, Task<string>> _receiver;

        public bool IsConnected { get; private set; }

        public async Task<bool> ConnectAsync(string remoteDnsHost, int remotePort, Func<byte[], Transport, Task<string>> receiver = null)
        {
            try
            {
                this._receiver = receiver;
                this.udpClient = new UdpClient(remoteDnsHost, remotePort);
                this.IsConnected = true;
                this._cts = new CancellationTokenSource();
                Task.Run(async () =>
                {
                    while (!this._cts.IsCancellationRequested)
                    {
                        var result = await this.udpClient.ReceiveAsync();
                        string s = await this._receiver(result.Buffer, Transport.UDP);
                    }
                });
                return true;
            }
            catch (Exception e)
            {
                DisconnectPrivate();
                return false;
            }
        }


        public async Task DisconnectAsync()
        {
            using (await this.lockObj.LockAsync())
                DisconnectPrivate();
        }

        public async Task<List<IEnvelope>> SendRequestAsync(byte[] request)
        {
            using (await this.lockObj.LockAsync())
            {
                await this.udpClient.SendAsync(request, request.Length);
                return this.successResponse;
            }
        }

       

        void DisconnectPrivate()
        {
            this.IsConnected = false;
            this._cts?.Cancel();
            this.udpClient?.Dispose();
            this.udpClient = null;
            this._cts?.Dispose();
            this._cts = null;
        }
    }
}