using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.SDK.Messaging.BlockchainClient.Data;

namespace XDS.SDK.Messaging.BlockchainClient
{
    public sealed class ConnectedPeer : IDisposable
    {
        [Flags]
        public enum State : uint
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

        readonly ILogger _logger;
        readonly TcpClient _tcpClient;

        NetworkStream _networkStream;
        public int BytesRead;


        public int BytesWritten;
        public bool PeerVerAckReceived;

        public BitcoinVersionPayload PeerVersionPayload;

        public Peer RemotePeer;
        public Peer SelfFromPeer;

        public Peer SelfFromSocket;

        public ConnectedPeer(Peer remotePeer, ILoggerFactory loggerFactory)
        {
            this.RemotePeer = remotePeer;

            this._logger = loggerFactory.CreateLogger(this.ToString());
            this._tcpClient = new TcpClient(AddressFamily.InterNetworkV6);
            this._tcpClient.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        }

        public State PeerState { get; set; }
        void DisposeAndThrow(Exception e, [CallerMemberName] string location = "")
        {
            this.PeerState |= State.Failed;

            Dispose();

            var message = $"{this} failed in {location}: {e.Message}";
            throw new ConnectedPeerException(message, e);
        }

        public void Dispose()
        {
            //if (this._tcpClient.Connected)
            //    this._tcpClient.Client.Shutdown(SocketShutdown.Both);
            this._tcpClient.Dispose();
            this.PeerState |= State.Disposed;
        }

        public async Task ConnectAsync()
        {
            try
            {
                await this._tcpClient.ConnectAsync(this.RemotePeer.IPAddress, this.RemotePeer.ProtocolPort);

                var ownIp = ((IPEndPoint)this._tcpClient.Client.LocalEndPoint).Address;
                var ownPort = ((IPEndPoint)this._tcpClient.Client.LocalEndPoint).Port;

                // track own address to avoid connecting to self
                this.SelfFromSocket = new Peer
                {
                    Id = ownIp.CreatePeerId(ownPort),
                    IPAddress = ownIp,
                    ProtocolPort = ownPort
                };

                this._networkStream = this._tcpClient.GetStream();
            }
            catch (Exception e)
            {
                DisposeAndThrow(e);
            }
        }

        public async Task VersionHandshakeAsync(string userAgent, byte[] nonce, CancellationToken cancellationToken)
        {
            try
            {
                BitcoinVersionPayload outgoingVersionPayload = PayloadFactory.CreateVersionPayload(userAgent, nonce,
                    (IPEndPoint)this._tcpClient.Client.LocalEndPoint,
                    (IPEndPoint)this._tcpClient.Client.RemoteEndPoint);

                var myVersionMessage = new BitcoinMessage("version", outgoingVersionPayload.Serialize());
                this._logger.LogInformation("Sending own version.");
                await WriteAsync(myVersionMessage.Serialize());
                this._logger.LogInformation("'version' sent.");

                // do it like this so that there is no specific expected order for version and verack, but expect both. 
                while (this.PeerVersionPayload == null || this.PeerVerAckReceived == false)
                {
                    var reply = await ReadMessageAsync(cancellationToken);
                    if (reply.Command == "version")
                    {
                        this.PeerVersionPayload = new BitcoinVersionPayload(reply.PayloadBytes);
                        this._logger.LogInformation(
                            $"Version payload decoded from {this.PeerVersionPayload.UserAgent.Text} at {this.PeerVersionPayload.Sender}.");
                        this._logger.LogInformation($"Setting own address to {this.PeerVersionPayload.Receiver}.");


                        this.SelfFromPeer = this.PeerVersionPayload.Receiver.ToPeer();

                        this._logger.LogInformation("Sending version acknowledgement.");
                        await WriteAsync(new BitcoinMessage("verack", new byte[0]).Serialize());
                        this._logger.LogInformation("'verack' sent.");
                    }
                    else if (reply.Command == "verack")
                    {
                        this.PeerVerAckReceived = true;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Received unexpected message '{reply.Command}' during handshake.");
                    }
                }
            }
            catch (Exception e)
            {
                DisposeAndThrow(e);
            }
        }

        public async Task GetAddressesAsync(Func<BitcoinNetworkAddressPayload, Task> addToAddressBook,
            CancellationToken cancellationToken)
        {
            try
            {
                this._logger.LogInformation("Asking peer for node addresses.");
                await WriteAsync(new BitcoinMessage("getaddr", new byte[0]).Serialize());
                this._logger.LogInformation("'getaddr' sent.");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var reply = await ReadMessageAsync(cancellationToken);
                    if (reply.Command == "ping")
                    {
                        await WriteAsync(new BitcoinMessage("pong", new PongPayload(reply.PayloadBytes).NonceBytes)
                            .Serialize());
                        this._logger.LogInformation("'pong' sent.");
                    }
                    else if (reply.Command == "addr")
                    {
                        var addrPayload = new BitcoinAddrPayload(reply.PayloadBytes);
                        this._logger.LogInformation($"Received {addrPayload.AddrCount.Value} addresses.");

                        foreach (BitcoinNetworkAddressPayload address in addrPayload.Addresses)
                        {
                            if (address.PeerServices.HasFlag(PeerServices.MessageRelay))
                                ;
                            await addToAddressBook(address);
                        }

                        this.PeerState |= State.AddrReceived;
                    }
                }
            }
            catch (Exception e)
            {
                DisposeAndThrow(e);
            }
        }

        

