using System;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Chat
{
    public sealed class MessageDescriptor
    {
        const int MessageIdLenght = 6; // e.g. 999998
        public readonly string MessageId;
        public readonly string RecipientId;
        readonly string _senderId;

        public MessageDescriptor(string messageId, string senderId, string recipientId)
        {
            if (messageId == null || senderId == null || recipientId == null
                || messageId.Length != MessageIdLenght || senderId.Length != 10 || recipientId.Length != 10)
                throw new ArgumentException(nameof(MessageDescriptor));
            this.MessageId = messageId;
            this._senderId = senderId;
            this.RecipientId = recipientId;
        }

        public bool BelongsToThread(string contactId, string profileId)
        {
            return this.RecipientId == profileId && this._senderId == contactId ||
                   this.RecipientId == contactId && this._senderId == profileId;
        }

        public Message ToMessage()
        {
            return new Message { Id = this.MessageId, SenderId = this._senderId, RecipientId = this.RecipientId };
        }
    }
}
