using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Serialization;
using XDS.SDK.Cryptography.Api.Interfaces;
using XDS.SDK.Cryptography.E2E;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.CrossTierTypes.FStore;

namespace XDS.Messaging.SDK.ApplicationBehavior.Data
{
    public class AppRepository
    {
        #region Init
        static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        readonly IAsyncRepository<Profile> profiles;
        readonly IAsyncRepository<Identity> contacts;
        readonly IAsyncRepository<Message> messages;

        string ownChatId;

        string OwnChatId
        {
            set
            {
                if (this.ownChatId == null && value != null)
                    this.ownChatId = value;
                if (value != this.ownChatId)
                    throw new InvalidOperationException();
            }
        }

        public AppRepository(FStoreConfig fStoreConfig, IXDSSecService xdsSecService)
        {
            this.profiles = new FStoreRepository<Profile>(new FStoreMono(fStoreConfig), RepositorySerializer.Serialize, RepositorySerializer.Deserialize<Profile>);
            this.contacts = new FStoreRepository<Identity>(new FStoreMono(fStoreConfig), RepositorySerializer.Serialize, RepositorySerializer.Deserialize<Identity>);
            this.messages = new FStoreRepository<Message>(new FStoreMono(fStoreConfig), RepositorySerializer.Serialize, RepositorySerializer.Deserialize<Message>);

            RepositorySerializer.XDSSecService = xdsSecService;
        }

        #endregion

        #region Profile