        async Task<BitcoinMessage> ReadMessageAsync(CancellationToken cancellation)
        {
            // First find and read the magic. This will block until a valid magic has been read, or throw if the connection is lost / stream ended
            await ReadMagicAsync(ChatClientConfiguration.NetworkMagicBytes, cancellation).ConfigureAwait(false);


            // Then read the rest of the header (we read the magic already), which is formed of command, length, and a required checksum.
            int headerExMagicSize = BitcoinMessage.ModernHeaderSize - BitcoinMessage.MagicSize;

            var completeHeaderBuffer =
                new byte[BitcoinMessage
                    .ModernHeaderSize]; // magic already read, now read: 12 bytes command, 4 bytes payload length, 4 bytes checksum = 20 bytes
            if (!await this
                .ReadBytesAsync(completeHeaderBuffer, BitcoinMessage.MagicSize, headerExMagicSize, cancellation)
                .ConfigureAwait(false))
                throw new ConnectedPeerException("Stream ended before the header could be read.", null);

            Buffer.BlockCopy(ChatClientConfiguration.NetworkMagicBytes, 0, completeHeaderBuffer, 0, BitcoinMessage.MagicSize);

            var command = Encoding.ASCII
                .GetString(completeHeaderBuffer, BitcoinMessage.MagicSize, BitcoinMessage.CommandSize).TrimEnd('\0');


            // Then extract the length, which is the message payload size.
            uint payloadLength = BitConverter.ToUInt32(completeHeaderBuffer,
                BitcoinMessage.MagicSize + BitcoinMessage.CommandSize);
            if (payloadLength > 0x00400000)
                throw new ProtocolViolationException("Message payload too big (over 0x00400000 bytes)");

            this._logger.LogInformation($"Received message '{command}', payload length: {payloadLength} bytes.");

            var checkSumBytes = new byte[4];
            Buffer.BlockCopy(completeHeaderBuffer,
                BitcoinMessage.MagicSize + BitcoinMessage.CommandSize + BitcoinMessage.PayloadLengthSize, checkSumBytes,
                0, BitcoinMessage.ChecksumSize);


            byte[] payloadBuffer = new byte[payloadLength];
            if (!await this.ReadBytesAsync(payloadBuffer, 0, (int)payloadLength, cancellation).ConfigureAwait(false))
                throw new ConnectedPeerException("Stream ended before the payload could be read.", null);

            byte[] completeWithHeaderAndPayload = new byte[BitcoinMessage.ModernHeaderSize + payloadLength];
            Buffer.BlockCopy(completeHeaderBuffer, 0, completeWithHeaderAndPayload, 0, completeHeaderBuffer.Length);
            Buffer.BlockCopy(payloadBuffer, 0, completeWithHeaderAndPayload, completeHeaderBuffer.Length,
                payloadBuffer.Length);

            var ret = new BitcoinMessage(command, payloadBuffer, checkSumBytes);
            return ret;
        }


        async Task ReadMagicAsync(byte[] magic, CancellationToken cancellation)
        {
            var buffer = new byte[1];

            for (int i = 0; i < magic.Length; i++)
            {
                byte expectedByte = magic[i];

                if (!await ReadBytesAsync(buffer, 0, 1, cancellation).ConfigureAwait(false))
                    throw new ConnectedPeerException("Stream ended before the magic could be read.", null);

                byte receivedByte = buffer[0];

                if (receivedByte == expectedByte) // best case, we received the expected byte at the right point
                    continue; // so we continue with next

                // If we are here, we did not read what we expected, trying to recover...

                // Did we got the first byte of the magic instead (while we were expecting a later byte)?
                bool firstMagicByteReceived = receivedByte == magic[0];
                if (firstMagicByteReceived)
                    i = 0; // loop will continue checking with magic-byte at index 1
                else
                    i = -1; // loop will continue checking with magic-byte at index 0

                //i = receivedByte == magic[0] ? 0 : -1;

                // i is incremented at the end of the loop
            }

            // loop can only exit after we either read the correct magic or we are running out of bytes to read.
        }


        async Task<bool> ReadBytesAsync(byte[] buffer, int destinationOffset, int bytesToRead,
            CancellationToken cancellation)
        {
            while (bytesToRead > 0)
            {
                int bytesRead = await this._networkStream
                    .ReadAsync(buffer, destinationOffset, bytesToRead, cancellation).ConfigureAwait(false);
                if (bytesRead == 0) return false;

                destinationOffset += bytesRead;
                bytesToRead -= bytesRead;

                this.BytesRead += bytesRead;
            }

            return true;
        }

        async Task WriteAsync(byte[] buffer)
        {
            await this._networkStream.WriteAsync(buffer, 0, buffer.Length);
            this.BytesWritten += buffer.Length;
        }


        public override string ToString()
        {
            var ip = this.RemotePeer.IPAddress.ToString();
            if (ip.StartsWith("::ffff:"))
                ip = ip.Substring(7);

            return $"{ip}:{this.RemotePeer.ProtocolPort}";
        }
    }
}