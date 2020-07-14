using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace XDS.SDK.Messaging.BlockchainClient
{
    public class BitcoinMessages
    {
    }

    /// <summary>
    ///     https://en.bitcoin.it/wiki/Protocol_documentation#Message_structure
    /// </summary>
    public class BitcoinMessage
    {
        public const int MagicSize = 4;

        public const int CommandSize = 12;

        public const int PayloadLengthSize = 4;

        public const int ChecksumSize = 4;

        /// <summary>
        ///     A modern header includes the 4 checksum bytes, so it's:
        ///     4 (Magic)
        ///     + 12 (Command)
        ///     + 4 (PayloadLength)
        ///     + 4 (Checksum)
        ///     = 24
        /// </summary>
        public const int ModernHeaderSize = 24;

        static readonly SHA256 SHA = SHA256.Create();

        public readonly byte[] ChecksumBytes; // 4 bytes

        public readonly string Command;

        public readonly byte[] CommandBytes; // 12 bytes


        /// <summary>
        ///     4 bytes, uint32_t
        ///     e.g. 31 53 44 58
        ///     0  1  2  3
        ///     so, 0x58445331 is reversed
        /// </summary>
        public readonly byte[] MagicBytes; // 4 bytes

        public readonly int NetworkSize; // length for performance counter

        public readonly byte[] PayloadBytes; // variable

        public readonly byte[] PayloadLengthBytes; // 4 bytes

        public BitcoinMessage(string command, byte[] payloadBytes)
        {
            this.Command = command;

            this.MagicBytes = BitConverter.GetBytes(ChatClientConfiguration.NetworkMagic);

            this.CommandBytes = new byte[12];
            var commandBytes = Encoding.ASCII.GetBytes(command);
            if (commandBytes.Length > 11)
                throw new InvalidOperationException(
                    "Command can only have 11 characters (and we need at least 1 byte of NUL for padding, to a total length of 12.");

            Buffer.BlockCopy(commandBytes, 0, this.CommandBytes, 0, commandBytes.Length);

            this.PayloadBytes = payloadBytes;

            this.PayloadLengthBytes = BitConverter.GetBytes((uint) payloadBytes.Length);

            this.ChecksumBytes = new byte[4];

            // create checksum
            var first = SHA.ComputeHash(this.PayloadBytes);
            var actualChecksum = SHA.ComputeHash(first);
            Buffer.BlockCopy(actualChecksum, 0, this.ChecksumBytes, 0, 4);

            this.NetworkSize = ModernHeaderSize + payloadBytes.Length;
        }

        public BitcoinMessage(string command, byte[] payloadBytes, byte[] expectedChecksum) : this(command,
            payloadBytes)
        {
            // verify checksum
            var first = SHA.ComputeHash(this.PayloadBytes);
            var actualChecksum = SHA.ComputeHash(first);
            for (var i = 0; i < expectedChecksum.Length; i++)
                if (actualChecksum[i] != expectedChecksum[i])
                    throw new InvalidDataException(
                        $"Invalid checksum at byte {i}: actual: {actualChecksum[i]} expected: {expectedChecksum[i]}");

            this.ChecksumBytes = expectedChecksum;
        }

        public byte[] Serialize()
        {
            var dest = new byte[24 + this.PayloadBytes.Length];
            Buffer.BlockCopy(this.MagicBytes, 0, dest, 0, 4);
            Buffer.BlockCopy(this.CommandBytes, 0, dest, 4, 12);
            Buffer.BlockCopy(this.PayloadLengthBytes, 0, dest, 16, 4);
            Buffer.BlockCopy(this.ChecksumBytes, 0, dest, 20, 4);
            Buffer.BlockCopy(this.PayloadBytes, 0, dest, 24, this.PayloadBytes.Length);
            return dest;
        }
    }

    public class BitcoinVersionPayload
    {
        public readonly byte[] NonceBytes; // 8 bytes

        public readonly uint ProtocolVersion;
        public readonly byte[] ProtocolVersionBytes; // 4 bytes
        public readonly BitcoinNetworkAddressPayload Receiver;
        public readonly byte[] RelayBytes; // 1 byte
        public readonly BitcoinNetworkAddressPayload Sender;
        public readonly PeerServices Services;
        public readonly byte[] ServicesBytes; // 8 bytes

        public readonly uint StartHeight;
        public readonly byte[] StartHeightBytes; // 4 bytes
        public readonly DateTimeOffset TimeStamp;

        public readonly BitcoinVarString UserAgent;
        public byte[] ReceiverAddressBytes; // 26 bytes
        public byte[] SenderAddressBytes; // 26 bytes
        public byte[] TimestampBytes; // 8 bytes
        public byte[] UserAgentBytes; // varstring

        public BitcoinVersionPayload(BitcoinVarString userAgent, byte[] nonceBytes, BitcoinNetworkAddressPayload sender,
            BitcoinNetworkAddressPayload receiver)
        {
            this.ProtocolVersion = ChatClientConfiguration.ProtocolVersion;
            this.ProtocolVersionBytes = BitConverter.GetBytes(ChatClientConfiguration.ProtocolVersion);

            this.Services = ChatClientConfiguration.OwnPeerServices;
            this.ServicesBytes = BitConverter.GetBytes((ulong) this.Services);

            this.TimeStamp = DateTimeOffset.UtcNow;
            this.TimestampBytes = BitConverter.GetBytes((ulong) this.TimeStamp.ToUnixTimeSeconds());

            this.Sender = sender;
            this.SenderAddressBytes = sender.SerializeForVersion();

            this.Receiver = receiver;
            this.ReceiverAddressBytes = receiver.SerializeForVersion();

            this.NonceBytes = nonceBytes;

            this.UserAgent = userAgent;
            this.UserAgentBytes = userAgent.VarStringBytes;

            this.StartHeightBytes = new byte[4];
            this.StartHeight = BitConverter.ToUInt32(this.StartHeightBytes, 0);
            this.RelayBytes = new byte[1];
            this.RelayBytes[0] = 1;
        }

        public BitcoinVersionPayload(byte[] serialized)
        {
            this.ProtocolVersionBytes = new byte[4];
            Buffer.BlockCopy(serialized, 0, this.ProtocolVersionBytes, 0, 4);
            this.ProtocolVersion = BitConverter.ToUInt32(this.ProtocolVersionBytes, 0);

            this.ServicesBytes = new byte[8];
            Buffer.BlockCopy(serialized, 4, this.ServicesBytes, 0, 8);
            this.Services = (PeerServices) BitConverter.ToUInt64(this.ServicesBytes, 0);

            this.TimestampBytes = new byte[8];
            Buffer.BlockCopy(serialized, 12, this.TimestampBytes, 0, 8);
            this.TimeStamp = DateTimeOffset.FromUnixTimeSeconds((long) BitConverter.ToUInt64(this.TimestampBytes, 0));

            this.ReceiverAddressBytes = new byte[26];
            Buffer.BlockCopy(serialized, 20, this.ReceiverAddressBytes, 0, 26);
            this.Receiver = new BitcoinNetworkAddressPayload(this.ReceiverAddressBytes, 0, false);

            this.SenderAddressBytes = new byte[26];
            Buffer.BlockCopy(serialized, 46, this.SenderAddressBytes, 0, 26);
            this.Sender = new BitcoinNetworkAddressPayload(this.SenderAddressBytes, 0, false);

            this.NonceBytes = new byte[8];
            Buffer.BlockCopy(serialized, 72, this.NonceBytes, 0, 8);

            this.UserAgent = new BitcoinVarString(serialized, 80);
            this.UserAgentBytes = this.UserAgent.VarStringBytes;

            this.StartHeightBytes = new byte[4];
            Buffer.BlockCopy(serialized, 80 + this.UserAgent.SerializedLength, this.StartHeightBytes, 0, 4);
            this.StartHeight = BitConverter.ToUInt32(this.StartHeightBytes, 0);


            this.RelayBytes = new byte[1];
            this.RelayBytes[0] = serialized[80 + this.UserAgent.SerializedLength + 4];
        }

        public byte[] Serialize()
        {
            if (this.ProtocolVersionBytes.Length
                + this.ServicesBytes.Length
                + this.TimestampBytes.Length
                + this.ReceiverAddressBytes.Length
                + this.SenderAddressBytes.Length
                + this.NonceBytes.Length
                // UserAgentBytes
                + this.StartHeightBytes.Length
                + this.RelayBytes.Length != 85)
                throw new Exception("Invalid part lenghts!");
            var dest = new byte[85 + this.UserAgentBytes.Length];
            Buffer.BlockCopy(this.ProtocolVersionBytes, 0, dest, 0, this.ProtocolVersionBytes.Length);
            Buffer.BlockCopy(this.ServicesBytes, 0, dest, 4, this.ServicesBytes.Length);
            Buffer.BlockCopy(this.TimestampBytes, 0, dest, 12, this.TimestampBytes.Length);
            Buffer.BlockCopy(this.ReceiverAddressBytes, 0, dest, 20, this.ReceiverAddressBytes.Length);
            Buffer.BlockCopy(this.SenderAddressBytes, 0, dest, 46, this.SenderAddressBytes.Length);
            Buffer.BlockCopy(this.NonceBytes, 0, dest, 72, this.NonceBytes.Length);
            Buffer.BlockCopy(this.UserAgentBytes, 0, dest, 80, this.UserAgentBytes.Length);
            Buffer.BlockCopy(this.StartHeightBytes, 0, dest, 80 + this.UserAgentBytes.Length,
                this.StartHeightBytes.Length);
            Buffer.BlockCopy(this.RelayBytes, 0, dest, 80 + this.UserAgentBytes.Length + this.StartHeightBytes.Length,
                this.RelayBytes.Length);
            return dest;
        }
    }

    public class BitcoinAddrPayload
    {
        public readonly BitcoinVarInt AddrCount;
        public readonly List<BitcoinNetworkAddressPayload> Addresses;

        public BitcoinAddrPayload(byte[] serialized)
        {
            this.Addresses = new List<BitcoinNetworkAddressPayload>();
            this.AddrCount = new BitcoinVarInt(serialized);
            var currentOffset = this.AddrCount.SerializedLength;
            for (var i = currentOffset; i < 30 * (int) this.AddrCount.Value; i += 30)
            {
                var addr = new BitcoinNetworkAddressPayload(serialized, i, true);
                this.Addresses.Add(addr);
            }
        }
    }

    public class PingPayload
    {
        public byte[] NonceBytes;

        public PingPayload(byte[] nonceBytes)
        {
            this.NonceBytes = nonceBytes;
            if (nonceBytes.Length != 8) throw new InvalidOperationException("Ping expects 8 bytes of a nonce.");
        }
    }

    public class PongPayload
    {
        public byte[] NonceBytes;

        public PongPayload(byte[] nonceBytes)
        {
            this.NonceBytes = nonceBytes;
            if (nonceBytes.Length != 8) throw new InvalidOperationException("Pong expects 8 bytes of a nonce.");
        }
    }


    public class BitcoinVarInt
    {
        public int SerializedLength;

        public ulong Value;
        public byte[] VarIntBytes;

        public BitcoinVarInt(ulong uint64)
        {
            if (uint64 < 0xfd)
            {
                this.VarIntBytes = new byte[1];
                this.VarIntBytes[0] = (byte) uint64;
                this.SerializedLength = 1;
            }
            else if (uint64 <= 0xffff)
            {
                this.VarIntBytes = new byte[3];
                this.VarIntBytes[0] = 0xfd;
                var two = BitConverter.GetBytes((ushort) uint64);
                Buffer.BlockCopy(two, 0, this.VarIntBytes, 1, 2);
                this.SerializedLength = 3;
            }
            else if (uint64 <= 0xffffffff)
            {
                this.VarIntBytes = new byte[5];
                this.VarIntBytes[0] = 0xfe;
                var four = BitConverter.GetBytes((uint) uint64);
                Buffer.BlockCopy(four, 0, this.VarIntBytes, 1, 4);
                this.SerializedLength = 5;
            }
            else
            {
                this.VarIntBytes = new byte[9];
                this.VarIntBytes[0] = 0xff;
                var eight = BitConverter.GetBytes(uint64);
                Buffer.BlockCopy(eight, 0, this.VarIntBytes, 1, 8);
                this.SerializedLength = 9;
            }

            this.Value = uint64;
        }

        public BitcoinVarInt(byte[] varIntBytes, int startIndex = 0)
        {
            byte prefix = varIntBytes[startIndex];

            if (prefix < 0xfd)
            {
                this.Value = prefix;
                this.SerializedLength = 1;
            }
            else if (prefix == 0xfd)
            {
                this.Value = BitConverter.ToUInt16(varIntBytes, startIndex + 1);
                this.SerializedLength = 3;
            }
            else if (prefix == 0xfe)
            {
                this.Value = BitConverter.ToUInt32(varIntBytes, startIndex + 1);
                this.SerializedLength = 5;
            }
            else
            {
                this.Value = BitConverter.ToUInt64(varIntBytes, startIndex + 1);
                this.SerializedLength = 9;
            }
        }
    }

    public class BitcoinVarString
    {
        public readonly int SerializedLength;

        public readonly string Text;
        public readonly byte[] VarStringBytes;

        public BitcoinVarString(string text)
        {
            byte[] lengthAsVarInt = new BitcoinVarInt((ulong) text.Length).VarIntBytes;
            byte[] textBytes = Encoding.ASCII.GetBytes(text);
            this.VarStringBytes = new byte[lengthAsVarInt.Length + textBytes.Length];
            Buffer.BlockCopy(lengthAsVarInt, 0, this.VarStringBytes, 0, lengthAsVarInt.Length);
            Buffer.BlockCopy(textBytes, 0, this.VarStringBytes, lengthAsVarInt.Length, textBytes.Length);
            this.Text = text;
            this.SerializedLength = this.VarStringBytes.Length;
        }

        public BitcoinVarString(byte[] varStringBytes, int startIndex = 0)
        {
            this.VarStringBytes = varStringBytes;
            BitcoinVarInt length = new BitcoinVarInt(varStringBytes, startIndex);
            this.Text = Encoding.ASCII.GetString(varStringBytes, startIndex + length.SerializedLength,
                (int) length.Value);
            this.SerializedLength = length.SerializedLength + (int) length.Value;
            this.VarStringBytes = new byte[this.SerializedLength];
            Buffer.BlockCopy(varStringBytes, startIndex, this.VarStringBytes, 0, this.VarStringBytes.Length);
        }
    }

    public class BitcoinNetworkAddressPayload
    {
        public readonly IPAddress IPAddress;
        public readonly byte[] IPBytes; // 16 bytes
        public readonly byte[] PeerServicesBytes; // 8 bytes
        public readonly int Port;
        public readonly byte[] TimeBytes; // 4 bytes, not serialized in version message

        public readonly DateTimeOffset Timestamp;
        public PeerServices PeerServices; // this is updatable from the version payload while connecting
        public byte[] PortBytes; // 2 bytes


        public BitcoinNetworkAddressPayload(DateTimeOffset timestamp, PeerServices peerServices, IPAddress ipAddress,
            int port)
        {
            this.Timestamp = timestamp;
            this.PeerServices = peerServices;
            this.IPAddress = ipAddress;
            this.Port = port;

            this.TimeBytes = BitConverter.GetBytes((uint) DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            this.PeerServicesBytes = BitConverter.GetBytes((ulong) this.PeerServices);

            byte[] ipBytes = ipAddress.GetAddressBytes();
            if (ipBytes.Length == 16)
            {
                this.IPBytes = ipBytes;
            }
            else if (ipBytes.Length == 4)
            {
                this.IPBytes = new byte[16];
                Buffer.BlockCopy(ipBytes, 0, this.IPBytes, 12, 4);
                this.IPBytes[10] = 0xff;
                this.IPBytes[11] = 0xff;
            }
            else
            {
                throw new ArgumentException("ipAddress");
            }

            var portBytes = BitConverter.GetBytes((ushort) port);
            this.PortBytes = new byte[2];
            // swap to get network byte order
            this.PortBytes[0] = portBytes[1];
            this.PortBytes[1] = portBytes[0];
        }


        public BitcoinNetworkAddressPayload(byte[] serialized, int startIndex, bool hasTimeStamp)
        {
            if (hasTimeStamp)
            {
                var time = BitConverter.ToUInt32(serialized, startIndex);
                this.Timestamp = DateTimeOffset.FromUnixTimeSeconds(time);
                this.TimeBytes = new byte[4];
                Buffer.BlockCopy(serialized, startIndex, this.TimeBytes, 0, 4);

                this.PeerServices = (PeerServices) BitConverter.ToUInt64(serialized, startIndex + 4);
                this.PeerServicesBytes = new byte[8];
                Buffer.BlockCopy(serialized, startIndex + 4, this.PeerServicesBytes, 0, 8);

                this.IPBytes = new byte[16];
                Buffer.BlockCopy(serialized, startIndex + 12, this.IPBytes, 0, 16);
                this.IPAddress = new IPAddress(this.IPBytes);

                this.PortBytes = new byte[2];
                Buffer.BlockCopy(serialized, startIndex + 28, this.PortBytes, 0, 2);

                var portBytes = new byte[2];
                // swap to from network byte order
                portBytes[0] = this.PortBytes[1];
                portBytes[1] = this.PortBytes[0];
                this.Port = BitConverter.ToUInt16(portBytes, 0);
            }
            else
            {
                this.PeerServices = (PeerServices) BitConverter.ToUInt64(serialized, startIndex);
                this.PeerServicesBytes = new byte[8];
                Buffer.BlockCopy(serialized, startIndex, this.PeerServicesBytes, 0, 8);

                this.IPBytes = new byte[16];
                Buffer.BlockCopy(serialized, startIndex + 8, this.IPBytes, 0, 16);
                this.IPAddress = new IPAddress(this.IPBytes);

                this.PortBytes = new byte[2];
                Buffer.BlockCopy(serialized, startIndex + 24, this.PortBytes, 0, 2);

                var portBytes = new byte[2];
                // swap to from network byte order
                portBytes[0] = this.PortBytes[1];
                portBytes[1] = this.PortBytes[0];
                this.Port = BitConverter.ToUInt16(portBytes, 0);
            }
        }

        public override string ToString()
        {
            return $"{this.IPAddress}:{this.Port}";
        }


        internal byte[] SerializeForVersion()
        {
            var serialized = new byte[26];
            Buffer.BlockCopy(this.PeerServicesBytes, 0, serialized, 0, 8);
            Buffer.BlockCopy(this.IPBytes, 0, serialized, 8, 16);
            Buffer.BlockCopy(this.PortBytes, 0, serialized, 8 + 16, 2);
            return serialized;
        }
    }

    public class BitcoinVerAckPayload
    {
    }
}