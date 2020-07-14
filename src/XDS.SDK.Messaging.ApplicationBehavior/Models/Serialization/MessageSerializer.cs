using System;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Serialization
{
	public static class MessageSerializer
	{
		public static byte[] SerializeCore(Message m)
		{
			byte[] serialized = PocoSerializer.Begin()
				// XMessage members
				.Append(m.Id)
				.Append(m.SenderId)
				.Append(m.RecipientId)
				.Append((byte)m.MessageType)
				.Append(m.SenderLocalMessageId)
				.Append(m.TextCipher)
				.Append(m.ImageCipher)
				.Append(m.DynamicPublicKey)
				.Append(m.DynamicPublicKeyId)
				.Append(m.PrivateKeyHint)
				// Message members
				.Append(m.NetworkPayloadHash)
				.Append((byte)m.Side)
				.Append((byte)m.PrevSide)
				.Append((byte)m.SendMessageState)
				.Append((byte)m.LocalMessageState)
				.Append(m.EncryptedE2EEncryptionKey)
				// some members are ignored!
				.Append(m.ImageImportPath)
				.Finish();
			return serialized;
		}

		public static Message Deserialize(byte[] serializedMessage)
		{
			if (serializedMessage == null)
				return null;
			var m = new Message();
			try
			{


				var ser = PocoSerializer.GetDeserializer(serializedMessage);

				// XMessage members
				m.Id = ser.MakeString(0);
				m.SenderId = ser.MakeString(1);
				m.RecipientId = ser.MakeString(2);
				m.MessageType = (MessageType)ser.MakeByte(3);
				m.SenderLocalMessageId = ser.MakeString(4);
				m.TextCipher = ser.MakeByteArray(5);
				m.ImageCipher = ser.MakeByteArray(6);
				m.DynamicPublicKey = ser.MakeByteArray(7);
				m.DynamicPublicKeyId = ser.MakeInt64(8);
				m.PrivateKeyHint = ser.MakeInt64(9);
				// Message members
				m.NetworkPayloadHash = ser.MakeString(10);
				m.Side = (MessageSide)ser.MakeByte(11);
				m.PrevSide = (MessageSide)ser.MakeByte(12);
				m.SendMessageState = (SendMessageState)ser.MakeByte(13);
				m.LocalMessageState = (LocalMessageState)ser.MakeByte(14);
				m.EncryptedE2EEncryptionKey = ser.MakeByteArray(15);
				m.ImageImportPath = ser.MakeString(16);

				return m;
			}
			catch (Exception e)
			{
				return m;
			}

		}
	}
}
