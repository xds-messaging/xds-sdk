using XDS.SDK.Cryptography.Api.Infrastructure;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public static class XMessageExtensions
    {
        public static bool EqualDeep(this XMessage m1, XMessage m2)
        {
            if (ReferenceEquals(m1, m2))
                return true;

            if (m1 == null || m2 == null)
                return false;
            if (m1.Id != m2.Id)
                return false;
          
            if (m1.DynamicPublicKeyId != m2.DynamicPublicKeyId)
                return false;
            if (m1.PrivateKeyHint != m2.PrivateKeyHint)
                return false;

            if (!ByteArrays.AreAllBytesEqualOrBothNull(m1.DynamicPublicKey, m2.DynamicPublicKey))
                return false;

	        if (!ByteArrays.AreAllBytesEqualOrBothNull(m1.MetaCipher, m2.MetaCipher))
		        return false;

			if (!ByteArrays.AreAllBytesEqualOrBothNull(m1.TextCipher, m2.TextCipher))
                return false;

            if (!ByteArrays.AreAllBytesEqualOrBothNull(m1.ImageCipher, m2.ImageCipher))
                return false;

            return true;
        }

       
        public static byte[] SerializeCore(this XMessage m)
        {
            byte[] serialized = PocoSerializer.Begin()
                .Append(m.Id)
                .Append(m.MetaCipher)
                .Append(m.TextCipher)
                .Append(m.ImageCipher)
                .Append(m.DynamicPublicKey)
                .Append(m.DynamicPublicKeyId)
                .Append(m.PrivateKeyHint)
                .Append(m.IsDownloaded) // append this new Property at the end, to avoid breaking compatibility with older code/data
                .Finish();
            return serialized;
        }

       

        public static XMessage DeserializeMessage(this byte[] message)
        {
            var m = new XMessage();

            var ser = PocoSerializer.GetDeserializer(message);

            m.Id = ser.MakeString(0);
            m.MetaCipher = ser.MakeByteArray(1);
            m.TextCipher = ser.MakeByteArray(2);
            m.ImageCipher = ser.MakeByteArray(3);
            m.DynamicPublicKey = ser.MakeByteArray(4);
            m.DynamicPublicKeyId = ser.MakeInt64(5);
            m.PrivateKeyHint = ser.MakeInt64(6);

            try
            {
                m.IsDownloaded = ser.MakeBoolean(7); // backwards compat
            }
            catch
            {
                m.IsDownloaded = false;
            }

	        m.SerializedPayload = message;

            return m;
        }

       
    }
}
