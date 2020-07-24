using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.CrossTierTypes.Photon;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
    public class PhotonWalletManager 
    {
        ILogger logger;
        AppRepository appRepository;
        ITcpConnection tcpConnection;
        readonly INetworkClient networkClient;

        public PhotonWalletManager(ILoggerFactory loggerFactory, AppRepository appRepository, ITcpConnection tcpConnection, INetworkClient networkClient)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.appRepository = appRepository;
            this.tcpConnection = tcpConnection;
            this.networkClient = networkClient;
        }

        (long balance, int height, byte[] hashBlock, PhotonError photonError) DeserializePhotonBalance(byte[] serialized)
        {
            var startIndex = 0;

            long balance = BitConverter.ToInt64(serialized, startIndex);
            startIndex += sizeof(long);

            int height = BitConverter.ToInt32(serialized, startIndex);
            startIndex += sizeof(int);

            byte[] hashBlock = new byte[32];
            Buffer.BlockCopy(serialized, startIndex, hashBlock, 0, 32);
            startIndex += 32;
            PhotonError photonError = (PhotonError)BitConverter.ToInt32(serialized, startIndex);
            return (balance, height, hashBlock, photonError);
        }


        public async Task<(long balance, int height, byte[] hashBlock, PhotonError photonError)> GetPhotonBalance(string address, PhotonFlags photonFlags)
        {

            var request = $"{address};{(int)photonFlags}";
            var requestCommand = new RequestCommand(CommandId.PhotonBalance, request).Serialize(CommandHeader.Yes);
            var tlsResponse = await this.networkClient.SendRequestAsync(requestCommand, Transport.TCP);

            return DeserializePhotonBalance(tlsResponse[0].CommandData);
        }

        public async Task<(long balance, int height, byte[] hashBlock, IPhotonOutput[] outputs, PhotonError photonError)> GetPhotonOutputs(string address, PhotonFlags photonFlags)
        {
            var request = $"{address};{(int)photonFlags}";
            var requestCommand = new RequestCommand(CommandId.PhotonOutputs, request).Serialize(CommandHeader.Yes);
            var tlsResponse = await this.networkClient.SendRequestAsync(requestCommand, Transport.TCP);
            return DeserializePhotonOutputs(tlsResponse[0].CommandData);
        }

        private (long balance, int height, byte[] hashBlock, IPhotonOutput[] outputs, PhotonError photonError) DeserializePhotonOutputs(byte[] serialized)
        {
            var startIndex = 0;

            long balance = BitConverter.ToInt64(serialized, startIndex);
            startIndex += sizeof(long);

            int height = BitConverter.ToInt32(serialized, startIndex);
            startIndex += sizeof(int);

            byte[] hashBlock = new byte[32];
            Buffer.BlockCopy(serialized, startIndex, hashBlock, 0, 32);
            startIndex += 32;
            PhotonError photonError = (PhotonError)BitConverter.ToInt32(serialized, startIndex);
            startIndex += sizeof(int);

            byte[] outputCollection = new byte[serialized.Length - startIndex];
            Buffer.BlockCopy(serialized,startIndex,outputCollection,0,outputCollection.Length);
            var deserialized = PocoSerializer.DeserializeCollection(outputCollection, PhotonOutputExtensions.DeserializePhotonOutput);
            IPhotonOutput[] outputs = deserialized.ToArray();
            return (balance, height, hashBlock,outputs, photonError);
        }
    }
}
