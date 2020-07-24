using System;
using System.Diagnostics;
using System.Net;
using XDS.SDK.Cryptography;

namespace XDS.SDK.Messaging.CrossTierTypes.BlockchainIntegration
{
    public static class IpPortToId
    {
        public static (IPAddress ipAddress, int port) ToAddress(this string peerId)
        {
            var parts = peerId.Split('-');
            var ipBytes = parts[0].FromHexString();
            Debug.Assert(ipBytes.Length == 16);
            var ipAddress = new IPAddress(ipBytes);
            var port = ushort.Parse(parts[1]);
            return (ipAddress, port);
        }

        public static string CreatePeerId(this IPAddress ipAddress, int port)
        {
            byte[] ipBytes = ipAddress.GetAddressBytes();
            if (ipBytes.Length == 16) return $"{ipBytes.ToHexString()}-{port}";
            if (ipBytes.Length == 4)
            {
                var ipV6Bytes = new byte[16];
                Buffer.BlockCopy(ipBytes, 0, ipV6Bytes, 12, 4);
                ipV6Bytes[10] = 0xff;
                ipV6Bytes[11] = 0xff;
                return $"{ipV6Bytes.ToHexString()}-{port}";
            }

            throw new ArgumentException("ipAddress");
        }
    }
}
