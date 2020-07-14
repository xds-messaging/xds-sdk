using System;
using System.Diagnostics;
using System.Net;
using XDS.SDK.Cryptography;
using XDS.SDK.Messaging.BlockchainClient.Data;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.SDK.Messaging.BlockchainClient
{
    public static class PeerSerializer
    {
        public static byte[] SerializeCore(Peer peer)
        {
            byte[] serialized = PocoSerializer.Begin()
                .Append(peer.Id)
                .Append((ulong) peer.PeerServices)
                .Append(peer.LastSeen)
                .Append(peer.LastError)
                .Append(peer.BytesSent)
                .Append(peer.BytesReceived)
                .Append(peer.Priority)
                .Finish();
            return serialized;
        }

        public static Peer Deserialize(byte[] serializedMessage)
        {
            if (serializedMessage == null)
                return null;

            var ser = PocoSerializer.GetDeserializer(serializedMessage);

            var peer = new Peer();
            peer.Id = ser.MakeString(0);

            peer.PeerServices = (PeerServices) ser.MakeUInt64(1);

            peer.LastSeen = ser.MakeDateTime(2);
            peer.LastError = ser.MakeDateTime(3);

            peer.BytesSent = ser.MakeUInt64(4);
            peer.BytesReceived = ser.MakeUInt64(5);
            peer.Priority = ser.MakeInt32(6);

            var address = peer.Id.ToAddress();
            peer.IPAddress = address.ipAddress;
            peer.ProtocolPort = address.port;

            return peer;
        }

        public static Peer ToPeer(this BitcoinNetworkAddressPayload payload)
        {
            var p = new Peer
            {
                Id = CreatePeerId(payload.IPAddress, payload.Port),
                IPAddress = payload.IPAddress,
                ProtocolPort = payload.Port,
                LastSeen = payload.Timestamp.DateTime,
                PeerServices = payload.PeerServices,
                LastError = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
            };
            return p;
        }

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