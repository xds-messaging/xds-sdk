using System;
using System.Net;
using System.Security.Cryptography;

namespace XDS.SDK.Messaging.BlockchainClient
{
    public static class Tools
    {
        static readonly RNGCryptoServiceProvider RNG = new RNGCryptoServiceProvider();

        public static byte[] GetRandomNonce()
        {
            var nonce = new byte[8];
            RNG.GetNonZeroBytes(nonce);
            return nonce;
        }

        public static IPAddress GetIpAddressFromHostName(string hostName, int port, bool throwIfMoreThanOneIp = true)
        {
            var addresses = Dns.GetHostAddresses(hostName);
            if (addresses.Length == 0)
                throw new ArgumentException("Unable to retrieve address from specified host name.", nameof(hostName));

            if (throwIfMoreThanOneIp && addresses.Length > 1)
                throw new ArgumentException("There is more that one IP address to the specified host.",
                    nameof(hostName));
            return new IPEndPoint(addresses[0], port).Address; // port gets validated here.
        }

        public static IPEndPoint GetIpEndPointFromHostName(string hostName, int port, bool throwIfMoreThanOneIp = true)
        {
            var addresses = Dns.GetHostAddresses(hostName);
            if (addresses.Length == 0)
                throw new ArgumentException("Unable to retrieve address from specified host name.", nameof(hostName));

            if (throwIfMoreThanOneIp && addresses.Length > 1)
                throw new ArgumentException("There is more that one IP address to the specified host.",
                    nameof(hostName));
            return new IPEndPoint(addresses[0], port); // port gets validated here.
        }
    }
}