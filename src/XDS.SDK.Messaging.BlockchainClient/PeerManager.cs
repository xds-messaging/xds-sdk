using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;
using XDS.SDK.Messaging.BlockchainClient.Data;

namespace XDS.SDK.Messaging.BlockchainClient
{
    public class PeerManager
    {
        readonly ICancellation cancellation;
        readonly ChatClientConfiguration chatClientConfiguration;
        readonly IMessageRelayAddressReceiver messageRelayAddressReceiver;
        readonly ConcurrentDictionary<string, ConnectedPeer> connectedPeers =
            new ConcurrentDictionary<string, ConnectedPeer>();

        readonly ILogger logger;
        readonly ILoggerFactory loggerFactory;

        readonly ConcurrentDictionary<string, Peer> ownAddresses = new ConcurrentDictionary<string, Peer>();
        readonly PeerRepository peerRepository;

        readonly byte[] sessionNonce = Tools.GetRandomNonce();

        bool hasShutDown;

        public PeerManager(ILoggerFactory loggerFactory, ICancellation cancellation,
            IChatClientConfiguration chatClientConfiguration, PeerRepository peerRepository, IMessageRelayAddressReceiver messageRelayAddressReceiver)
        {
            this.loggerFactory = loggerFactory;
            this.cancellation = cancellation;
            this.chatClientConfiguration = (ChatClientConfiguration)chatClientConfiguration;

            this.logger = this.loggerFactory.CreateLogger<PeerManager>();

            this.cancellation.ApplicationStopping.Token.Register(DisposeAll);

            this.peerRepository = peerRepository;
            this.messageRelayAddressReceiver = messageRelayAddressReceiver;
        }


        public async Task AddSeedNodesIfMissingAsync()
        {
            // The seed nodes should only be added if there are not already present, so that connection data is not overwritten
            foreach (var node in this.chatClientConfiguration.SeedNodes)
            {
                var peerId = IPAddress.Parse(node).CreatePeerId(ChatClientConfiguration.DefaultPort);

                var existingPeer = await this.peerRepository.GetPeerAsync(peerId);
                if (existingPeer != null) continue;

                var peer = new Peer
                {
                    Id = peerId,
                    PeerServices = PeerServices.Network,
                    LastSeen = DateTime.UtcNow
                        .AddDays(-1) // seed nodes should no be promoted as the 'freshest' connections, but also not as archaic
                };

                await this.peerRepository.AddIfNotExistsAsync(peer);
            }
        }

        public async Task StartAsync()
        {

            while (!this.cancellation.ApplicationStopping.IsCancellationRequested)
            {
                while (this.connectedPeers.Count < 32 && !this.cancellation.ApplicationStopping.IsCancellationRequested)
                {
                    ConnectedPeer
                        nextPeer = await CreateNextPeerAsync(); // this method must block and only run on one thread, it's not thread safe


                    if (nextPeer == null)
                    {
                        this.logger.LogInformation("Out off connection candidates...");
                        break;
                    }

                    _ = Task.Run(() =>
                        ConnectAndRunAsync(
                            nextPeer)); // Connecting and running happens on max X threads, so that we can build up connections quickly
                }

                this.logger.LogInformation("Waiting 30 seconds..");
                await Task.Delay(30000);
            }
        }

        public void PrintStatus()
        {
            try
            {
                var sb = new StringBuilder();
                var count = 0;
                var all = this.connectedPeers.Values;
                var conn = all.Where(x => x.PeerState.HasFlag(ConnectedPeer.State.VersionHandshake)).ToArray();
                var attempting = all.Where(x => !x.PeerState.HasFlag(ConnectedPeer.State.VersionHandshake)).ToArray();
                foreach (var connectedPeer in conn)
                {
                    count++;
                    sb.AppendLine(
                        $"{connectedPeer}".PadRight(24) +
                        $" {connectedPeer.PeerVersionPayload.UserAgent.Text}".PadRight(24) +
                        $" Bytes in: {connectedPeer.BytesRead}, out: {connectedPeer.BytesWritten}".PadRight(24) +
                        $" Svc: {connectedPeer.RemotePeer.PeerServices}".PadRight(24)
                    );
                }

                if (attempting.Length > 0)
                {
                    sb.AppendLine($"===== Peer Manager: {attempting.Length} connections in progress =====");

                    foreach (var connectedPeer in attempting)
                        sb.AppendLine(
                            $"{connectedPeer}".PadRight(24) +
                            $" {connectedPeer.PeerState}".PadRight(24) +
                            $" Bytes in: {connectedPeer.BytesRead}, out: {connectedPeer.BytesWritten}".PadRight(24) +
                            $" Svc: {connectedPeer.RemotePeer.PeerServices}".PadRight(24)
                        );
                }


                var list = sb.ToString();

                this.logger.LogInformation(
                    $"===== Peer Manager: {count} connections, {this.peerRepository.GetPeerCountAsync().Result} known addresses =====\r\n{list}");
            }
            catch (Exception e)
            {
                this.logger.LogError($"Error in PrintStatus: {e.Message}");
            }
        }

