using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using XDS.Messaging.SDK.AppSupport.NetStandard;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.BlockchainClient;
using XDS.SDK.Messaging.MessageHostClient.Data;

namespace XDS.SDK.Messaging.MessageHostClient
{
    public class MessageRelayConnection
    {
        private MessageRelayRecord messageMessageRelayRecord;
        private CancellationToken cancellationToken;

        public MessageRelayRecord MessageRelayRecord { get; }
        public TcpClient TcpClient { get; }

        public NetworkStream NetworkStream { get; private set; }

        public MessageRelayConnection(MessageRelayRecord messageMessageRelayRecord, CancellationToken cancellationToken)
        {
            this.MessageRelayRecord = messageMessageRelayRecord;
            this.TcpClient = new TcpClient(AddressFamily.InterNetworkV6);
            this.TcpClient.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            this.cancellationToken = cancellationToken;
        }

        public ConnectedPeer.State ConnectionState { get; set; }

        public ulong BytesSent { get; private set; }
        public ulong BytesReceived { get; private set; }

        public async Task ConnectAsync()
        {
            try
            {
                await this.TcpClient.ConnectAsync(this.MessageRelayRecord.IpAddress, this.MessageRelayRecord.MessagingPort);

                this.NetworkStream = this.TcpClient.GetStream();
            }
            catch (Exception e)
            {
                DisposeAndThrow(e);
            }
        }

        void DisposeAndThrow(Exception e, [CallerMemberName] string location = "")
        {
            this.ConnectionState |= ConnectedPeer.State.Failed;

            Dispose();

            var message = $"{this} failed in {location}: {e.Message}";
            throw new MessageRelayConnectionException(message, e);
        }

        public void Dispose()
        {
            try
            {
                this.TcpClient.Client.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // ignored
            }

            this.TcpClient.Dispose();
            this.ConnectionState |= ConnectedPeer.State.Disposed;
        }

        public async Task SendAsync(byte[] request)
        {
            try
            {
                await this.NetworkStream.WriteAsync(request, 0, request.Length, this.cancellationToken);
                this.BytesSent += (ulong)request.Length;
            }
            catch (Exception e)
            {
                DisposeAndThrow(e);
            }
        }

        public async Task<List<IEnvelope>> ReceiveAsync()
        {
            try
            {
                var reader = new EnvelopeReaderBuffer { Buffer = new byte[4096], Payload = null };

                using (var socketStream = new SocketStream(this.TcpClient.Client)) // SocketStream is a class I have created
                {
                    //if (_expectTls == true)
                    //	receivedPackets = await TLSEnvelopeReader.ReceivePackets(reader, socketStream, this.cts.Token);
                    //else

                    List<IEnvelope> receivedPackets = await NOTLSEnvelopeReader.ReceivePackets(reader, socketStream, this.cancellationToken);
                    foreach (var packet in receivedPackets)
                    {
                        this.BytesReceived += (ulong)packet.EncipheredPayload.Length;
                    }

                    return receivedPackets;
                } // does t his something to the underlying socket...? No! Calls NetworkStream.Dispose which calls stream.Close
            }
            catch (Exception e)
            {
                DisposeAndThrow(e);
                return default;
            }
        }
    }
}
