using System;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public class XIdentity : IId
    {
		/// <summary>
		/// Id is the ChatId
		/// </summary>
        public string Id { get; set; }

        public string Name;
        public byte[] Image;
        public DateTime LastSeenUTC;
        public DateTime FirstSeenUTC;
        public string ImagePath;
        public byte[] PublicIdentityKey;
        public ContactState ContactState;

        public override bool Equals(object obj)
        {
            XIdentity p = obj as XIdentity;
            return p != null && p.EqualDeep(this);
        }

        public bool Equals(XIdentity p)
        {
            return p != null && p.EqualDeep(this);
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}

