using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.SDK.Cryptography.Api.Interfaces;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Cryptography.TLS;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Workers
{
    public class TLSClient : INetworkClient
    {
        readonly IChatClient _chatClient;
        readonly IXDSSecService ixdsCryptoService;
        readonly ITcpConnection _tcp;
        readonly IUdpConnection _udp;
        readonly ILogger _log;
        readonly AppState _appState;
        TLSClientRatchet _r;

        public TLSClient(ILoggerFactory loggerFactory, IChatClient chatClient, IXDSSecService ixdsCryptoService, ITcpConnection tcpConnection, IUdpConnection udpConnection, AppState appState)
        {
            this._log = loggerFactory.CreateLogger<TLSClient>();
            this._chatClient = chatClient;
            this.ixdsCryptoService = ixdsCryptoService;
            this._tcp = tcpConnection;
            this._udp = udpConnection;
            this._appState = appState;
        }

        // public for tests only
        public TLSClientRatchet Ratchet
        {
            get
            {
                if (this._r != null)
                    return this._r;
                this._r = new TLSClientRatchet(this._chatClient.MyId,
                    this._chatClient.MyPrivateKey,
                    new TLSUser(HardCoded.Server0001, HardCoded.Server0001StaticPublicKey),
                this.ixdsCryptoService);
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

                var tlsEnvelope = new TLSEnvelope(rawRequest);

                if (!TLSEnvelopeExtensions.ValidateCrc32(rawRequest, out var actualCrc32))
                    throw new Exception($"TLSEnvelope CRC32 Error: Expected: {tlsEnvelope.Crc32}, actual: {actualCrc32}");

                var request = await this.Ratchet.DecryptRequest(tlsEnvelope);


                var command = request.ParseCommand();
                if (!command.CommandId.IsCommandDefined())
                    throw new Exception($"TLSClient: The command {command.CommandId} is not defined.");

                await ProcessCommand(command);
            }
            catch (Exception e)
            {
                var error = $"{nameof(TLSClient)} received bad request via {transport}: {e.Message}";
                this._log.LogError(error);
            }
            return null; // this goes back to the IUdpConnection and is not used there.
        }

        async Task ProcessCommand(Command command)
        {
            switch (command.CommandId)
            {
                case CommandId.AnyNews_Response:
                    this._chatClient.ReceiveAnyNewsResponse(command.CommandData.DeserializeByteCore());
                    return;
                case CommandId.NoSuchUser_Response:
                    this._appState.SetIsIdentityPublished(false);
                    return;
                case CommandId.LostDynamicKey_Response:
                    await this.Ratchet.Reset();
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
                    networkResponse = await this._tcp.SendRequestAsync(requestBytes).ConfigureAwait(false); // http://stackoverflow.com/a/19727627
                else
                {
                    networkResponse = await this._udp.SendRequestAsync(requestBytes);
                }

                if (transport == Transport.UDP && networkResponse.Count == 0)
                {
                    return response;
                }

                foreach (TLSEnvelope tlsEnvelope in networkResponse)
                {
                    TLSRequest tlsRequest = await this.Ratchet.DecryptRequest(tlsEnvelope);

                    if (!tlsRequest.IsAuthenticated)
                        throw new Exception("Authentication failed.");
                    this._log.LogDebug($"{tlsRequest.UserId} is authenticated.");

                    Command command = tlsRequest.ParseCommand();
                    if (!command.CommandId.IsCommandDefined())
                        throw new Exception($"Invalid Command {command.CommandId}");

                    if (command.CommandId == CommandId.LostDynamicKey_Response)
                    {
                        await this.Ratchet.Reset();
                    }
                    else if (command.CommandId == CommandId.NoSuchUser_Response)
                    {
                        this._appState.SetIsIdentityPublished(false);
                    }
                    tlsRequest.CommandData = command.CommandData; // minus the header
                    response.Add(tlsRequest);
                }
            }
            catch (Exception e)
            {
                this._log.LogError(e.Message);
                throw;
            }
            return response;

        }


    }
}