        public async Task AddProfile(Profile profile)
        {
            this.OwnChatId = profile.Id;
            await SemaphoreSlim.WaitAsync();
            try
            {
                var profiles = await this.profiles.GetAll();
                if (profiles.Count != 0)
                    throw new InvalidOperationException("The Profile already exists.");
                await this.profiles.Add(profile);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }


        public async Task<Profile> GetProfile()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                var profiles = await this.profiles.GetAll();
                if (profiles.Count == 1)
                    return profiles[0];
                throw new InvalidOperationException($"Could not get Profile - found {profiles.Count} Profiles.");
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task UpdateProfile(Profile profile)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                await this.profiles.Update(profile);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task DeleteProfiles(IEnumerable<Profile> profiles)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                foreach (var profile in profiles)
                    await this.profiles.Delete(profile.Id);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task UpdateEncryptedProfileImage(string id, byte[] encryptedProfileImage)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                var profile = await this.profiles.Get(id);
                profile.PictureBytes = encryptedProfileImage;
                await this.profiles.Update(profile);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        #endregion

        #region Contact

        public async Task UpdateContactImage(string id, byte[] encryptedContactImage)
        {
            Guid.Parse(id);

            await SemaphoreSlim.WaitAsync();
            try
            {
                Identity contact = await this.contacts.Get(id);
                contact.Image = encryptedContactImage;
                await this.contacts.Update(contact);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<Identity> GetContact(string id)
        {
            Guid.Parse(id);

            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this.contacts.Get(id);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task UpdateContactName(string id, string newName)
        {
            Guid.Parse(id);

            await SemaphoreSlim.WaitAsync();
            try
            {
                Identity contact = await this.contacts.Get(id);
                contact.Name = newName;
                await this.contacts.Update(contact);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task UpdateContactState(Identity c)
        {
            Guid.Parse(c.Id);
            await SemaphoreSlim.WaitAsync();
            try
            {
                Identity contact = await this.contacts.Get(c.Id);
                contact.ContactState = c.ContactState;
                await this.contacts.Update(contact);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<IReadOnlyList<Identity>> GetAllContacts()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this.contacts.GetAll();
            }
            finally
            {
                SemaphoreSlim.Release();
            }

        }

        public async Task DeleteContacts(IEnumerable<string> contactsIds)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                foreach (var id in contactsIds)
                {
                    Guid.Parse(id);
                    await this.contacts.Delete(id);
                }

            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task AddContact(Identity contact)
        {
            Guid.Parse(contact.Id);

            if (contact.ContactState == ContactState.Added && contact.UnverifiedId == this.ownChatId)
                throw new InvalidOperationException("You can't add yourself as contact.");

            await SemaphoreSlim.WaitAsync();
            try
            {
                await this.contacts.Add(contact);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task UpdateAddedContactWithPublicKey(XIdentity identity, string guidId)
        {
            Guid.Parse(guidId);

            if (identity == null)
                throw new ArgumentNullException(nameof(identity));

            if (identity.ContactState != ContactState.Valid)
                throw new Exception($"Expected IdentityState.Added but was {identity.ContactState}");

            if (identity.PublicIdentityKey == null)
                throw new Exception("The public key must not be null");

            await SemaphoreSlim.WaitAsync();
            try
            {
                var contact = await this.contacts.Get(guidId);
                if (contact != null)
                {
                    // do not ovwerwrite the name and the image of the contact from the repository!
                    contact.UnverifiedId = null;
                    contact.FirstSeenUtc = identity.FirstSeenUTC;
                    contact.LastSeenUtc = identity.LastSeenUTC;
                    contact.StaticPublicKey = identity.PublicIdentityKey;
                    contact.ContactState = identity.ContactState;
                    await this.contacts.Update(contact);
                }
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task UpdateUser(E2EUser user)
        {
            Guid.Parse(user.UserId);

            await SemaphoreSlim.WaitAsync();
            try
            {
                Identity contact = await this.contacts.Get(user.UserId);
                var serialized = E2EUserSerializer.Serialize(user);
                contact.CryptographicInformation = serialized;
                await this.contacts.Update(contact);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<E2EUser> GetUserById(string userId)
        {
            Guid.Parse(userId);

            await SemaphoreSlim.WaitAsync();
            try
            {
                var contact = await this.contacts.Get(userId);
                Debug.Assert(contact.Id == userId);
                E2EUser user = E2EUserSerializer.Deserialize(contact.CryptographicInformation);
                if (user == null) // for example, when the contact has just been added
                    user = new E2EUser { DynamicPrivateDecryptionKeys = new Dictionary<long, byte[]>() };
                user.UserId = userId;
                user.StaticPublicKey = contact.StaticPublicKey;
                return user;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        #endregion

        #region Message

        public async Task AddMessage(Message message)
        {
            var page = GuessPage(message);
            await SemaphoreSlim.WaitAsync();
            try
            {
                var previousMessages = await this.messages.GetRange(0, 1, page);
                var previousMessage = previousMessages.Count == 0 ? null : previousMessages[0];
                message.SetPreviousSide(previousMessage);
                await this.messages.Add(message, page);
            }
            finally
            {
                SemaphoreSlim.Release();
            }

        }

        public async Task<Message> GetMessage(string messageId, string page)
        {
            ValidateMessagePage(page);
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this.messages.Get(messageId, page);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }


        public async Task UpdateMessage(Message message)
        {
            var page = GuessPage(message);
            await SemaphoreSlim.WaitAsync();
            try
            {
                await this.messages.Update(message, page);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }



        public async Task DeleteMessage(string messageId, string page)
        {
            ValidateMessagePage(page);
            await SemaphoreSlim.WaitAsync();
            try
            {
                await this.messages.Delete(messageId, page);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task DeleteAllMessages(string ofContactId)
        {
            ValidateMessagePage(ofContactId);
            await SemaphoreSlim.WaitAsync();
            try
            {
                await this.messages.DeletePage(ofContactId);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }


        public async Task<uint> GetMessageCount(string page)
        {
            ValidateMessagePage(page);
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this.messages.Count(page);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<IReadOnlyList<Message>> GetMessageRange(uint firstIndex, uint maxCount, string page)
        {
            ValidateMessagePage(page);
            await SemaphoreSlim.WaitAsync();
            try
            {
                var messages = await this.messages.GetRange(firstIndex, maxCount, page);
                return messages;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<Message> GetLastMessage(string page)
        {
            var singleRange = await GetMessageRange(0, 1, page);
            return singleRange.Count == 1 ? singleRange[0] : null;
        }

        #endregion

        #region Other

        public async Task DropRecreateStoreWithAllTables()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                IFStore store = (IFStore)this.profiles.Store;
                await store.DeleteAndRecreateStore();
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<byte[]> LoadSpecialFile(string filename)
        {
            IFStore store = (IFStore)this.profiles.Store;
            return await store.LoadSpecialFile(filename);
        }

        public async Task WriteSpecialFile(string filename, byte[] buffer)
        {
            IFStore store = (IFStore)this.profiles.Store;
            await store.WriteSpecialFile(filename, buffer);
        }

        void ValidateMessagePage(string page)
        {
            if (string.IsNullOrWhiteSpace(page) || page == this.ownChatId)
                throw new InvalidOperationException();
        }

        string GuessPage(Message message)
        {
            string page;
            if (message.Side == MessageSide.Me)
                page = message.RecipientId;
            else if (message.Side == MessageSide.You)
                page = message.SenderId;
            else throw new InvalidOperationException();
            ValidateMessagePage(page);
            return page;
        }

        #endregion
    }
}
