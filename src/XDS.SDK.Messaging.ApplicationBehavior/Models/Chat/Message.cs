using System;
using System.Globalization;
using XDS.SDK.Cryptography.Api.Implementations;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Chat
{
	/// <summary>
	/// This class used to inherit from XMessage
	/// </summary>
    public class Message : IId
    {
		

		#region formerly inherited XMessage members

		public string Id { get; set; } // gets encrypted in MetaCipher
	    public MessageType MessageType { get; set; } // gets encrypted in MetaCipher
	    public string SenderLocalMessageId { get; set; } // gets encrypted in MetaCipher

		public string SenderId { get; set; } // the sender ID is only required for authentication
	    public string RecipientId { get; set; }

	    public byte[] MetaCipher { get; set; } // Id, MessageType
		public byte[] TextCipher { get; set; }
	    public byte[] ImageCipher { get; set; }

	    public byte[] DynamicPublicKey { get; set; }
	    public long DynamicPublicKeyId { get; set; }
	    public long PrivateKeyHint { get; set; }

		#endregion

	    public string NetworkPayloadHash { get; set; }
		
        public MessageSide Side { get; set; }
        public MessageSide PrevSide { get; set; }
        public SendMessageState SendMessageState { get; set; }
        public LocalMessageState LocalMessageState { get; set; }
        public byte[] EncryptedE2EEncryptionKey { get; set; }

        // Ignore on Saving
        public string ThreadText { get; set; }
        // Ignore on Saving
        public byte[] ThreadMedia { get; set; }
        // Ignore on Saving
        public string ImageImportPath { get; set; }

		public DateTime GetEncrytedDateUtc()
	    {
			return new DateTime(this.DynamicPublicKeyId, DateTimeKind.Utc);
		}

    }


    public static class Helpers
    {
        public static void SetPreviousSide(this Message messageToSet, Message previousMessage)
        {
            if (messageToSet.Side == MessageSide.NotSet)
                throw new InvalidOperationException();

            if (previousMessage == null) // if there is no previous message, set the inverse as PrevSide, so that the bubbles with arrows appear.
                messageToSet.PrevSide = messageToSet.Side == MessageSide.Me ? MessageSide.You : MessageSide.Me;
            else
                messageToSet.PrevSide = previousMessage.Side == MessageSide.Me ? MessageSide.Me : MessageSide.You;
        }

        public static string GetLocalDateString(this Message message)
        {
			return message.GetEncrytedDateUtc().ToLocalTime().ToString(CultureInfo.CurrentCulture);
        }

        public static string GetDateStringShort(this DateTime dateTime)
        {
            return dateTime.ToLocalTime().ToString(@"dd.MM.yy HH:mm:ss", CultureInfo.CurrentCulture);
        }

        public static string GetXDSSecPreview(this Message message)
        {
            if (message.ImageCipher == null)
                return null;
            return XDSSecFormatter.CreateXDSSecText(message.ImageCipher, 1000, 46).Text;
        }

	    public static bool BelongsToCurrentConverstation(this Message message, string contactId)
	    {
		    if (message.Side == MessageSide.Me && message.RecipientId == contactId)
			    return true;
		    if (message.Side == MessageSide.You && message.SenderId == contactId)
			    return true;
		    return false;
	    }
    }
}
