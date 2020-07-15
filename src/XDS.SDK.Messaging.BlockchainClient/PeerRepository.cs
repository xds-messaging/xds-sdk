using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XDS.SDK.Messaging.BlockchainClient.Data;
using XDS.SDK.Messaging.CrossTierTypes.FStore;

namespace XDS.SDK.Messaging.BlockchainClient
{
    public class PeerRepository
    {
        static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        readonly FStoreRepository<Peer> peers;

        public PeerRepository(FStoreConfig fStoreConfig)
        {
            this.peers = new FStoreRepository<Peer>(new FStoreMono(fStoreConfig), PeerSerializer.SerializeCore, PeerSerializer.Deserialize);
        }


        public async Task AddIfNotExistsAsync(Peer peer)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                var existingPeer = await this.peers.Get(peer.Id);
                if (existingPeer != null) 
                    return;
                await this.peers.Add(peer);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<Peer> GetPeerAsync(string id)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this.peers.Get(id);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<uint> GetPeerCountAsync()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this.peers.Count();
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<IReadOnlyList<Peer>> GetAllPeersAsync()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this.peers.GetAll();
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task UpdatePeerLastErrorAsync(string peerId, DateTime lastError)
        {
            await SemaphoreSlim.WaitAsync();

            try
            {
                Peer contact = await this.peers.Get(peerId);
                contact.LastError = lastError;
                await this.peers.Update(contact, null);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task UpdatePeerAfterHandshakeAsync(Peer peer)
        {
            await SemaphoreSlim.WaitAsync();

            try
            {
                Peer existingPeer = await this.peers.Get(peer.Id);
                existingPeer.PeerServices = peer.PeerServices;
                existingPeer.LastSeen = peer.LastSeen;
                existingPeer.LastError = DateTime.MaxValue;
                await this.peers.Update(existingPeer, null);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
    }
}