using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.BlockchainClient;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.MessageHostClient.Data;

namespace XDS.SDK.Messaging.MessageHostClient
{
    public class MessageRelayConnectionFactory : ITcpConnection
    {
        const int DefaultMessagingPort = 38334;

        readonly ILogger logger;
        readonly ICancellation cancellation;
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        readonly Random random = new Random();
       
        readonly MessageRelayRecordRepository messageRelayRecords;

        ConcurrentDictionary<string, MessageRelayConnection> connections;

        Task connectTask;
        public MessageRelayConnectionFactory(ILoggerFactory loggerFactory, ICancellation cancellation, MessageRelayRecordRepository messageRelayRecords, PeerManager peerManager)
        {
            this.messageRelayRecords = messageRelayRecords;
            this.connections = new ConcurrentDictionary<string, MessageRelayConnection>();
            this.cancellation = cancellation;
            this.logger = loggerFactory.CreateLogger<MessageRelayConnectionFactory>();
            peerManager.OnVersionHandshakeSuccess = ReceiveMessageRelayRecordAsync;
        }


        public bool IsConnected
        {
            get { return this.connections.Count > 0; }
        }

        public MessageRelayConnection[] GetCurrentConnections()
        {
            return this.connections.Values.ToArray();
        }

        public async Task<bool> ConnectAsync(string remoteDnsHost, int remotePort, Func<byte[], Transport, Task<string>> receiver = null)
        {
            if (this.connectTask != null)
                return this.connections.Count > 0;

            this.connectTask = Task.Run(MaintainConnectionsAsync);
            return false;
        }


        public async Task MaintainConnectionsAsync()
        {

            while (!this.cancellation.ApplicationStopping.IsCancellationRequested)
            {
                while (this.connections.Count < 32 && !this.cancellation.ApplicationStopping.IsCancellationRequested)
                {
                    var connection = await SelectNextConnectionAsync(); // this method must block and only run on one thread, it's not thread safe

                    if (connection == null)
                    {
                        this.logger.LogInformation("Out off connection candidates...");
                        break;
                    }

                    _ = Task.Run(() => ConnectAndRun(connection)); // Connecting and running happens on max X threads, so that we can build up connections quickly
                }

                this.logger.LogInformation("Waiting 30 seconds..");
                await Task.Delay(30000);
            }
        }

        public async Task<MessageRelayConnection> SelectNextConnectionAsync()
        {
            IReadOnlyList<MessageRelayRecord> allRecords = await this.messageRelayRecords.GetAllMessageRelayRecordsAsync();


            if (allRecords.Count == 0)
            {
                this.logger.LogWarning("No relay addresses in the store - can't select a connection candidate!");
                return null;
            }

            // peers, shortest success ago first
            var hottestRecords = allRecords
                .Where(x => !IsExcluded(x)) // e.g. if we are already connected, or the last error is less than a certain time ago
                .OrderBy(x => DateTimeOffset.UtcNow - x.LastSeenUtc) // then, try the best candidates first
                .ThenByDescending(x => DateTimeOffset.UtcNow - x.LastErrorUtc) // then, try the candidates where the last error is longest ago
                .ToList();

            if (hottestRecords.Count == 0)
            {
                this.logger.LogDebug("After applying the filtering rules, no connection candidates remain for selection.");
                return null;
            }

            MessageRelayRecord messageRelayRecord = hottestRecords[0];
            this.logger.LogDebug($"Selected connection candidate {messageRelayRecord}, last seen {DateTimeOffset.UtcNow - messageRelayRecord.LastSeenUtc} ago, last error {DateTimeOffset.UtcNow - messageRelayRecord.LastErrorUtc} ago.");
            var messageRelayConnection = new MessageRelayConnection(messageRelayRecord, this.cancellation.ApplicationStopping.Token);
            bool addSuccess = this.connections.TryAdd(messageRelayRecord.Id, messageRelayConnection);
            Debug.Assert(addSuccess, $"Bug: Peer {messageRelayRecord} was already in the ConnectedPeers dictionary - that should not happen.");
            return messageRelayConnection;
        }

