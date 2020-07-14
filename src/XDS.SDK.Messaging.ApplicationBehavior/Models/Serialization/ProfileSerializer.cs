using System;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Models.Serialization
{
    public static class ProfileSerializer
    {
        public static byte[] SerializeCore(Profile profile)
        {
            byte[] serialized = PocoSerializer.Begin()
                .Append(profile.Id)
                .Append(profile.Name)
                .Append(profile.PublicKey)
                .Append(profile.PrivateKey)
                .Append(profile.IsIdentityPublished)
                .Append(profile.PictureBytes)
               
                .Append(profile.ChatId)
                .Append(profile.MasterKey)
                .Append(profile.DefaultAddressPrivateKey)
                .Append(profile.DefaultAddressPublicKey)
                .Append(profile.DefaultAddressHash)
                .Append(profile.DefaultAddress)
                .Append(profile.DefaultAddressScriptPubKey)
                .Append(profile.DefaultAddressKeyPath)
                .Finish();
            return serialized;
        }

        public static Profile Deserialize(byte[] serializedProfile)
        {
            if (serializedProfile == null)
                return null;
            try
            {
                var profile = new Profile();

                var ser = PocoSerializer.GetDeserializer(serializedProfile);

                profile.Id = ser.MakeString(0);
                profile.Name = ser.MakeString(1);
                profile.PublicKey = ser.MakeByteArray(2);
                profile.PrivateKey = ser.MakeByteArray(3);
                profile.IsIdentityPublished = ser.MakeBoolean(4);
                profile.PictureBytes = ser.MakeByteArray(5);

                profile.ChatId = ser.MakeString(6);
                profile.MasterKey = ser.MakeByteArray(7);
                profile.DefaultAddressPrivateKey = ser.MakeByteArray(8);
                profile.DefaultAddressPublicKey = ser.MakeByteArray(9);
                profile.DefaultAddressHash = ser.MakeByteArray(10);
                profile.DefaultAddress = ser.MakeString(11);
                profile.DefaultAddressScriptPubKey = ser.MakeByteArray(12);
                profile.DefaultAddressKeyPath = ser.MakeString(13);

                return profile;
            }
            catch (Exception)
            {
                return null;
            }

        }
    }
}
