using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Workers
{
    public class NoTLSClient : INetworkClient
    {
        readonly ILogger logger;
        readonly IChatClient chatClient;
        readonly ITcpConnection tcp;
        readonly IUdpConnection udp;
        readonly AppState appState;
        NOTLSClientRatchet _r;

        public NoTLSClient(ILoggerFactory loggerFactory, IChatClient chatClient, ITcpConnection tcpConnection, IUdpConnection udpConnection, AppState appState)
        {
            this.logger = loggerFactory.CreateLogger<NoTLSClient>();
            this.chatClient = chatClient;
            this.tcp = tcpConnection;
            this.udp = udpConnection;
            this.appState = appState;
        }

        // public for tests only
        public NOTLSClientRatchet Ratchet
        {
            get
            {
                if (this._r != null)
                    return this._r;
                this._r = new NOTLSClientRatchet();
                return this._r;
            }
        }

        public async Task<string> Receive(byte[] rawRequest, Transport transport)
        {
            

            Debug.Assert(transport == Transport.UDP);

            try
            {
                if (rawRequest == null || rawRequest.Length == 0)
                    throw new Exception("TLSClient received null or empty packet");

                var tlsEnvelope = new NOTLSEnvelope(rawRequest);

                int actualCrc32;
                if (!NOTLSEnvelopeExtensions.ValidateCrc32(rawRequest, out actualCrc32))
                    throw new Exception($"TLSEnvelope CRC32 Error: Expected: {tlsEnvelope.Crc32}, actual: {actualCrc32}");

                var request = await this.Ratchet.DecryptRequest(tlsEnvelope);


                var command = request.ParseCommand();

                if (!command.CommandId.IsCommandDefined())
                    throw new Exception($"TLSClient: The command {command.CommandId} is not defined.");

                await ProcessCommand(command);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }
            return null; // this goes back to the IUdpConnection and is not used there.
        }

        async Task ProcessCommand(Command command)
        {
            switch (command.CommandId)
            {
                case CommandId.AnyNews_Response:
                    this.chatClient.ReceiveAnyNewsResponse(command.CommandData.DeserializeByteCore());
                    return;
                case CommandId.NoSuchUser_Response:
                    this.appState.SetIsIdentityPublished(false);
                    return;
                case CommandId.LostDynamicKey_Response:
                    this.logger.LogDebug("LostDynamicKey_Response is not applicable in NoTLS mode.");
                    return;
            }
        }

        public async Task<List<IRequestCommandData>> SendRequestAsync(byte[] request, Transport transport)
        {
            var response = new List<IRequestCommandData>();
            try
            {
                var tlsRequestEnvelope = await this.Ratchet.EncryptRequest(request);
                var requestBytes = tlsRequestEnvelope.Serialize();

                List<IEnvelope> networkResponse;
                if (transport == Transport.TCP)
                    networkResponse = await this.tcp.SendRequestAsync(requestBytes).ConfigureAwait(false); // http://stackoverflow.com/a/19727627
                else
                {
                    networkResponse = await this.udp.SendRequestAsync(requestBytes);
                }
                if (transport == Transport.UDP && networkResponse.Count == 0)
                {
                    return response;
                }

                foreach (IEnvelope tlsEnvelope in networkResponse)
                {
                    NOTLSRequest tlsRequest = await this.Ratchet.DecryptRequest(tlsEnvelope);

                    Debug.Assert(tlsRequest.IsAuthenticated == false);


                    Command command = tlsRequest.ParseCommand();

                    if (!command.CommandId.IsCommandDefined())
                        throw new Exception($"Invalid Command {command.CommandId}");

                    if (command.CommandId == CommandId.ServerException)
                        throw new Exception($"Server Exception: {command.CommandData.DeserializeStringCore()}");


                    if (command.CommandId == CommandId.NoSuchUser_Response)
                    {
                        this.appState.SetIsIdentityPublished(false);
                    }
                    tlsRequest.CommandData = command.CommandData; // minus the header
                    response.Add(tlsRequest);
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                throw;
            }
            return response;

        }


    }
}
