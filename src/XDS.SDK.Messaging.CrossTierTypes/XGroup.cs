using System;
using XDS.SDK.Core.Peer;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public class XGroup : IPeerIdentity, IId
    {
        // Defining properties of the IPeerIdentity. The PrivateKey determines everything else,
        // according to the spec of the PeerIdentityType.
        public PeerIdentityType PeerIdentityType { get; set; }
        public byte[] PrivateKey { get; set; }
        public byte[] PublicKey { get; set; }
        public byte[] PublicKeyHash { get; set; }
        public string ShortId { get; set; }

        // Unique Key for local storage
        public string Id { get; set; }

        // More local properties for management of the item
        public string LocalName;
        public byte[] LocalImage;
        public DateTime LocalCreatedDate;
        public DateTime LocalModifiedDate;
        public ContactState LocalContactState;
    }
}
