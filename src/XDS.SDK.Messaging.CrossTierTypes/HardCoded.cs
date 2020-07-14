namespace XDS.SDK.Messaging.CrossTierTypes
{
    public static class HardCoded
    {
        /// <summary>
        /// If this is given instead of a user identifier
        /// the recipient presumes the identity of the sender and doesn't
        /// need additional information (i.e. a userID) to find the 
        /// sender's public keys. 
        /// </summary>
        public const string Server0001 = "Server0001";

        public static readonly byte[] Server0001StaticPublicKey =
        {
          0xC6, 0xBD, 0x2F, 0xCA, 0xEA, 0xF2, 0x9D, 0xFB, 0x0C, 0xC2, 0x95, 0x1A, 0x4D, 0x3F, 0xE8, 0x38,
            0x67, 0xB3, 0x43, 0x95, 0x59, 0x85, 0x0E, 0x74, 0x31, 0x93, 0x3D, 0x1A, 0xB9, 0x18, 0xE7, 0x66
        };
    }
}
