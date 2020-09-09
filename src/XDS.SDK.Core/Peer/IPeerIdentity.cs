namespace XDS.SDK.Core.Peer
{
    public interface IPeerIdentity
    {
        PeerIdentityType PeerIdentityType { get; set; }

        byte[] PrivateKey { get; set; }

        byte[] PublicKey { get; set; }

        byte[] PublicKeyHash { get; set; }

        string ShortId { get; set; }
    }
}


