using XDS.SDK.Cryptography.Api.Infrastructure;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public static class XIdentityExtensions
    {
        public static bool EqualDeep(this XIdentity id1, XIdentity id2)
        {
            if (ReferenceEquals(id1, id2))
                return true;

            if (id1 == null || id2 == null)
                return false;

            if (id1.Id != id2.Id)
                return false;
           
            if (!ByteArrays.AreAllBytesEqualOrBothNull(id1.PublicIdentityKey, id2.PublicIdentityKey))
                return false;
         
            return true;
        }

        public static byte[] SerializeXIdentity(this XIdentity xIdentity)
        {
            byte[] serialized = PocoSerializer.Begin()
                .Append(xIdentity.Id)
                .Append(xIdentity.PublicIdentityKey)
                .Append(xIdentity.FirstSeenUTC)
                .Append(xIdentity.LastSeenUTC)
                .Append((byte)xIdentity.ContactState)
                .Finish();
            return serialized;
        }

        public static XIdentity DeserializeXIdentityCore(this byte[] messagePart)
        {
            var xIdentity = new XIdentity();

            var ser = PocoSerializer.GetDeserializer(messagePart);

            xIdentity.Id = ser.MakeString(0);
            xIdentity.PublicIdentityKey = ser.MakeByteArray(1);
            xIdentity.FirstSeenUTC = ser.MakeDateTime(2);
            xIdentity.LastSeenUTC = ser.MakeDateTime(3);
            xIdentity.ContactState = (ContactState)ser.MakeByte(4);
           
            return xIdentity;
        }
    }
}
