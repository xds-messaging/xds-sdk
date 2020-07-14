using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;
using XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Workers
{
    public class ChatClient : IChatClient
    {
        readonly IDependencyInjection dependencyInjection;
        readonly ILogger logger;

        INetworkClient networkClient;
        ChatWorker chatWorker;
        AppRepository appRepository;

        public ChatClient(IDependencyInjection dependencyInjection, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<ChatClient>();
            this.dependencyInjection = dependencyInjection;
        }

        public void Init(string myId, byte[] myPrivateKey)
        {
            

            this.MyId = myId;
            this.MyPrivateKey = myPrivateKey;

            // todo: check if this can move to c'tor
            this.networkClient = this.dependencyInjection.ServiceProvider.Get<INetworkClient>();
            this.chatWorker = this.dependencyInjection.ServiceProvider.Get<ChatWorker>();
            this.appRepository = this.dependencyInjection.ServiceProvider.Get<AppRepository>();
        }

        public string MyId { get; private set; }
        public byte[] MyPrivateKey { get; private set; }

        public async Task<Response<IReadOnlyCollection<XMessage>>> DownloadMessages(string myId)
        {
            

            var response = new Response<IReadOnlyCollection<XMessage>>();
            try
            {
                var requestCommand = new RequestCommand(CommandId.DownloadMessages, myId).Serialize(CommandHeader.Yes);
                var tlsResponse = await this.networkClient.SendRequestAsync(requestCommand, Transport.TCP);
                List<XMessage> downloadedMessages = new List<XMessage>();
                foreach (var authenticableRequest in tlsResponse)
                {
                    downloadedMessages.AddRange(authenticableRequest.CommandData.DeserializeCollection(XMessageExtensions.DeserializeMessage));
                }

                this.logger.LogDebug($"Successfully downloaded and deserialized {downloadedMessages.Count} XMessage items.");
                response.Result = downloadedMessages;
                response.SetSuccess();
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                response.SetError(e);
            }
            return response;
        }

        public async Task<Response<byte>> AnyNews(string myId)
        {
            

            var response = new Response<byte>();
            try
            {
                var requestCommand = new RequestCommand(CommandId.AnyNews, myId).Serialize(CommandHeader.Yes);

                List<IRequestCommandData> tlsResponse = await this.networkClient.SendRequestAsync(requestCommand, Transport.TCP);
                AssertOneItem(tlsResponse);

                var contents = tlsResponse[0].CommandData[0];
                response.Result = contents;
                response.SetSuccess();
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                response.SetError(e);
            }
            return response;
        }

        void AssertOneItem(List<IRequestCommandData> requestCommandData)
        {
            Debug.Assert(requestCommandData != null && requestCommandData.Count == 1);
        }

        public async Task<Response<byte>> CheckForResendRequest(XResendRequest resendRequest)
        {
            

            var response = new Response<byte>();
            try
            {
                var requestCommand = new RequestCommand(CommandId.CheckForResendRequest, resendRequest).Serialize(CommandHeader.Yes);
                var tlsResponse = await this.networkClient.SendRequestAsync(requestCommand, Transport.TCP);
                AssertOneItem(tlsResponse);

                var contents = tlsResponse[0].CommandData[0];
                response.Result = contents;
                response.SetSuccess();
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                response.SetError(e);
            }
            return response;
        }


        public async Task<Response<NetworkPayloadAdded>> UploadMessage(Message message)
        {
            

            var response = new Response<NetworkPayloadAdded>();
            try
            {
                var recipient = await this.appRepository.GetContact(message.RecipientId);
                var recipientChatId = recipient.ChatId;

                var xm = new XMessage
                {
                    Id = recipientChatId,

                    DynamicPublicKey = message.DynamicPublicKey,
                    DynamicPublicKeyId = message.DynamicPublicKeyId,
                    PrivateKeyHint = message.PrivateKeyHint,

                    MetaCipher = message.MetaCipher,
                    TextCipher = message.TextCipher,
                    ImageCipher = message.ImageCipher,
                };

                byte[] networkPayload = new RequestCommand(CommandId.UploadMessage, xm).Serialize(CommandHeader.Yes, out string networkPayloadHash);

                var tlsResponse = await this.networkClient.SendRequestAsync(networkPayload, Transport.TCP);
                AssertOneItem(tlsResponse);

                response.Result = new NetworkPayloadAdded
                {
                    NetworkResponse = tlsResponse[0].CommandData.DeserializeStringCore(),
                    NetworkPayloadHash = networkPayloadHash

                };
                response.SetSuccess();
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                response.SetError(e);
            }
            return response;
        }


        /// <inheritdoc />
        /// <summary>
        /// Uploads a resend request.
        /// </summary>
        /// <param name="resendRequest">The XResendRequest containing only the NetworkPayloadHash of the message that the other party should resend.</param>
        /// <returns>The NetworkPayloadHash GuidString, to indicate success.</returns>
        public async Task<Response<NetworkPayloadAdded>> UploadResendRequest(XResendRequest resendRequest)
        {
            

            var response = new Response<NetworkPayloadAdded>();
            try
            {
                this.logger.LogDebug($"Uploading resendRequest: NPH/ID: {resendRequest.Id}, {resendRequest.RecipientId}");
                Debug.Assert(resendRequest.RecipientId == null);

                byte[] networkPayload = new RequestCommand(CommandId.UploadResendRequest, resendRequest).Serialize(CommandHeader.Yes);
                var tlsResponse = await this.networkClient.SendRequestAsync(networkPayload, Transport.TCP);
                AssertOneItem(tlsResponse);

                var ret = tlsResponse[0].CommandData.DeserializeStringCore();
                if (ret == resendRequest.Id)
                {
                    response.Result = new NetworkPayloadAdded
                    {
                        NetworkResponse = ret,
                        NetworkPayloadHash = null // TODO: check if this property is still used anywhere
                    };
                    response.SetSuccess();
                }
                else
                {
                    response.SetError("The uploaded NetworkPayloadHash and the returned value are not equal.");
                }

            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                response.SetError(e);
            }
            return response;
        }


        public async Task<Response<string>> PublishIdentityAsync(XIdentity identity)
        {
            

            var response = new Response<string>();
            try
            {
                var requestCommand = new RequestCommand(CommandId.PublishIdentity, identity).Serialize(CommandHeader.Yes);
                var tlsResponse = await this.networkClient.SendRequestAsync(requestCommand, Transport.TCP);
                AssertOneItem(tlsResponse);

                    response.Result = tlsResponse[0].CommandData.DeserializeStringCore();
                    response.SetSuccess();
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                response.SetError(e);
            }
            return response;
        }

        public async Task<Response<XIdentity>> GetIdentityAsync(string contactId)
        {
            

            var response = new Response<XIdentity>();
            try
            {
                var requestCommand = new RequestCommand(CommandId.GetIdentity, contactId).Serialize(CommandHeader.Yes);
                var tlsResponse = await this.networkClient.SendRequestAsync(requestCommand, Transport.TCP);
                AssertOneItem(tlsResponse);

                response.Result = tlsResponse[0].CommandData.DeserializeXIdentityCore();
                response.SetSuccess();

            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                response.SetError(e);
            }
            return response;
        }



        public void ReceiveAnyNewsResponse(byte isAnyNews)
        {
            

            this.chatWorker.ReceiveCheckForMessagesReply(isAnyNews);
        }


    }
}
