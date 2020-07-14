using System.Collections.Generic;
using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat.MessageCollection.Framework;

namespace XDS.Messaging.SDK.ApplicationBehavior.ViewModels
{
	public class MessagesLoader
	{
		readonly AppRepository _repo;
		readonly string _contactGuid;

		public MessagesLoader(AppRepository appRepository, string contactGuid)
		{
			this._repo = appRepository;
			this._contactGuid = contactGuid;
		}

		public async Task<IReadOnlyCollection<Message>> LoadMessageRangeAsync(ItemIndexRange itemIndexRange)
		{
			var messages = await GetMessages(itemIndexRange.FirstIndex, itemIndexRange.Length);
			if (messages == null)
				return await LoadMessageRangeAsync(itemIndexRange);
			return messages;
		}

		async Task<IReadOnlyList<Message>> GetMessages(int firstIndex, int max)
		{
			IReadOnlyList<Message> queryResult;
			checked
			{
				queryResult = await this._repo.GetMessageRange((uint)firstIndex, (uint)max, this._contactGuid);
			}

			var isThreadReloadRequired = false;
			foreach (var message in queryResult)
			{
				// Validate everything what is important
				if (IsNotCorrupted(message))
					continue;
				await this._repo.DeleteMessage(message.Id, this._contactGuid);
				isThreadReloadRequired = true;
			}
			if (isThreadReloadRequired)
				return null;

			return queryResult;
		}

		public async Task<int> GetMessagesCount()
		{
			return (int)await this._repo.GetMessageCount(this._contactGuid);
		}

		bool IsNotCorrupted(Message message)
		{
			if (string.IsNullOrWhiteSpace(message.SenderId))
				return false;
			if (string.IsNullOrWhiteSpace(message.RecipientId))
				return false;
			if (message.SendMessageState == SendMessageState.None && message.Side != MessageSide.You)
				return false;
			return true;
		}

		public async Task DeleteMessage(string messageId, string contactId)
		{
			await this._repo.DeleteMessage(messageId, contactId);
		}

		public async Task DeleteAllMessages(string contactId)
		{
			await this._repo.DeleteAllMessages(contactId);
		}
	}
}
