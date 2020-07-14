using XDS.SDK.Cryptography.ECC;

namespace XDS.SDK.Messaging.Principal
{
    /// <summary>
    /// A lightweight container for the defining artifacts that constitute an XDS principal.
    /// </summary>
    public class XDSPrincipal
    {
        public const int XDSCoinType = 15118976;

        /// <summary>
        /// If set, the length is 128 bytes.
        /// </summary>
        public readonly byte[] MasterKey;

        /// <summary>
        /// First 64 bytes of the MasterKey.
        /// </summary>
        public readonly byte[] HdWalletSeed;

        /// <summary>
        /// This is currently a Curve25519 elliptic curve key pair.
        /// </summary>
        public readonly ECKeyPair IdentityKeyPair;

        /// <summary>
        /// The XDS chat Id, derived from the public key of the IdentityKeyPair.
        /// </summary>
        public readonly string ChatId;

        /// <summary>
        /// The XDS blockchain P2WPKH address corresponding to the ChatId.
        /// HD Key path: {m/44'/xds_coin_type'/0'/0/0} (account_0, external, index 0)
        /// </summary>
        public readonly XDSPubKeyAddress DefaultAddress;

        public XDSPrincipal(byte[] masterKey, byte[] hdWalletSeed, ECKeyPair identityKeyPair, string chatId, XDSPubKeyAddress defaultAddress)
        {
            // not checking params for null because not all params are needed at all times
            this.MasterKey = masterKey;
            this.HdWalletSeed = hdWalletSeed;
            this.IdentityKeyPair = identityKeyPair;
            this.ChatId = chatId;
            this.DefaultAddress = defaultAddress;
        }
    }
}
