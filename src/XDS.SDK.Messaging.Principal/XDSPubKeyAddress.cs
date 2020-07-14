namespace XDS.SDK.Messaging.Principal
{
    /// <summary>
    /// A lightweight container for an XDS address.
    /// </summary>
    public class XDSPubKeyAddress
    {
        public byte[] PrivateKey { get; set; }

        public byte[] PublicKey { get; set; }

        public byte[] Hash { get; set; }

        public string Address { get; set; }

        public byte[] ScriptPubKey { get; set; }

        public string KeyPath { get; set; }
    }
}
