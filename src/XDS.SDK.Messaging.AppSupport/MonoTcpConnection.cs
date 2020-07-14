using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Workers;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.AppSupport.NetStandard
{
    public class MonoTcpConnection : ITcpConnection
    {
        bool? _expectTls;
	    readonly SemaphoreSlim _sem = new SemaphoreSlim(1, 1);
        readonly INetworkClient networkClient;
		public MonoTcpConnection(INetworkClient networkClient)
        {
            this.networkClient = networkClient;
        }

        Socket _streamSocket;

        CancellationTokenSource _cts;

        public bool IsConnected { get; private set; }

        public async Task<bool> ConnectAsync(string remoteDnsHost, int remotePort, Func<byte[], Transport, Task<string>> receiver = null)
        {

            try
            {
                if (this._expectTls == null)
                    this._expectTls = this.networkClient.GetType() == typeof(TLSClient);

                this._streamSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                this._cts = new CancellationTokenSource();
                // https://docs.microsoft.com/en-us/uwp/api/windows.networking.sockets.streamsocketcontrol#Windows_Networking_Sockets_StreamSocketControl_KeepAlive
                //_streamSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.KeepAlive, true);
                IPEndPoint ipEndPoint = GetIpEndPointFromHostName(remoteDnsHost, remotePort, true);
                this._streamSocket.Connect(ipEndPoint);

                this.IsConnected = true;
                return true;
            }
            catch (Exception e)
            {
	            await DisconnectAsync();
                return false;
            }
        }
        static IPEndPoint GetIpEndPointFromHostName(string hostName, int port, bool throwIfMoreThanOneIp)
        {
            var addresses = Dns.GetHostAddresses(hostName);
            if (addresses.Length == 0)
            {
                throw new ArgumentException(
                    "Unable to retrieve address from specified host name.",
                    nameof(hostName)
                );
            }
            else if (throwIfMoreThanOneIp && addresses.Length > 1)
            {
                throw new ArgumentException(
                    "There is more that one IP address to the specified host.",
                    nameof(hostName)
                );
            }
            return new IPEndPoint(addresses[0], port); // Port gets validated here.
        }

        public async Task DisconnectAsync()
        {

	        try
	        {
		        DisconnectPrivate();
	        }
	        catch (Exception e)
	        {
	        }
           
	        await Task.CompletedTask;
        }

        public async Task<List<IEnvelope>> SendRequestAsync(byte[] request)
        {

            var response = new List<IEnvelope>();

            try
            {
                await this._sem.WaitAsync(); // this definitely deadlocks sometimes

                if (!this.IsConnected)
                {
                    return response;
                }
                this._streamSocket.Send(request);


                var bufferSize = 4096;
                var reader = new EnvelopeReaderBuffer { Buffer = new byte[bufferSize], Payload = null };

                using (var socketStream = new SocketStream(this._streamSocket)) // SocketStream is a class I have created
                {
                    List<IEnvelope> receivedPackets;
                    if (this._expectTls == true)
                        receivedPackets = await TLSEnvelopeReader.ReceivePackets(reader, socketStream, this._cts.Token);
                    else
                        receivedPackets = await NOTLSEnvelopeReader.ReceivePackets(reader, socketStream, this._cts.Token);
                } 

            }
            catch (Exception e)
            {
					await DisconnectAsync();
                    throw;
            }
            finally
            {
               this._sem.Release();
            }
            return response;
        }

        void DisconnectPrivate()
        {
            this.IsConnected = false;
            this._cts?.Cancel();

            // To reuse the socket with another data writer, the application must detach the stream from the
            // current writer before disposing it.
            //_dataWriter?.DetachStream();
            //_dataWriter?.Dispose();
            //_dataWriter = null;

            this._streamSocket?.Dispose();
            this._streamSocket = null;
            this._cts?.Dispose();
            this._cts = null;
        }
    }
}