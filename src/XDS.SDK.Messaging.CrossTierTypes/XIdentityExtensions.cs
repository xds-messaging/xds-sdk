using XDS.SDK.Core.Peer;
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

    public static class XGroupExtensions
    {
        public static byte[] SerializeXGroup(this XGroup xGroup)
        {
            byte[] serialized = PocoSerializer.Begin()
                // IID members
                .Append(xGroup.Id)

                // IPeerIdentity members
                .Append((int)xGroup.PeerIdentityType)
                .Append(xGroup.PrivateKey)
                .Append(xGroup.PublicKey)
                .Append(xGroup.PublicKeyHash)
                .Append(xGroup.ShortId)

                // More local properties for management of the item
                .Append(xGroup.LocalName)
                .Append(xGroup.LocalImage)
                .Append(xGroup.LocalCreatedDate)
                .Append(xGroup.LocalModifiedDate)
                .Append((byte)xGroup.LocalContactState)
                .Finish();
            return serialized;
        }

        public static XGroup DeserializeXGroup(this byte[] xGroupBytes)
        {
            var xGroup = new XGroup();

            var ser = PocoSerializer.GetDeserializer(xGroupBytes);

            // IID members
            xGroup.Id = ser.MakeString(0);

            // IPeerIdentity members
            xGroup.PeerIdentityType = (PeerIdentityType)ser.MakeInt32(1);
            xGroup.PrivateKey = ser.MakeByteArray(2);
            xGroup.PublicKey = ser.MakeByteArray(3);
            xGroup.PublicKeyHash = ser.MakeByteArray(4);
            xGroup.ShortId = ser.MakeString(5);

            // More local properties for management of the item
            xGroup.LocalName = ser.MakeString(6);
            xGroup.LocalImage = ser.MakeByteArray(7);
            xGroup.LocalCreatedDate = ser.MakeDateTime(8);
            xGroup.LocalModifiedDate = ser.MakeDateTime(9);
            xGroup.LocalContactState = (ContactState) ser.MakeByte(10);

            return xGroup;
        }
    }
}
