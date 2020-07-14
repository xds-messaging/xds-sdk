using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Infrastructure;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
	public class UnreadManager : NotifyPropertyChanged
	{
		readonly Dictionary<string, int> _unreadByUser = new Dictionary<string, int>();
		readonly AppRepository _appRepository;

		IList<Contact> _contactsCollectionReference;
		IList<Contact> _conversationCollectionReference;

		public UnreadManager(AppRepository appRepository)
		{
			this._appRepository = appRepository;
		}

		public UnreadMessages Totals
		{
			get => this._unreadMessages;
			private set => Set(ref this._unreadMessages, value);
		}
		UnreadMessages _unreadMessages;
		

		public async Task RebuildStatsDeep(IList<Contact> contacts, IList<Contact> conversations)
		{
			this._contactsCollectionReference = contacts;
			this._conversationCollectionReference = conversations;

			foreach (var c in contacts)
			{
				if (c.HasMessages && c.IsLastMessageUnread)
				{
					int totalUnread = await SearchForMoreUnreadMessage(c.Id);
					c.UnreadMessages = totalUnread;
					this._unreadByUser.Add(c.Id, totalUnread);
				}
			}
			UpdateTotals();
		}

		

		async Task<int> SearchForMoreUnreadMessage(string contactId)
		{
			var unread = 1;
			var firstIndex = 1u;
			const int batch = 5;
			var done = false;

			search:
			var messages = await this._appRepository.GetMessageRange(firstIndex, batch, contactId);

			for (var index = messages.Count - 1; index >= 0; index--)
			{
				var m = messages[index];
				if (m.LocalMessageState == LocalMessageState.JustReceived)
					unread += 1;
				else
				{
					done = true;
					break;
				}
			}
			if (done || messages.Count < batch)
				return unread;
			firstIndex += batch;
			goto search;
		}

		public void NotifyRead(Message message)
		{
			var contactId = message.SenderId;
			var contact = this._contactsCollectionReference.Single(c => c.Id == contactId);

			contact.UnreadMessages -= 1;
			if (contact.UnreadMessages == 0)
				contact.IsLastMessageUnread = false;

			if (this._unreadByUser[contactId] == 1)
				this._unreadByUser.Remove(contactId);
			else
				this._unreadByUser[contactId] -= 1;
			UpdateTotals();
		}

		public void NotifyAllDeleted(string contactId)
		{
			var contact = this._contactsCollectionReference.Single(c => c.Id == contactId);
			contact.UnreadMessages = 0;
			contact.HasMessages = false;
			contact.IsLastMessageUnread = false;
			contact.LastMessageSideIsMe = true;
			contact.SendMessageState = SendMessageState.None;
			this._conversationCollectionReference.Remove(contact);
		}

		public void AddUnread(Message message)
		{
			var contactId = message.SenderId;
			var contact = this._contactsCollectionReference.Single(c => c.Id == contactId);

			contact.UnreadMessages += 1;

			if(this._unreadByUser.ContainsKey(contactId))
				this._unreadByUser[contactId] += 1;
			else
			{
				this._unreadByUser.Add(contactId,1);
			}
			UpdateTotals();
		}

		void UpdateTotals()
		{
			this.Totals = new UnreadMessages(this._unreadByUser.Values.Sum(), this._unreadByUser.Count); 
		}

		public struct UnreadMessages
		{
			public readonly int Total;
			public readonly int ByContacts;
			public UnreadMessages(int total, int byContacts)
			{
				this.Total = total;
				this.ByContacts = byContacts;
			}
		}
	}
}
