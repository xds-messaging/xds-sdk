using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations;
using XDS.SDK.Cryptography.Api.DataTypes;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.Api.Interfaces;
using XDS.SDK.Cryptography.E2E;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Workers
{
    public class ChatWorker
    {
        public event EventHandler<Message> SendMessageStateUpdated;
        public event EventHandler<Message> IncomingMessageDecrypted;

        readonly ILogger logger;
        readonly AppState appState;
        readonly AppRepository repo;
        readonly IChatClient chatClient;
        readonly IUdpConnection udp;
        readonly ITcpConnection tcp;
        readonly IXDSSecService ixdsCryptoService;
        readonly IChatEncryptionService chatEncryptionService;
        readonly E2ERatchet e2eRatchet;

        readonly AbstractSettingsManager settingsManager;
        readonly ContactListManager contactListManager;

        string ownChatId;
        bool isInitialized;
        bool isRunning;
        bool hasStopped;
        int interval;
        bool readToReceive;

        public ChatWorker(ILoggerFactory loggerFactory, AppState appState, AppRepository appRepository, ITcpConnection tcpConnection, IUdpConnection udpConnection, IChatClient chatClient, IXDSSecService ixdsCryptoService, IChatEncryptionService chatEncryptionService, E2ERatchet e2eRatchet, AbstractSettingsManager settingsManager, ContactListManager contactListManager)
        {
            this.logger = loggerFactory.CreateLogger<ChatWorker>();
            this.appState = appState;
            this.repo = appRepository;
            this.tcp = tcpConnection;
            this.udp = udpConnection;
            this.chatClient = chatClient;
            this.ixdsCryptoService = ixdsCryptoService;
            this.chatEncryptionService = chatEncryptionService;
            this.e2eRatchet = e2eRatchet;
            this.settingsManager = settingsManager;
            this.contactListManager = contactListManager;
        }

        #region Start, Stop, Run


        public async Task InitAsync()
        {


            try
            {
                if (this.isInitialized)
                    return;

                var profile = await this.repo.GetProfile();
                profile.IsIdentityPublished = false;
                await this.repo.UpdateProfile(profile);

                this.e2eRatchet.InitialiseFromRatchetParameters(new E2ERatchetParameters
                {
                    OwnId = profile.Id,
                    OwnStaticPrivateKey = profile.PrivateKey,
                    GetUser = this.repo.GetUserById,
                    UpdateUser = this.repo.UpdateUser
                });


                this.ownChatId = ChatId.GenerateChatId(profile.PublicKey);

                this.chatClient.Init(this.ownChatId, profile.PrivateKey);

                this.interval = this.settingsManager.ChatSettings.Interval;
                this.isInitialized = true;
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                throw;
            }
        }

        public void StartRunning()
        {


            if (this.isRunning)
                return;
            this.isRunning = true;
            this.hasStopped = false;
            this.tcp.ConnectAsync(default, default);
            Task.Run(RunUntilStopAsync);
        }

        public async Task StopRunLoopAndDisconnectAllAsync()
        {
            this.isRunning = false;
            await this.tcp.DisconnectAsync();
            while (!this.hasStopped)
            {
                await Task.Delay(100);
            }
        }


        async Task RunUntilStopAsync()
        {


            while (this.isRunning)
            {
                try
                {
                    await RunWithStateAsync();
                }
                catch (Exception e)
                {
                    this.logger.LogError(e.Message);
                }
                await Task.Delay(this.interval);
            }
            this.hasStopped = true;
        }

        async Task RunWithStateAsync()
        {
            if (!this.tcp.IsConnected)
            {
                return;
            }

            // this might run a bit to often
            
            await TryGetMissingPublicKeysForContactsAsync();

            //if (!this.appState.AreContactsChecked)
            //{
            //    bool areContactsChecked = await TryGetMissingPublicKeysForContactsAsync();
            //    this.appState.SetAreContactsChecked(areContactsChecked);

            //    if (!areContactsChecked)
            //        return;
            //}

            if (!this.appState.IsIdentityPublished)
            {
                bool isIdentityPublished = await PublishIdentity();
                this.appState.SetIsIdentityPublished(isIdentityPublished);

                if (!isIdentityPublished)
                    return;
            }


            this.readToReceive = true;

            await SendReadReceipts();

            await CheckForResendRequestsAsync();

            if (!this.appState.IsMessagesWaiting)
            {
                var anyNewsResponse = await this.chatClient.AnyNews(this.ownChatId);
                if (!anyNewsResponse.IsSuccess)
                {
                    // if we are here, the datagram socket may have become null, which happenes after a sleep of a couple of minutes.
                    await this.udp.DisconnectAsync(); // 'disconnect' explicitly
                    this.appState.IsUdpConnected = false;
                    await this.tcp.DisconnectAsync();

                }
                else
                {
                    this.appState.SetIsMessagesWaiting(anyNewsResponse.Result > 0);
                }
            }
            else
            {
                if (this.readToReceive)
                {
                    this.readToReceive = false;
                    Response<IReadOnlyCollection<XMessage>> downloadMessagesResponse = await this.chatClient.DownloadMessages(this.ownChatId);

                    if (downloadMessagesResponse.IsSuccess)
                    {
                        foreach (var xMessage in downloadMessagesResponse.Result.OrderBy(x => x.DynamicPublicKeyId))
                        {
                            try
                            {
                                await DecryptDownloadedMessage(xMessage);
                            }
                            catch (Exception e)
                            {
                                this.logger.LogError(e.Message);
                            }
                        }
                    }
                    this.appState.SetIsMessagesWaiting(false);
                    this.readToReceive = true;
                }
            }
        }

        async Task CheckForResendRequestsAsync()
        {
            // 1. Get the hashes of messages where we did not get a delivery or read receipt
            var contacts = await this.repo.GetAllContacts();

            foreach (var c in contacts.Where(c => c.ContactState == ContactState.Valid).ToArray())
            {
                // First, look at the last message and try to detect cases where simply everything is ok
                var lastMessage = await this.repo.GetLastMessage(c.Id);
                if (lastMessage == null // if there is no message, there is nothing to resend
                   || lastMessage.Side == MessageSide.You // if the last message was incoming, it means the other side has the keys, it's ok
                   || lastMessage.SendMessageState == SendMessageState.Delivered || lastMessage.SendMessageState == SendMessageState.Read) // also nothing to repair
                    continue;

                // Now let's look deeper. We may have sent a couple of messages, that are all still without receipts. That means our state is XDSNetwork.
                // It's possible they could't be read, and the other side has already posted resend requests.
                // Of course it's also possible the other side has just been offline though. Unfortunately, we don't know.
                var moreLastMessages = await this.repo.GetMessageRange(0, 10, c.Id);

                foreach (var message in moreLastMessages)
                {
                    if (message.Side == MessageSide.Me && message.SendMessageState == SendMessageState.XDSNetwork && message.SendMessageState != SendMessageState.Resent
                        && DateTime.UtcNow - message.GetEncrytedDateUtc() > TimeSpan.FromSeconds(60) // old enough to check
                        && DateTime.UtcNow - message.GetEncrytedDateUtc() < TimeSpan.FromDays(7)) // young enough not to be expired
                    {
                        var resendRequestQuery = new XResendRequest { Id = message.NetworkPayloadHash, RecipientId = c.ChatId };
                        var response = await this.chatClient.CheckForResendRequest(resendRequestQuery); // check if resend request has been posted, or if the sent message is still there and has not yet been fetched.

                        if (!response.IsSuccess)
                            return; // give up the whole procedure for all contacts, something else is broken

                        if (response.Result == 0)
                        {
                            // There is nothing on the server. Not the original message and no resend request. Possible causes:
                            // - The recipient did download it but did not send a resend request, or a receipt yet.
                            // - The message or receipt or resend request was lost.
                            // => keep checking. It's possible a resend request or receipt arrives, it drops out the top 10 we are checking
                            // or max ago for checking after messages arrives, so that we stop checking eventually.
                            // => but do not resend the message.
                            // => but do check the other messages of this contact.
                            message.SendMessageState = SendMessageState.Untracable;
                            await this.repo.UpdateMessage(message);
                            SendMessageStateUpdated?.Invoke(this, message);
                            continue;
                        }

                        if (response.Result == 1)
                        {
                            // The original message is still on the server.
                            // - The recipient hasn't downloaded his messages.
                            // => stop checking this contact
                            break;
                        }

                        if (response.Result == 2)
                        {
                            // yes, there is a resend request on the server.
                            // => resend the message
                            await this.chatEncryptionService.DecryptCipherTextInVisibleBubble(message);
                            await this.chatEncryptionService.EncryptMessage(message, true);
                            var resendResponse = await this.chatClient.UploadMessage(message);
                            if (resendResponse.IsSuccess)
                            {
                                message.SendMessageState = SendMessageState.Resent;
                                await this.repo.UpdateMessage(message);
                                SendMessageStateUpdated?.Invoke(this, message);
                            }
                        }
                    }
                }
            }
        }

        async Task DecryptDownloadedMessage(XMessage xMessage)
        {


            Message decryptedMessage = await TryDecryptMessageToFindSenderEnryptDecryptionKeyAndSaveIt(xMessage);

            if (decryptedMessage == null) // TODO: we'll be here also when Ratchet mismatch error is happening. Then, the message should not be just dropped. Do we have the sender's Id so that we can notify him?
                return; // Ignore the message - it was truely garbled or a resend request was already queued. In both cases we are done with it here.

            if (decryptedMessage.MessageType.IsReceipt())
            {
                Message messageToConfirm = await this.repo.GetMessage(decryptedMessage.SenderLocalMessageId, decryptedMessage.SenderId);
                if (messageToConfirm != null) // the message could have already been deleted
                {
                    messageToConfirm.SendMessageState = decryptedMessage.MessageType == MessageType.DeliveryReceipt ? SendMessageState.Delivered : SendMessageState.Read;
                    await this.repo.UpdateMessage(messageToConfirm);
                    SendMessageStateUpdated?.Invoke(this, messageToConfirm);
                }
                return;
            }

            if (decryptedMessage.MessageType.IsContent())
            {

                if (this.contactListManager.CurrentContact == null || this.contactListManager.CurrentContact.Id != decryptedMessage.SenderId)
                {
                    await SendReceipt(decryptedMessage, MessageType.DeliveryReceipt);

                    // This message will become an UNREAD message and not be displayed
                    await this.contactListManager.HandelUnreadAndChatPreviewForIncomingMessageFromChatWorker(decryptedMessage, countAsUnread: true);
                }
                else
                {
                    await SendReceipt(decryptedMessage, MessageType.ReadReceipt);

                    // this message will become an READ message and display immediately
                    await this.contactListManager.HandelUnreadAndChatPreviewForIncomingMessageFromChatWorker(decryptedMessage, countAsUnread: false);
                    IncomingMessageDecrypted?.Invoke(this, decryptedMessage);
                }
            }

            #region Incoming Contact Logic
            // Check if the sender is in our contacts
            // OLD: Identity contactInList = existingContacts.SingleOrDefault(c => c.ChatId == msg.SenderId);
            /*
			Identity contactInList = existingContacts.SingleOrDefault(c => c.ChatId == msg.SenderId);
			if (contactInList != null && contactInList.ContactState == ContactState.Valid)
				continue; // Yes, proceed checking the nex message

			if (contactInList != null && contactInList.ContactState == ContactState.Added)
			{
				await VerifyContactInAddedState(contactInList);

				// Check if the contact is valid now
				existingContacts = await repo.GetAllContacts();
				contactInList = existingContacts.SingleOrDefault(c => c.ChatId == msg.SenderId);
				if (contactInList.ContactState == ContactState.Valid)
					continue; // Yes, proceed checking the nex message

				// If still not valid, drop the message
				msg.MessageType = MessageType.None; // MessageType.None will be ignored by the code below
				continue; // Proceed checking the nex message
			}
			var incomingContact = new Identity { Id = Guid.NewGuid().ToString(), UnverifiedId=msg.SenderId, ContactState = ContactState.Added, Name = "Incoming Contact" };
			await repo.AddContact(incomingContact);
			await VerifyContactInAddedState(incomingContact);

			// Check if the incoming contact is valid now
			existingContacts = await repo.GetAllContacts();
			contactInList = existingContacts.SingleOrDefault(c => c.ChatId == msg.SenderId);
			if (contactInList.ContactState == ContactState.Valid)
				continue; // Yes, proceed checking the next message

			// If still not valid, drop the message
			msg.MessageType = MessageType.None; // MessageType.None will be ignored by the code below
			*/
            #endregion
        }

        /// <summary>
        /// Fully decrypts a message of all types, both control and content messages (including images), if an end-to-en-encryption key can be determined.
        /// </summary>
        async Task<Message> TryDecryptMessageToFindSenderEnryptDecryptionKeyAndSaveIt(XMessage xmessage)
        {


            try
            {


                IReadOnlyList<Identity> contacts = await this.repo.GetAllContacts();

                var decryptedMessage = new Message
                {
                    // XMessage fields
                    RecipientId = "1", // drop my Id

                    TextCipher = xmessage.TextCipher,
                    ImageCipher = xmessage.ImageCipher,

                    DynamicPublicKey = xmessage.DynamicPublicKey,
                    DynamicPublicKeyId = xmessage.DynamicPublicKeyId,
                    PrivateKeyHint = xmessage.PrivateKeyHint,

                    LocalMessageState = LocalMessageState.JustReceived,
                    SendMessageState = SendMessageState.None,
                    Side = MessageSide.You,

                    // This is what we try to decrypt here
                    MessageType = MessageType.None,
                    SenderId = null,
                };


                Response<CipherV2> decodeMetaResponse = this.ixdsCryptoService.BinaryDecodeXDSSec(xmessage.MetaCipher, null);
                if (!decodeMetaResponse.IsSuccess)
                    return null; // garbled, no chance, ignore!

                foreach (Identity identity in contacts)
                {
                    GetE2EDecryptionKeyResult getE2EDecryptionKeyResult = await this.e2eRatchet.GetEndToEndDecryptionKeyAsync(identity.Id, xmessage.DynamicPublicKey, xmessage.PrivateKeyHint);
                    if (getE2EDecryptionKeyResult.E2EDecryptionKeyType == E2EDecryptionKeyType.UnavailableDynamicPrivateKey)
                        continue; // there was a privateKeyHint != 0 in the message, but the dynamic private key was not in the ratchet for user in the loop

                    KeyMaterial64 keyMaterial64 = getE2EDecryptionKeyResult.E2EDecryptionKeyMaterial;

                    var decryptMetaResponse = this.ixdsCryptoService.BinaryDecrypt(decodeMetaResponse.Result, keyMaterial64, null);
                    if (!decryptMetaResponse.IsSuccess)
                        continue;

                    await this.e2eRatchet.SaveIncomingDynamicPublicKeyOnSuccessfulDecryptionAsync(identity.Id, xmessage.DynamicPublicKey,
                        xmessage.DynamicPublicKeyId);

                    XMessageMetaData metadata = decryptMetaResponse.Result.GetBytes().DeserializeMessageMetadata();

                    decryptedMessage.SenderId = identity.Id;
                    decryptedMessage.MessageType = metadata.MessageType;
                    decryptedMessage.SenderLocalMessageId = metadata.SenderLocalMessageId.ToString();


                    if (decryptedMessage.MessageType.IsReceipt())
                        return decryptedMessage;

                    if (decryptedMessage.MessageType.IsContent())
                    {
                        if (decryptedMessage.MessageType == MessageType.Text || decryptedMessage.MessageType == MessageType.TextAndMedia || decryptedMessage.MessageType == MessageType.File)
                        {
                            // if the message has text, decrypt all text
                            var decodeTextResponse = this.ixdsCryptoService.BinaryDecodeXDSSec(xmessage.TextCipher, null);
                            if (!decodeTextResponse.IsSuccess)
                                return null; // something is wrong, should have worked
                            var decrpytTextResponse = this.ixdsCryptoService.Decrypt(decodeTextResponse.Result, keyMaterial64, null);
                            if (!decrpytTextResponse.IsSuccess)
                                return null; // something is wrong, should have worked
                            decryptedMessage.ThreadText = decrpytTextResponse.Result.Text;
                        }

                        if (decryptedMessage.MessageType == MessageType.Media || decryptedMessage.MessageType == MessageType.TextAndMedia || decryptedMessage.MessageType == MessageType.File)
                        {
                            // if the message has image content, decrypt the image content
                            var decodeImageResponse = this.ixdsCryptoService.BinaryDecodeXDSSec(xmessage.ImageCipher, null);
                            if (!decodeImageResponse.IsSuccess)
                                return null; // something is wrong, should have worked
                            var decrpytImageResponse = this.ixdsCryptoService.BinaryDecrypt(decodeImageResponse.Result, keyMaterial64, null);
                            if (!decrpytImageResponse.IsSuccess)
                                return null; // something is wrong, should have worked
                            decryptedMessage.ThreadMedia = decrpytImageResponse.Result.GetBytes();
                        }

                        decryptedMessage.EncryptedE2EEncryptionKey = this.ixdsCryptoService.DefaultEncrypt(keyMaterial64.GetBytes(), this.ixdsCryptoService.SymmetricKeyRepository.GetMasterRandomKey());
                        decryptedMessage.LocalMessageState = LocalMessageState.Integrated;
                        await this.repo.AddMessage(decryptedMessage);
                    }
                    else
                        throw new Exception($"Invalid MessageType {decryptedMessage.MessageType}");

                    return decryptedMessage;
                } // foreach



                // If we are here, assume it's a new contact's message or RESENT message:
                CipherV2 encryptedMetaData = decodeMetaResponse.Result;
                KeyMaterial64 initialKey = this.e2eRatchet.GetInitialE2EDecryptionKey(xmessage.DynamicPublicKey);
                var decryptInitialMetaResponse = this.ixdsCryptoService.BinaryDecrypt(encryptedMetaData, initialKey, null);
                if (decryptInitialMetaResponse.IsSuccess)
                {
                    XMessageMetaData initialMessageMetadata =
                        decryptInitialMetaResponse.Result.GetBytes().DeserializeMessageMetadata();
                    var incomingPublicKey = initialMessageMetadata.SenderPublicKey;

                    // If we received several resent messages, the first from that incoming contact has already produced that contact:
                    var contact = contacts.SingleOrDefault(c => ByteArrays.AreAllBytesEqual(c.StaticPublicKey, incomingPublicKey));
                    bool isIncomingContact = false;

                    if (contact == null)
                    {
                        // Create new contact and save it to make the ratchet work normally
                        isIncomingContact = true;
                        var date = DateTime.UtcNow;
                        var incomingContact = new Identity
                        {
                            Id = Guid.NewGuid().ToString(),
                            StaticPublicKey = incomingPublicKey,
                            ContactState = ContactState.Valid,
                            Name = "Incoming Contact",
                            FirstSeenUtc = date,
                            LastSeenUtc = date
                        };

                        await this.repo.AddContact(incomingContact);
                        contact = incomingContact;
                    }





                    await this.e2eRatchet.SaveIncomingDynamicPublicKeyOnSuccessfulDecryptionAsync(contact.Id, xmessage.DynamicPublicKey,
                        xmessage.DynamicPublicKeyId);

                    // add metadata to message
                    decryptedMessage.SenderId = contact.Id;
                    decryptedMessage.MessageType = initialMessageMetadata.MessageType;
                    decryptedMessage.SenderLocalMessageId = initialMessageMetadata.SenderLocalMessageId.ToString();

                    if (decryptedMessage.MessageType.IsContent())
                    {
                        var success = true;

                        GetE2EDecryptionKeyResult getE2EDecryptionKeyResult = await this.e2eRatchet.GetEndToEndDecryptionKeyAsync(contact.Id, xmessage.DynamicPublicKey, xmessage.PrivateKeyHint);

                        KeyMaterial64 keyMaterial64 = getE2EDecryptionKeyResult.E2EDecryptionKeyMaterial;
                        await this.e2eRatchet.GetEndToEndDecryptionKeyAsync(contact.Id, xmessage.DynamicPublicKey, xmessage.PrivateKeyHint);

                        if (decryptedMessage.MessageType == MessageType.Text || decryptedMessage.MessageType == MessageType.TextAndMedia)
                        {
                            // if the message has text, decrypt all text
                            var decodeTextResponse = this.ixdsCryptoService.BinaryDecodeXDSSec(xmessage.TextCipher, null);
                            if (!decodeTextResponse.IsSuccess)
                                success = false; // something is wrong, should have worked
                            else
                            {
                                var decrpytTextResponse = this.ixdsCryptoService.Decrypt(decodeTextResponse.Result, keyMaterial64, null);
                                if (!decrpytTextResponse.IsSuccess)
                                    success = false; // something is wrong, should have worked
                                else
                                    decryptedMessage.ThreadText = decrpytTextResponse.Result.Text;
                            }

                        }

                        if (decryptedMessage.MessageType == MessageType.Media || decryptedMessage.MessageType == MessageType.TextAndMedia)
                        {
                            // if the message has image content, decrypt the image content
                            var decodeImageResponse = this.ixdsCryptoService.BinaryDecodeXDSSec(xmessage.ImageCipher, null);
                            if (!decodeImageResponse.IsSuccess)
                                success = false; // something is wrong, should have worked
                            else
                            {
                                var decrpytImageResponse = this.ixdsCryptoService.BinaryDecrypt(decodeImageResponse.Result, keyMaterial64, null);
                                if (!decrpytImageResponse.IsSuccess)
                                    success = false; // something is wrong, should have worked
                                else
                                    decryptedMessage.ThreadMedia = decrpytImageResponse.Result.GetBytes();
                            }

                        }

                        if (success)
                        {
                            decryptedMessage.EncryptedE2EEncryptionKey = this.ixdsCryptoService.DefaultEncrypt(keyMaterial64.GetBytes(), this.ixdsCryptoService.SymmetricKeyRepository.GetMasterRandomKey());
                            decryptedMessage.LocalMessageState = LocalMessageState.Integrated;
                            await this.contactListManager.ChatWorker_ContactUpdateReceived(null, contact.Id);
                            await this.repo.AddMessage(decryptedMessage);
                            return decryptedMessage;
                        }
                        else
                        {
                            if (isIncomingContact)
                            {
                                await this.repo.DeleteContacts(new[] { contact.Id }); // delete the just incoming contact if the was an error anyway.
                            }

                            return null;
                        }

                    }
                    else
                        return null;

                }

                // nope, resend request (I hope we don't loop here!)
                string networkPayloadHash = NetworkPayloadHash.ComputeAsGuidString(xmessage.SerializedPayload);
                if (!this._resendsRequested.Contains(networkPayloadHash))
                {
                    var response = await this.chatClient.UploadResendRequest(new XResendRequest { Id = networkPayloadHash, RecipientId = null });
                    if (response.IsSuccess)
                    {
                        this._resendsRequested.Add(networkPayloadHash);
                    }
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }

            return null;
        }

        readonly HashSet<string> _resendsRequested = new HashSet<string>();






        async Task<bool> PublishIdentity()
        {


            try
            {
                var profile = await this.repo.GetProfile();

                var identityToPublish = new XIdentity
                {
                    Id = this.ownChatId,
                    PublicIdentityKey = profile.PublicKey,
                    LastSeenUTC = DateTime.UtcNow
                };

                var response = await this.chatClient.PublishIdentityAsync(identityToPublish);
                if (response.IsSuccess)
                {
                    if (response.Result == this.ownChatId)
                    {
                        profile.IsIdentityPublished = true;
                        Debug.Assert(profile.Id == "1");
                        await this.repo.UpdateProfile(profile);
                        return true;
                    }

                    throw new Exception($"The server did not confirm the identity was published. Server error: {response.Result}"); // SQL Server firewall...?

                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }
            return false;

        }



        async Task TryGetMissingPublicKeysForContactsAsync()
        {
            var contacts = await this.repo.GetAllContacts();

            foreach (var c in contacts.Where(c => c.ContactState == ContactState.Added))
            {
                try
                {
                    await VerifyContactInAddedStateAsync(c);
                }
                catch (Exception e)
                {
                    this.logger.LogDebug($"Error while attempting to download missing public key for contact {c.Id}:{e.Message}.");
                }
            }
        }

        #endregion

        public void ReceiveCheckForMessagesReply(byte isAnyNews)
        {
            this.appState.SetIsMessagesWaiting(isAnyNews > 0);
        }


        internal async Task VerifyContactInAddedStateAsync(Identity addedContact)
        {
            Debug.Assert(addedContact.ContactState == ContactState.Added);
            Debug.Assert(Guid.TryParse(addedContact.Id, out var isGuid));


            var response = await this.chatClient.GetIdentityAsync(addedContact.UnverifiedId);
            if (!response.IsSuccess)
            {
                throw new InvalidOperationException($"ChatClient could not download identity: {response.Error}");
            }

            XIdentity contactAdded = response.Result;
            if (contactAdded.ContactState == ContactState.Valid)
            {
                // It's on the server makeked as Valid, double check this
                if (contactAdded?.Id != null && contactAdded.PublicIdentityKey != null &&
                    ChatId.GenerateChatId(contactAdded.PublicIdentityKey) == contactAdded.Id)
                {
                    await this.repo.UpdateAddedContactWithPublicKey(contactAdded,
                        Guid.Parse(addedContact.Id).ToString());
                    await this.contactListManager.ChatWorker_ContactUpdateReceived(null, addedContact.Id);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Mismatch of id and public key in downloaded identity for added contact with unverified id {addedContact.UnverifiedId} received id {contactAdded.Id} and state '{contactAdded.ContactState}'. Expected state was 'Valid' and that the ids match.");
                }
            }
            else if (contactAdded.ContactState == ContactState.NonExistent)
            {
                this.logger.LogInformation($"Added contact with unverified Id {addedContact.UnverifiedId} did not exist on the node that was queried.");
            }
            else
            {
                throw new InvalidOperationException(
                    $"Downloaded identity for added contact with unverified id {addedContact.UnverifiedId} has id {contactAdded.Id} and state '{contactAdded.ContactState}'. Expected state was 'Valid' and that the ids match.");
            }
        }

        readonly ConcurrentQueue<Message> _readReceipts = new ConcurrentQueue<Message>();

        public async Task SendReceipt(Message messageToSendReceiptFor, MessageType receiptType)
        {


            try
            {
                // we could use the hash to tell the network it can delete the delivered message now.
                var deliveryReceipt = new Message
                {
                    RecipientId = messageToSendReceiptFor.SenderId,
                    MessageType = receiptType,
                    SenderLocalMessageId = messageToSendReceiptFor.SenderLocalMessageId
                };

                Debug.Assert(deliveryReceipt.MessageType.IsReceipt());
                Debug.Assert(Guid.TryParse(deliveryReceipt.RecipientId, out Guid dummy1));
                Debug.Assert(deliveryReceipt.SenderLocalMessageId.Length == 6 && int.TryParse(deliveryReceipt.SenderLocalMessageId, out int dummy2));

                await this.chatEncryptionService.EncryptMessage(deliveryReceipt);

                if (deliveryReceipt.MessageType == MessageType.ReadReceipt)
                    this._readReceipts.Enqueue(deliveryReceipt);
                else
                    await this.chatClient.UploadMessage(deliveryReceipt);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }
        }

        async Task SendReadReceipts()
        {


            try
            {
                while (this._readReceipts.TryDequeue(out var readReceipt))
                {
                    await this.chatClient.UploadMessage(readReceipt);
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }

        }
    }
}
