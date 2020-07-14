
namespace XDS.SDK.Messaging.CrossTierTypes
{
    public class XMessage : IId
    {
		/// <summary>
		/// Id is the RecipientId
		/// </summary>
        public string Id { get; set; }

        public byte[] MetaCipher { get; set; } // encrypted XMessageMeta
        public byte[] TextCipher { get; set; }
        public byte[] ImageCipher { get; set; }
        public byte[] DynamicPublicKey { get; set; }
        public long DynamicPublicKeyId { get; set; } // timestamp, not needed for decyption. Only needed for reply.
        public long PrivateKeyHint { get; set; }

		/// <summary>
		/// Gets populated in <see cref="XMessageExtensions.DeserializeMessage"/>.
		/// Network stores the message calculating the <see cref="NetworkPayloadHash"/> from the message,
		/// using the hash as Id.
		/// When a client cannot decrypt a message, it notifies the network by calculating the hash from the 
		/// received message an reporting it to the server again. Then, the message is AGAIN stored
		/// using this id.
		/// The sender can check using this hash/id for the status of the message and resend it if the receiver
		/// has indicated it's necessary.
		/// </summary>
		public byte[] SerializedPayload { get; internal set; }  

		public XMessageMetaData MessageMetaData { get; set; }
	}

	public class XMessageMetaData
	{
		public MessageType MessageType { get; set; }
		public int SenderLocalMessageId { get; set; }
		public byte[] SenderPublicKey { get; set; }
	}

	public static class XMessageMetaDataExtensions
	{
		public static byte[] SerializeCore(this XMessageMetaData m)
		{
			byte[] serialized = PocoSerializer.Begin()
				.Append((byte)m.MessageType)
				.Append(m.SenderLocalMessageId)
				.Append(m.SenderPublicKey)
				.Finish();
			return serialized;
		}



		public static XMessageMetaData DeserializeMessageMetadata(this byte[] messageMetadata)
		{
			var m = new XMessageMetaData();

			var ser = PocoSerializer.GetDeserializer(messageMetadata);

			m.MessageType = (MessageType)ser.MakeByte(0);
			m.SenderLocalMessageId = ser.MakeInt32(1);
			m.SenderPublicKey = ser.MakeByteArray(2);

			return m;
		}
	}
}
