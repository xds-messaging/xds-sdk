using System;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Serialization
{
    public static class IdentitySerializer
    {
        public static byte[] SerializeCore(Identity identity)
        {
            byte[] serialized = PocoSerializer.Begin()
                .Append(identity.Id)
				.Append(identity.UnverifiedId)
                .Append(identity.Name)
                .Append(identity.Image)
                .Append(identity.LastSeenUtc)
                .Append(identity.FirstSeenUtc)
                .Append(identity.StaticPublicKey)
                .Append((byte)identity.ContactState)
                .Append(identity.CryptographicInformation)
                .Finish();
            return serialized;
        }

        public static Identity Deserialize(byte[] serializedIdentity)
        {
            if (serializedIdentity == null)
                return null;
            try
            {
                var identity = new Identity();

                var ser = PocoSerializer.GetDeserializer(serializedIdentity);

                identity.Id = ser.MakeString(0);
				identity.UnverifiedId = ser.MakeString(1);
                identity.Name = ser.MakeString(2);
                identity.Image = ser.MakeByteArray(3);
                identity.LastSeenUtc = ser.MakeDateTime(4);
                identity.FirstSeenUtc = ser.MakeDateTime(5);
                identity.StaticPublicKey = ser.MakeByteArray(6);
                identity.ContactState = (ContactState)ser.MakeByte(7);
                identity.CryptographicInformation = ser.MakeByteArray(8);

                return identity;
            }
            catch (Exception)
            {
                return null;
            }

        }
    }
}