        bool IsExcluded(MessageRelayRecord relay)
        {
            if (this.connections.ContainsKey(relay.Id))
            {
                this.logger.LogDebug($"Peer {relay} is excluded, because it's in the list of connected peers.");
                return true;
            }

            if (relay.LastErrorUtc != default)
            {
                var timeSinceLastError = DateTimeOffset.UtcNow - relay.LastErrorUtc;
                if (timeSinceLastError <= TimeSpan.FromSeconds(60))
                {
                    this.logger.LogDebug(
                        $"Peer (MessageRelay) {relay} is excluded, because it's last error is only {timeSinceLastError} ago.");
                    return true;
                }
            }

            return false;
        }

        public async Task ConnectAndRun(MessageRelayConnection createdInstance)
        {
            var connectedInstance = await CreateConnectedPeerBlockingOrThrowAsync(createdInstance);

            if (connectedInstance != null)
            {
                this.logger.LogInformation(
                    $"Successfully created connected peer {createdInstance}, loading off to new thread.");
                //await RunNetworkPeer(createdInstance);
            }
        }

        async Task<MessageRelayConnection> CreateConnectedPeerBlockingOrThrowAsync(MessageRelayConnection connection)
        {
            try
            {
                connection.ConnectionState |= ConnectedPeer.State.Connecting;
                await connection.ConnectAsync();
                connection.ConnectionState &= ~ConnectedPeer.State.Connecting;
                connection.ConnectionState |= ConnectedPeer.State.Connected;

                return connection;
            }
            catch (Exception e)
            {
                await HandleFailedConnectedPeerAsync(e, connection);
                return null;
            }
        }

        async Task HandleFailedConnectedPeerAsync(Exception e, MessageRelayConnection connection)
        {
            Debug.Assert(connection.ConnectionState.HasFlag(ConnectedPeer.State.Failed));
            Debug.Assert(connection.ConnectionState.HasFlag(ConnectedPeer.State.Disposed));

            if (PeerManager.ShouldRecordError(e, this.cancellation.ApplicationStopping.IsCancellationRequested, connection.ToString(), this.logger))
            {
                // set these properties on the loaded connection instance and not only in the repository, so that we can
                // use the cached collection of Peer objects
                connection.MessageRelayRecord.LastErrorUtc = DateTime.UtcNow;
                await this.messageRelayRecords.UpdatePeerLastError(connection.MessageRelayRecord.Id,
                    connection.MessageRelayRecord.LastErrorUtc);
            }

            this.connections.TryRemove(connection.MessageRelayRecord.Id, out _);
        }



        public async Task DisconnectAsync()
        {
            await Task.CompletedTask;
        }



        public async Task<List<IEnvelope>> SendRequestAsync(byte[] request)
        {
            var response = new List<IEnvelope>();

            MessageRelayConnection currentConnection = null;

            try
            {
                await this.semaphore.WaitAsync(); // this definitely deadlocks sometimes

                currentConnection = GetRandomConnection();

                if (currentConnection == null)
                    throw new MessageRelayConnectionException("No connection(s) available, please retry later.", null) { NoConnectionAvailable = true };

                await currentConnection.SendAsync(request);
                response.AddRange(await currentConnection.ReceiveAsync());

            }
            catch (Exception e)
            {
                await HandleFailedConnectedPeerAsync(e, currentConnection);
                throw;
            }
            finally
            {
                this.semaphore.Release();
            }
            return response;
        }


        public MessageRelayConnection GetRandomConnection()
        {
            var currentConnections = this.connections.Values.ToArray();
            if (currentConnections.Length == 0)
                return null;
            return currentConnections[this.random.Next(currentConnections.Length)];
        }
        async void ReceiveMessageRelayRecordAsync(IPAddress ipAddress, int port, PeerServices peerServices, string userAgent)
        {
            try
            {
                if (peerServices.HasFlag(PeerServices.MessageRelay))
                {
                    var messageRelayRecord = new MessageRelayRecord
                    {
                        Id = PeerSerializer.CreatePeerId(ipAddress, DefaultMessagingPort),
                        // pretend we have had a successful connection already (it is likely, since we have just had a version handshake via the protocol port
                        LastSeenUtc = DateTime.UtcNow,
                        LastErrorUtc = default,
                        ErrorScore = 0
                    };
                    await this.messageRelayRecords.AddMessageNodeRecordAsync(messageRelayRecord);
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }

        }

    }
}
