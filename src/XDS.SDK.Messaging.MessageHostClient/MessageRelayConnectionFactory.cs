﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;
using XDS.Messaging.SDK.ApplicationBehavior.Workers;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.BlockchainClient;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.MessageHostClient.Data;

namespace XDS.SDK.Messaging.MessageHostClient
{
    public class MessageRelayConnectionFactory : IWorker, ITcpConnection, IMessageRelayAddressReceiver
    {
        const int DefaultMessagingPort = 38334;

        readonly ILogger logger;
        readonly ICancellation cancellation;
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        readonly Random random = new Random();

        readonly MessageRelayRecordRepository messageRelayRecords;

        readonly ConcurrentDictionary<string, MessageRelayConnection> connections;



        public MessageRelayConnectionFactory(ILoggerFactory loggerFactory, ICancellation cancellation, MessageRelayRecordRepository messageRelayRecords)
        {
            this.messageRelayRecords = messageRelayRecords;
            this.connections = new ConcurrentDictionary<string, MessageRelayConnection>();
            this.cancellation = cancellation;
            this.logger = loggerFactory.CreateLogger<MessageRelayConnectionFactory>();

            cancellation.RegisterWorker(this);
        }

        public async Task InitializeAsync()
        {
            this.WorkerTask = Task.Run(MaintainConnectionsAsync);
            await Task.CompletedTask;
        }

        public async Task MaintainConnectionsAsync()
        {
            try
            {
                while (!this.cancellation.Token.IsCancellationRequested)
                {
                    while (this.connections.Count <= 3 && !this.cancellation.Token.IsCancellationRequested)
                    {
                        var connection = await SelectNextConnectionAsync(); // this method must block and only run on one thread, it's not thread safe

                        if (connection == null)
                        {
                            this.logger.LogInformation("Out off connection candidates...");
                            break;
                        }

                        _ = Task.Run(() => ConnectAndRunAsync(connection)).ConfigureAwait(false); // Connecting and running happens on max X threads, so that we can build up connections quickly
                    }

                    this.logger.LogInformation("Waiting 30 seconds..");
                    await Task.Delay(30000, this.cancellation.Token).ContinueWith(_ => { }); // we do not want to have a TaskCancelledException here
                }
            }
            catch (Exception e)
            {
                this.logger.LogCritical(e.Message);
                this.FaultReason = e;
            }

            ;
        }


        public bool IsConnected
        {
            get { return this.connections.Any(c => c.Value.ConnectionState == ConnectedPeer.State.Connected); }
        }

        public Task WorkerTask { get; private set; }

        public Exception FaultReason { get; private set; }

        public MessageRelayConnection[] GetCurrentConnections()
        {
            return this.connections.Values.ToArray();
        }

        public async Task<bool> ConnectAsync(string remoteDnsHost, int remotePort, Func<byte[], Transport, Task<string>> receiver = null)
        {
            return this.connections.Count > 0;
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
            var messageRelayConnection = new MessageRelayConnection(messageRelayRecord, this.cancellation.Token);
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

        async Task ConnectAndRunAsync(MessageRelayConnection createdInstance)
        {
            var connectedInstance = await CreateConnectedPeerBlockingOrThrowAsync(createdInstance);

            if (connectedInstance != null)
            {
                this.logger.LogInformation($"Successfully created connected peer {createdInstance}, loading off to new thread.");
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
            if (connection == null)  // there was no connection available
                return;

            if (e == null) // explicit disconnect
            {
                Debug.Assert(connection.ConnectionState.HasFlag(ConnectedPeer.State.Disconnecting));
                Debug.Assert(connection.ConnectionState.HasFlag(ConnectedPeer.State.Disposed));
            }
            else
            {
                Debug.Assert(connection.ConnectionState.HasFlag(ConnectedPeer.State.Failed));
                Debug.Assert(connection.ConnectionState.HasFlag(ConnectedPeer.State.Disposed));
            }


            if (PeerManager.ShouldRecordError(e, !this.cancellation.Token.IsCancellationRequested, connection.ToString(), this.logger))
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
            foreach (MessageRelayConnection activeConnection in this.connections.Values)
            {
                try
                {
                    if (activeConnection.ConnectionState.HasFlag(ConnectedPeer.State.Connected))
                    {
                        activeConnection.ConnectionState &= ~ConnectedPeer.State.Connected;
                        activeConnection.ConnectionState |= ConnectedPeer.State.Disconnecting;
                        activeConnection.Dispose();
                        this.logger.LogInformation($"{nameof(MessageRelayConnection)} {activeConnection} was disposed.");
                    }

                }
                catch (Exception e)
                {
                    this.logger.LogError($"Error disposing {nameof(MessageRelayConnection)} {activeConnection}: {e.Message}");
                }

                await HandleFailedConnectedPeerAsync(null, activeConnection);
            }
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
            var currentConnections = this.connections.Values.Where(x => x.ConnectionState.HasFlag(ConnectedPeer.State.Connected)).ToArray();
            if (currentConnections.Length == 0)
                return null;
            return currentConnections[this.random.Next(currentConnections.Length)];
        }

        public async void ReceiveMessageRelayRecordAsync(IPAddress ipAddress, int port, PeerServices peerServices, string userAgent)
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



        public string GetInfo()
        {
            return nameof(MessageRelayConnectionFactory);
        }

        public void Pause()
        {
            throw new NotImplementedException();
        }

        public void Resume()
        {
            throw new NotImplementedException();
        }
    }
}
