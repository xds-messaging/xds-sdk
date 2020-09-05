using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Infrastructure;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;
using XDS.Messaging.SDK.ApplicationBehavior.Workers;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
	public class ContactListManager : NotifyPropertyChanged
    {
        readonly ILogger logger;
        readonly IDependencyInjection dependencyInjection;
		readonly AppRepository _repo;
		readonly IChatEncryptionService _chatEncryptionService;
		readonly NotificationManager _notificationManager;
		readonly UnreadManager _unreadManager;
		readonly IDispatcher _dispatcher;
		readonly IFileService _fileService;

		public bool _isInitialized;

		public ContactListManager(IDependencyInjection dependencyInjection, ILoggerFactory loggerFactory, AppRepository appRepository, IFileService fileService, IChatEncryptionService chatEncryptionService, IDispatcher dispatcher, NotificationManager notificationManager, UnreadManager unreadManager)
        {
            this.logger = loggerFactory.CreateLogger<ContactListManager>();
			this._repo = appRepository;
            this._fileService = fileService;
            this._chatEncryptionService = chatEncryptionService;
            this._dispatcher = dispatcher;
            this._notificationManager = notificationManager;
            this._unreadManager = unreadManager;
            this.dependencyInjection = dependencyInjection;



        }

		// https://stackoverflow.com/questions/19112922/sort-observablecollectionstring-c-sharp
		public ObservableCollection<Contact> Contacts { get; } = new ObservableCollection<Contact>();
		public ObservableCollection<Contact> Conversations { get; } = new ObservableCollection<Contact>();

		public async Task HandelUnreadAndChatPreviewForIncomingMessageFromChatWorker(Message message, bool? countAsUnread)
		{
			Debug.Assert(message.LocalMessageState == LocalMessageState.Integrated);

			string chatPreview = await GetChatPreview(message);

			await this._dispatcher.RunAsync(async () =>
			{
				var contactId = message.SenderId;
				var contact = this.Contacts.Single(c => c.Id == contactId);
				await UpdateChatPreview(contact, message, chatPreview);

				this._unreadManager.AddUnread(message);

				if (this.Conversations.SingleOrDefault(c => c.Id == contactId) == null)
					this.Conversations.Add(contact);

				var currentContact = this._currentContact;
				SortByDate(this.Conversations);
				this.CurrentContact = currentContact;

				if (currentContact == null || message.SenderId != this.CurrentContact?.Id)
				{
					await this._notificationManager.NotifyMessageReceived(message);
				}

			});

		}

		public async Task ChatWorker_ContactUpdateReceived(object sender, string contactId)
		{
			await this._dispatcher.RunAsync(async () =>
			{
				await AddOrUpdateContactFromIdentity(contactId);
			});
		}

		async Task AddOrUpdateContactFromIdentity(string contactId)
		{
			Identity updatedContact = await this._repo.GetContact(contactId);

			Contact contact = IdentityToContact(updatedContact);
			await LoadLastMessageFromStoreAndAddInfoToContactButDontCount(contact, updatedContact);

			Contact existing = this.Contacts.SingleOrDefault(c => c.Id == contactId);
			if (existing == null)
			{
				this.Contacts.Add(contact);
				OnPropertyChanged(nameof(this.Contacts));

				if (contact.HasMessages)
				{
					this.Conversations.Add(contact);
					OnPropertyChanged(nameof(this.Conversations));
				}
			}
			else
			{
				existing.ChatPreview = contact.ChatPreview;
				existing.ContactState = contact.ContactState;
				existing.ContactStateText = contact.ContactStateText;
				existing.HasMessages = contact.HasMessages;
				existing.UnreadMessages = contact.UnreadMessages;
				existing.LastMessageDateLocalTime = contact.LastMessageDateLocalTime;
				existing.Name = contact.Name;
				existing.StaticPublicKey = contact.StaticPublicKey;
			}
		}

		/// <summary>
		/// Updates the conversation with an outgoing message text/description in SendMessageState.Created.
		/// If the contact has no conversation yet, the contact must be inserted into the conversation.
		/// Also, the arrow must be set to 
		/// </summary>
		/// <param name="message"></param>
		public async Task AddOutgoingMessageToConversation(Message message)
		{
			var contactId = message.RecipientId;
			var contact = this.Contacts.Single(c => c.Id == contactId);
			contact.LastMessageSideIsMe = true;
			contact.ChatPreview = await GetChatPreview(message);
			contact.LastMessageDateLocalTime = message.GetEncrytedDateUtc().ToLocalTime();
			contact.SendMessageState = message.SendMessageState;

			if (this.Conversations.SingleOrDefault(c => c.Id == contactId) == null)
			{
				this.Conversations.Insert(0, contact); // when there is no conv, insert at top and select it
				this.CurrentContact = contact; // then a reselect is necessary
			}
			else
			{
				if (this.Conversations[0].Id != contactId)
				{
					SortByDate(this.Conversations);
					this.CurrentContact = contact; // then a reselect is necessary
				} // else: nothing to do, all convs in the right place
			}
		}



		/// <summary>
		/// Uses the SendMessageState in the message to update the SendMessageState in the conversation,
		/// assuming the message text/description is already in the conversation.
		/// </summary>
		/// <param name="message"></param>
		public void UpdateOutgoingMessageStateInConversation(Message message)
		{
			var contact = this.Contacts.Single(c => c.Id == message.RecipientId);
			contact.SendMessageState = message.SendMessageState;
		}




		public Contact CurrentContact
		{
			get => this._currentContact;
			set => Set(ref this._currentContact, value);
		}
		Contact _currentContact;

		public bool CanChat(Contact contact)
		{
			return contact != null && (contact.ContactState == ContactState.Valid);
		}

		public async Task InitFromStore()
		{
			if (this._isInitialized)
				return;

            //chatWorker.ContactUpdateReceived += async (sender, contactId) => await ChatWorker_ContactUpdateReceived(sender, contactId);
			this.dependencyInjection.ServiceProvider.Get<ChatWorker>().SendMessageStateUpdated += (sender, message) => UpdateOutgoingMessageStateInConversation(message);
			await UpdateContacts();
            this._isInitialized = true;
        }

		public async Task UpdateContacts()
		{
			// TODO: refactor this so that the contact lists gets filled incrementally.



			var identities = await this._repo.GetAllContacts();
			var contacts = new List<Contact>();
			foreach (Identity i in identities)
			{
				Contact contact = IdentityToContact(i);
				await LoadLastMessageFromStoreAndAddInfoToContactButDontCount(contact, i);
				contacts.Add(contact);
			}

			var byName = contacts.OrderBy(c => c.Name);

			this.Contacts.Clear();

			foreach (var c in byName)
			{
				this.Contacts.Add(c);
			}
			OnPropertyChanged(nameof(this.Contacts));

			this.Conversations.Clear();

			var byLastMessageDate = contacts.Where(c => c.HasMessages).OrderByDescending(c => c.LastMessageDateLocalTime);
			foreach (var c in byLastMessageDate)
			{
				this.Conversations.Add(c);
			}

			await this._unreadManager.RebuildStatsDeep(this.Contacts, this.Conversations);

		}

		async Task LoadLastMessageFromStoreAndAddInfoToContactButDontCount(Contact contact, Identity identity)
		{
			if (identity.ContactState != ContactState.Valid)
				return;

			Message lastMessage = await this._repo.GetLastMessage(identity.Id);
			if (lastMessage == null)
				return;
			await UpdateChatPreview(contact, lastMessage, null);

		}

		public async Task<string> GetChatPreview(Message lastMessage)
		{
			string chatPreview = "Error";

			if (lastMessage.MessageType != MessageType.Text)
				chatPreview = lastMessage.MessageType.ToString();
			else
			{
				if (lastMessage.ThreadText != null) // no decryption required, e.g. just sending.
					chatPreview = lastMessage.ThreadText;
				else
				{
					if (lastMessage.LocalMessageState == LocalMessageState.JustReceived) // unread incoming
					{
						var response = await this._chatEncryptionService.PeekIntoUnreadTextMessageWithoutSideEffects(lastMessage);
						if (response.IsSuccess)
						{
							chatPreview = response.Result;
						}
					}
					else
					{
						var response = await this._chatEncryptionService.DecryptCipherTextInVisibleBubble(lastMessage);
						if (response.IsSuccess)
						{
							chatPreview = lastMessage.ThreadText;
						}
					}
				}
			}

			return chatPreview;
		}

		async Task UpdateChatPreview(Contact contact, Message lastMessage, string chatPreviewFromChatWorker)
		{
			if (chatPreviewFromChatWorker == null)
				contact.ChatPreview = await GetChatPreview(lastMessage);
			else
				contact.ChatPreview = chatPreviewFromChatWorker;

			contact.HasMessages = true;
			contact.LastMessageDateLocalTime = lastMessage.GetEncrytedDateUtc().ToLocalTime();
			contact.LastMessageSideIsMe = lastMessage.Side == MessageSide.Me;
			contact.IsLastMessageUnread = lastMessage.Side == MessageSide.You &&
										   lastMessage.LocalMessageState == LocalMessageState.JustReceived;
			contact.SendMessageState = lastMessage.SendMessageState;

		}

		Contact IdentityToContact(Identity identity)
		{
			var contact = new Contact
			{
				Id = identity.Id,
				Name = identity.Name,
				ContactState = identity.ContactState,
				ContactStateText = ContactStateToText(identity.ContactState),
				PictureBytes = identity.Image,
				StaticPublicKey = identity.StaticPublicKey,
				UnverfiedId = identity.UnverifiedId
			};
			if (contact.PictureBytes == null)
				contact.PictureBytes = this._fileService.LoadAssetImageBytesAsync("Profile.png").GetAwaiter().GetResult();
			return contact;
		}

		string ContactStateToText(ContactState contactState)
		{
			switch (contactState)
			{
				case ContactState.Added:
					return "Just added";
				case ContactState.Valid:
					return "Valid";
				default:
					return "ERROR";
			}
		}



		internal void RemoveContactsAndConversations(string[] contactIds)
		{
			foreach (var id in contactIds)
			{
				var contactToRemove = this.Contacts.SingleOrDefault(c => c.Id == id);
				if (contactToRemove != null)
					this.Contacts.Remove(contactToRemove);
				var conversationToToRemove = this.Conversations.SingleOrDefault(c => c.Id == id);
				if (conversationToToRemove != null)
					this.Conversations.Remove(conversationToToRemove);
			}
		}

		static void SortByDate(ObservableCollection<Contact> collection)
		{
			var sorted = collection.OrderByDescending(x => x.LastMessageDateLocalTime).ToArray();

			for (int i = 0; i < sorted.Length; i++)
			{
				var oldIndex = collection.IndexOf(sorted[i]);
				if (oldIndex != i)
					collection.Move(oldIndex, i);
			}
		}
	}




}
