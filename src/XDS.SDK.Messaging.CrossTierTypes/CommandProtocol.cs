using System;
using System.Collections.Generic;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.CrossTierTypes.Photon;

namespace XDS.SDK.Messaging.CrossTierTypes
{
    public static class CommandProtocol
    {
        const int HeaderSize = sizeof(byte) + sizeof(int); // = 5 = 1 + 4 = CommandByte + TotalLenght

        public static bool IsAuthenticationRequired(CommandId commandId)
        {
            return commandId != CommandId.PublishIdentity;
        }

        public static byte[] Serialize(this RequestCommand requestCommand, CommandHeader commandHeader)
        {
            if (commandHeader == CommandHeader.Yes)
                return Serialize(requestCommand.Contents).AddHeader(requestCommand.CommandId);
            return Serialize(requestCommand.Contents);
        }


        public static byte[] Serialize(this RequestCommand requestCommand, CommandHeader commandHeader, out string networkPayloadHash)
        {
            if (commandHeader == CommandHeader.Yes)
            {
                var bodyPart = Serialize(requestCommand.Contents);
                networkPayloadHash = NetworkPayloadHash.ComputeAsGuidString(bodyPart);
                return bodyPart.AddHeader(requestCommand.CommandId);
            }

            var body = Serialize(requestCommand.Contents);
            networkPayloadHash = NetworkPayloadHash.ComputeAsGuidString(body);
            return body;
        }

        static (long balance, int height, byte[] hashBlock, int photonError) test()
        {
            return default;
        }

        static byte[] Serialize(object value)
        {
            switch (value)
            {
                case null:
                    throw new ArgumentNullException(nameof(value), "Serializer does not accept null.");
                case byte b:
                    return new[] { b };
                case string s:
                    return s.SerializeCore();
                case XMessage message:
                    return message.SerializeCore();
                case XResendRequest resendRequest:
                    return resendRequest.Serialize();
                case XIdentity xIdentity:
                    return xIdentity.SerializeXIdentity();
                case List<XMessage> listOfMessage:
                    return listOfMessage.SerializeCollection(XMessageExtensions.SerializeCore);
                case List<string> listOfString:
                    return listOfString.SerializeCollection(PocoSerializer.SerializeCore);
                case ValueTuple<long, int, byte[], int> balance:
                    {
                        var part1 = BitConverter.GetBytes(balance.Item1);
                        var part2 = BitConverter.GetBytes(balance.Item2);
                        var part4 = BitConverter.GetBytes(balance.Item4);
                        return ByteArrays.Concatenate(part1, part2, balance.Item3 ?? new byte[32], part4);
                    }
                case ValueTuple<long, int, byte[], IPhotonOutput[], int> outputs:
                    {
                        var balance = BitConverter.GetBytes(outputs.Item1);
                        var height = BitConverter.GetBytes(outputs.Item2);
                        var hashBlock = outputs.Item3 ?? new byte[32];
                        if(outputs.Item4 == null)
                            outputs.Item4 = new IPhotonOutput[0];
                        var outputCollection = outputs.Item4.SerializeCollection(PhotonOutputExtensions.SerializeCore);
                        var photonError = BitConverter.GetBytes(outputs.Item5);
                        return ByteArrays.Concatenate(balance, height, hashBlock, photonError, outputCollection);
                    }
                default:
                    throw new NotSupportedException($"Serialization of {value.GetType()} is not supported.");
            }


        }
        public static Command ParseCommand(this IRequestCommandData tlsRequest)
        {
            var announcedLenght = BitConverter.ToInt32(tlsRequest.CommandData, 1);
            if (announcedLenght != tlsRequest.CommandData.Length)
                throw new InvalidOperationException($"According to the information in the message, length should be {announcedLenght} but actual length is {tlsRequest.CommandData.Length}.");
            var commandWithoutHeader = new byte[tlsRequest.CommandData.Length - HeaderSize];
            Buffer.BlockCopy(tlsRequest.CommandData, HeaderSize, commandWithoutHeader, 0, commandWithoutHeader.Length);
            return new Command((CommandId)tlsRequest.CommandData[0], commandWithoutHeader);
        }






        static byte[] AddHeader(this byte[] serializedResponse, CommandId commandId)
        {
            return CreateReplyWithHeader(commandId, serializedResponse);
        }

        static byte[] CreateReplyWithHeader(CommandId commandId, byte[] serializedResponse)
        {
            int replyLenght = Convert.ToInt32(HeaderSize + serializedResponse.Length);
            byte[] replyLenghtBytes = BitConverter.GetBytes(replyLenght);

            var replyWithHeader = new byte[replyLenght];
            replyWithHeader[0] = (byte)commandId;
            replyWithHeader[1] = replyLenghtBytes[0];
            replyWithHeader[2] = replyLenghtBytes[1];
            replyWithHeader[3] = replyLenghtBytes[2];
            replyWithHeader[4] = replyLenghtBytes[3];

            Buffer.BlockCopy(serializedResponse, 0, replyWithHeader, HeaderSize, serializedResponse.Length);
            return replyWithHeader;
        }


        public static bool IsCommandDefined(this CommandId commandId)
        {
            return Enum.IsDefined(typeof(CommandId), commandId) && commandId != CommandId.Zero;
        }
    }
}