        public async Task WaitForShutdownAsync()
        {
            while (!this.hasShutDown) await Task.Delay(100);
        }

        public async Task<ConnectedPeer> CreateNextPeerAsync()
        {
            IReadOnlyList<Peer> allPeers = await this.peerRepository.GetAllPeersAsync();

            if (allPeers.Count == 0)
            {
                this.logger.LogWarning("No peer addresses in the store - can't select a connection candidate!");
                return null;
            }

            // peers, shortest success ago first
            var hotPeers = allPeers
                .Where(x => !IsExcluded(
                    x)) // e.g. if it's our own address, if we are already connected, or the last error is less than X minutes ago
                .OrderByDescending(x => x.PeerServices.HasFlag(PeerServices.MessageRelay))
                .ThenBy(x => DateTimeOffset.UtcNow - x.LastSeen) // then, try the best candidates first
                .ThenByDescending(x =>
                    DateTimeOffset.UtcNow - x.LastError) // then, try the candidates where the last error is longest ago
                .ToList();

            if (hotPeers.Count == 0)
            {
                this.logger.LogDebug(
                    "After applying the filtering rules, no connection candidates remain for selection.");
                return null;
            }

            var candidate = hotPeers[0];
            this.logger.LogDebug(
                $"Selected connection candidate {candidate}, last seen {DateTimeOffset.UtcNow - candidate.LastSeen} ago, last error {DateTimeOffset.UtcNow - candidate.LastError} ago.");
            ConnectedPeer connectedPeer = new ConnectedPeer(candidate, this.loggerFactory);
            bool addSuccess = this.connectedPeers.TryAdd(connectedPeer.RemotePeer.Id, connectedPeer);
            Debug.Assert(addSuccess,
                $"Bug: Peer {candidate} was already in the ConnectedPeers dictionary - that should not happen.");
            return connectedPeer;
        }

        public async Task ConnectAndRunAsync(ConnectedPeer createdInstance)
        {
            var connectedInstance = await CreateConnectedPeerBlockingOrThrowAsync(createdInstance);

            if (connectedInstance != null)
            {
                this.logger.LogInformation(
                    $"Successfully created connected peer {createdInstance}, loading off to new thread.");
                await RunNetworkPeerAsync(createdInstance);
            }
        }

        async Task RunNetworkPeerAsync(ConnectedPeer connectedPeer)
        {
            Debug.Assert(connectedPeer.PeerState.HasFlag(ConnectedPeer.State.Connected));

            try
            {
                await connectedPeer.VersionHandshakeAsync(this.chatClientConfiguration.UserAgentName, this.sessionNonce, this.cancellation.ApplicationStopping.Token);
                connectedPeer.PeerState |= ConnectedPeer.State.VersionHandshake;

                this.ownAddresses.TryAdd(connectedPeer.SelfFromPeer.Id, connectedPeer.SelfFromPeer);

                // update also loaded (cached) instance and not only the repo
                connectedPeer.RemotePeer.PeerServices = connectedPeer.PeerVersionPayload.Services; // update services
                connectedPeer.RemotePeer.LastSeen = DateTime.UtcNow; // update success
                connectedPeer.RemotePeer.LastError = DateTime.MaxValue; // clear error
                await this.peerRepository.UpdatePeerAfterHandshakeAsync(connectedPeer.RemotePeer);

                this.messageRelayAddressReceiver.ReceiveMessageRelayRecordAsync(connectedPeer.RemotePeer.IPAddress,
                    connectedPeer.RemotePeer.ProtocolPort,
                    connectedPeer.RemotePeer.PeerServices, connectedPeer.PeerVersionPayload.UserAgent.Text);
               


                await connectedPeer.GetAddressesAsync(AddToAddressBookIfNotExistsAsync, this.cancellation.ApplicationStopping.Token);
                connectedPeer.PeerState |= ConnectedPeer.State.AddrReceived;
            }
            catch (Exception e)
            {
                await HandleFailedConnectedPeerAsync(e, connectedPeer);
            }
        }

       

