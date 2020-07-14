using System;
using XDS.SDK.Cryptography.E2E;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Serialization
{
    public static class E2EUserSerializer
    {
        public static byte[] Serialize(E2EUser u)
        {
            byte[] serialized = PocoSerializer.Begin()
                //.Append(m.UserId) // Ignore
                //.Append(m.StaticPublicKey) // Ignore
                .Append(u.LatestDynamicPublicKey)
                .Append(u.LatestDynamicPublicKeyId)
                .Append(u.AuthSecret)
                .Append(u.DynamicPrivateDecryptionKeys)
                .Finish();
            return serialized;
        }

        public static E2EUser Deserialize(byte[] serializedE2EUser)
        {
            if (serializedE2EUser == null)
                return null;

            try
            {
                var u = new E2EUser();
                var ser = PocoSerializer.GetDeserializer(serializedE2EUser);
                // not setting UserId, StaticPublicKey here!
                u.LatestDynamicPublicKey = ser.MakeByteArray(0);
                u.LatestDynamicPublicKeyId = ser.MakeInt64(1);
                u.AuthSecret = ser.MakeByteArray(2);
                u.DynamicPrivateDecryptionKeys = ser.MakeDictLongByteArray(3);

                return u;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
