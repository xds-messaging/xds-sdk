using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Infrastructure;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat.MessageCollection.Framework;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;
using XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations;
using XDS.Messaging.SDK.ApplicationBehavior.Workers;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.ViewModels
{
	public interface IChatView
	{
		Task OnThreadLoaded(IReadOnlyCollection<Message> messages);
		Task OnMessageAddedAsync(Message message);
		Task OnMessageEncrypted(Message message);
		Task OnMessageDecrypted(Message message);
		void UpdateSendMessageStateFromBackgroundThread(Message message);
	}

	public class MessagesViewModel : NotifyPropertyChanged
    {
        readonly ILogger logger;
		readonly IChatView _messageThreadView;
		readonly string _contactId;
		readonly MessagesLoader _messagesLoader;

		readonly IChatEncryptionService _encryptionService;
		readonly UnreadManager _unreadManager;
		readonly ContactListManager _contactListManager;
		readonly AppRepository _repo;
		readonly IChatClient _chatClient;
		readonly ChatWorker _chatWorker; // this is only for connection management, not nice

		const int Batch = 10;

		ItemIndexRange _currentItemIndexRange;

		int _messagesOffset;

		public MessagesViewModel(IServiceProvider provider, string contactGuid, IChatView messageThreadView)
		{
			this._contactId = contactGuid;
            this.logger = provider.Get<ILoggerFactory>().CreateLogger<MessagesViewModel>();
			this._repo = provider.Get<AppRepository>();
			this._messagesLoader = new MessagesLoader(this._repo, contactGuid);
			this._messageThreadView = messageThreadView;

			this._encryptionService = provider.Get<IChatEncryptionService>();
			this._unreadManager = provider.Get<UnreadManager>();
			this._contactListManager = provider.Get<ContactListManager>();

			this._chatClient = provider.Get<IChatClient>();
			this._chatWorker = provider.Get<ChatWorker>();
			this._chatWorker.SendMessageStateUpdated += (sender, message) => this._messageThreadView.UpdateSendMessageStateFromBackgroundThread(message);
			this._chatWorker.IncomingMessageDecrypted += async (sender, message) => await AddIncomingDecryptedMessageFromChatworker(message);
		}



		public async Task InitializeThread()
		{
			this._messagesOffset = 0;

			this._currentItemIndexRange = new ItemIndexRange(0, Batch); // For range calculation on collections with normal order, see code before 10/31/16. Also mind this requires reverse reading.

			var messages = await this._messagesLoader.LoadMessageRangeAsync(this._currentItemIndexRange);

			this._currentItemIndexRange = new ItemIndexRange(0, messages.Count);

			await this._messageThreadView.OnThreadLoaded(messages);

			foreach (var message in messages)
			{
				var isUnread = message.LocalMessageState == LocalMessageState.JustReceived;
				await this._encryptionService.DecryptCipherTextInVisibleBubble(message);
				var hasNowBeenReadOrCouldNotBeDecrypted = isUnread && (message.LocalMessageState == LocalMessageState.Integrated || message.LocalMessageState == LocalMessageState.RatchetMismatchError);
				if (hasNowBeenReadOrCouldNotBeDecrypted)
					this._unreadManager.NotifyRead(message);

				if (message.LocalMessageState == LocalMessageState.RatchetMismatchError)
					message.ThreadText = "ERROR - You have received message we can't decrypt, probably because you deleted this contact. You can ask this contact to resend his message if you want to. Your contact sees this message as 'delivered', but not as 'read'.";

				await this._messageThreadView.OnMessageDecrypted(message);
			}
		}

		public async Task<bool> CanLoadNewMessagesBatch()
		{
			var count = await this._messagesLoader.GetMessagesCount();
			return count > this._messagesOffset;
		}

		public async Task LoadNewMessagesBatch()
		{

			this._messagesOffset += Batch;
			try
			{
				this._currentItemIndexRange = new ItemIndexRange(this._messagesOffset, Batch); // For range calculation on collections with normal order, see code before 10/31/16. Also mind this requires reverse reading.
				var messages = await this._messagesLoader.LoadMessageRangeAsync(this._currentItemIndexRange);

				foreach (var message in messages)
				{
					await this._encryptionService.DecryptCipherTextInVisibleBubble(message);
					await this._messageThreadView.OnMessageDecrypted(message);
				}
			}
			catch (Exception e)
			{
				this.logger.LogError(e.Message);
				this.logger.LogError(e.Message);
			}
		}

		async Task AddIncomingDecryptedMessageFromChatworker(Message incomingDecryptedMessage)
		{
			

			Debug.Assert(this._contactListManager.CurrentContact != null && incomingDecryptedMessage.SenderId == this._contactListManager.CurrentContact.Id, "Calling code must ensure this.");

			try
			{

				await this._messageThreadView.OnMessageAddedAsync(incomingDecryptedMessage);

				this._currentItemIndexRange = new ItemIndexRange(this._currentItemIndexRange.FirstIndex, this._currentItemIndexRange.Length + 1);

				await this._messageThreadView.OnMessageDecrypted(incomingDecryptedMessage);
			}
			catch (Exception e)
			{
				this.logger.LogError(e.Message);
			}
		}


		async Task AddNewSendMessageToThreadBeforeEncryption(Message message)
		{
			await this._messageThreadView.OnMessageAddedAsync(message);

			this._currentItemIndexRange = new ItemIndexRange(this._currentItemIndexRange.FirstIndex, this._currentItemIndexRange.Length + 1);
		}


		async Task UpdateMessageInThreadToEncryptedState(Message message)
		{
			await this._messageThreadView.OnMessageEncrypted(message);
		}



		public async Task<IReadOnlyCollection<Message>> OnViewMoreMessagesRequestedAsync()
		{
			var additionalRange = new ItemIndexRange(this._currentItemIndexRange.FirstIndex + this._currentItemIndexRange.Length, Batch);

			var additionalMessages = await this._messagesLoader.LoadMessageRangeAsync(additionalRange);
			if (additionalMessages == null)
				return await OnViewMoreMessagesRequestedAsync();
			// make the sum of current and ACTUALLY received messages
			this._currentItemIndexRange = new ItemIndexRange(0, this._currentItemIndexRange.Length + additionalMessages.Count);

			this.logger.LogDebug($"{additionalMessages.Count} more Messages Loaded");
			// DECRYPTION
			foreach (var message in additionalMessages)
			{
				await this._encryptionService.DecryptCipherTextInVisibleBubble(message);
				await this._messageThreadView.OnMessageDecrypted(message);
			}
			return additionalMessages;
		}


		public async Task DeleteMessageAndReloadThread(string messageId)
		{
			Debug.Assert(this._contactId == this._contactListManager.CurrentContact.Id);
			await this._messagesLoader.DeleteMessage(messageId, this._contactId);
			// it's almost impossible that a single deleted message was unread,
			// so we need not call UnreadManager.NotifyRead.
			await InitializeThread();
		}

		public async Task DeleteAllMessages()
		{
			Debug.Assert(this._contactId == this._contactListManager.CurrentContact.Id);
			await this._messagesLoader.DeleteAllMessages(this._contactId);
			// it's almost impossible that a single deleted message was unread,
			// so we need not call UnreadManager.NotifyRead.
			await InitializeThread();
			this._unreadManager.NotifyAllDeleted(this._contactId);
		}

		public async Task SendMessage(MessageType type, string text, byte[] data = null)
		{
			

			try
			{
				if (this._contactListManager.CurrentContact == null)
					return;

				var message = new Message
				{
					Id = null,
					SenderId = ProfileViewModel.ProfileFStoreId,
					RecipientId = this._contactListManager.CurrentContact.Id,
					ThreadText = null,
					SendMessageState = SendMessageState.Created,
					Side = MessageSide.Me,
					MessageType = type
				};

				await this._repo.AddMessage(message);

				message.ThreadText = text;
				message.ThreadMedia = data;


				await AddNewSendMessageToThreadBeforeEncryption(message);
				await this._contactListManager.AddOutgoingMessageToConversation(message);

				var response = await this._encryptionService.EncryptMessage(message);
				if (!response.IsSuccess)
					throw new Exception(response.Error); // want to we actually want do if this happens?
				message.SendMessageState = SendMessageState.Encrypted;
				await this._repo.UpdateMessage(message);
				await UpdateMessageInThreadToEncryptedState(message);
				this._contactListManager.UpdateOutgoingMessageStateInConversation(message);

				await Task.Run(() => Send_MessageAsyncNew(message));
			}
			catch (Exception e)
			{
				throw new Exception("Error sending: \r\n\r\n" + e, e);
			}
		}

		async Task Send_MessageAsyncNew(Message message, int failCount = 0)
		{
			if (failCount == 0)
			{
				message.SendMessageState = SendMessageState.Sending;
				await this._repo.UpdateMessage(message);
				this._messageThreadView.UpdateSendMessageStateFromBackgroundThread(message);
			}

			var response = await this._chatClient.UploadMessage(message);
			if (response.IsSuccess)
			{
				message.NetworkPayloadHash = response.Result.NetworkPayloadHash;
				var sendAck = response.Result.NetworkResponse.Split(';');
				if (sendAck.Length == 2 && sendAck[0] == message.DynamicPublicKeyId.ToString() && sendAck[1] == message.DynamicPublicKeyId.ToString())
				{
					message.SendMessageState = SendMessageState.XDSNetwork;
					await this._repo.UpdateMessage(message);
					this._messageThreadView.UpdateSendMessageStateFromBackgroundThread(message);
				}
				else
					throw new ArgumentException("Received invalid SendAck.", nameof(sendAck));
			}
			else
			{
				if (failCount > 0)
				{
					message.SendMessageState = SendMessageState.ErrorSending;
					await this._repo.UpdateMessage(message);
					this._messageThreadView.UpdateSendMessageStateFromBackgroundThread(message);
					throw new Exception(response.Error);
					//return;
				}
				await this._chatWorker.TcpConnectAsync();
				await Send_MessageAsyncNew(message, failCount + 1);
			}
		}
	}
}