        bool IsExcluded(Peer peer)
        {
            if (this.connectedPeers.ContainsKey(peer.Id))
            {
                this.logger.LogDebug($"Peer {peer} is excluded, because it's in the list of connected peers.");
                return true;
            }

            if (this.ownAddresses.ContainsKey(peer.Id))
            {
                this.logger.LogDebug($"Peer {peer} is excluded, because it's an address of ourselves.");
                return true;
            }

            if (peer.HasLastError)
            {
                var timeSinceLastError = DateTimeOffset.UtcNow - peer.LastError;

                if (peer.PeerServices.HasFlag(PeerServices.MessageRelay))
                {
                    if (timeSinceLastError <= TimeSpan.FromSeconds(15))
                    {
                        this.logger.LogDebug(
                            $"Peer (MessageRelay) {peer} is excluded, because it's last error is only {timeSinceLastError} ago.");
                        return true;
                    }
                }
                else
                {
                    if (timeSinceLastError <= TimeSpan.FromMinutes(3))
                    {
                        this.logger.LogDebug(
                            $"Peer (not a MessageRelay) {peer} is excluded, because it's last error is only {timeSinceLastError} ago.");
                        return true;
                    }
                }
            }

            return false;
        }

        async Task AddToAddressBookIfNotExistsAsync(BitcoinNetworkAddressPayload receivedAddress)
        {
            var peer = receivedAddress.ToPeer();

           

            await this.peerRepository.AddIfNotExistsAsync(peer);
        }

        async Task<ConnectedPeer> CreateConnectedPeerBlockingOrThrowAsync(ConnectedPeer connectedPeer)
        {
            try
            {
                connectedPeer.PeerState |= ConnectedPeer.State.Connecting;
                await connectedPeer.ConnectAsync();

                this.ownAddresses.TryAdd(connectedPeer.SelfFromSocket.Id, connectedPeer.SelfFromSocket);
                connectedPeer.PeerState |= ConnectedPeer.State.Connected;

                return connectedPeer;
            }
            catch (Exception e)
            {
                await HandleFailedConnectedPeerAsync(e, connectedPeer);
                return null;
            }
        }

        async Task HandleFailedConnectedPeerAsync(Exception e, ConnectedPeer connectedPeer)
        {
            Debug.Assert(connectedPeer.PeerState.HasFlag(ConnectedPeer.State.Failed));
            Debug.Assert(connectedPeer.PeerState.HasFlag(ConnectedPeer.State.Disposed));

            if (ShouldRecordError(e, this.cancellation.ApplicationStopping.IsCancellationRequested, connectedPeer.ToString(), this.logger))
            {
                // set these properties on the loaded connectedPeer instance and not only in the repository, so that we can
                // use the cached collection of Peer objects
                connectedPeer.RemotePeer.LastError = DateTime.UtcNow;
                await this.peerRepository.UpdatePeerLastErrorAsync(connectedPeer.RemotePeer.Id,
                    connectedPeer.RemotePeer.LastError);
            }

            var removeSuccess = this.connectedPeers.TryRemove(connectedPeer.RemotePeer.Id, out _);
            Debug.Assert(removeSuccess);
        }

       public static  bool ShouldRecordError(Exception e, bool isCancelled, string connectionDescription, ILogger logger)
        {
            if (isCancelled) // the app is closing, no error
                return false;

            if (e is ConnectedPeerException cpe)
            {
                if (cpe.InnerException is SocketException se)
                {
                    if (se.ErrorCode == 10051) // we have lost our internet connection, the peer was good, no error
                        return false;

                    if (se.ErrorCode == 10060)
                    {
                        var message =
                            $"Marking {connectionDescription} as bad, did not respond ({10060})"; // 'this' is the remote address
                        logger.LogInformation(message);
                        return true;
                    }

                    if (se.ErrorCode == 10061)
                    {
                        var message = $"Marking {connectionDescription} as bad, refused the connection ({10061})";
                        logger.LogInformation(message);
                        return true;
                    }
                }

                if (cpe.InnerException is IOException _) return false; // this is also the internet connection's fault
            }
            else
            {
                return
                    false; // the error has nothing to do with the socket, we do not want to mark the peer as bad in that case either
            }

            var m = $"Marking {connectionDescription} as bad, error: {e.Message}";
            logger.LogInformation(m);
            return false; // in case of doubt, we record the error - this should probably be refined.
        }

        void DisposeAll()
        {
            foreach (var connectedPeer in this.connectedPeers.Values)
                try
                {
                    connectedPeer.Dispose();
                    this.logger.LogInformation($"Network peer {connectedPeer} was disposed.");
                }
                catch (Exception e)
                {
                    this.logger.LogError($"Error disposing network peer {connectedPeer} {e.Message}");
                }

            this.logger.LogInformation("All connections closed, all network peers disposed.");
            this.hasShutDown = true;
        }
    }
}