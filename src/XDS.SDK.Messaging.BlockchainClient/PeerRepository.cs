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

        readonly FStoreRepository<Peer> _peers;

        public PeerRepository(FStoreConfig fStoreConfig)
        {
            this._peers = new FStoreRepository<Peer>(new FStoreMono(fStoreConfig), PeerSerializer.SerializeCore, PeerSerializer.Deserialize);
        }


        public async Task AddPeer(Peer peer)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                await this._peers.Add(peer);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<Peer> GetPeer(string id)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this._peers.Get(id);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<uint> GetPeerCount()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this._peers.Count();
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<IReadOnlyList<Peer>> GetAllPeers()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this._peers.GetAll();
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task UpdatePeerLastError(string peerId, DateTime lastError)
        {
            await SemaphoreSlim.WaitAsync();

            try
            {
                Peer contact = await this._peers.Get(peerId);
                contact.LastError = lastError;
                await this._peers.Update(contact, null);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task UpdatePeerAfterHandshake(Peer peer)
        {
            await SemaphoreSlim.WaitAsync();

            try
            {
                Peer existingPeer = await this._peers.Get(peer.Id);
                existingPeer.PeerServices = peer.PeerServices;
                existingPeer.LastSeen = peer.LastSeen;
                existingPeer.LastError = DateTime.MaxValue;
                await this._peers.Update(existingPeer, null);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
    }
}