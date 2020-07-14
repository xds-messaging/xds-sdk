using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.Messaging.SDK.ApplicationBehavior.Data;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;
using XDS.Messaging.SDK.ApplicationBehavior.ViewModels;
using XDS.Messaging.SDK.ApplicationBehavior.Workers;
using XDS.SDK.Cryptography.Api.DataTypes;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.Api.Interfaces;
using XDS.SDK.Cryptography.E2E;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
	public interface IChatEncryptionService
	{
		Task<Response> EncryptMessage(Message message, bool? isInitial = null);

		Task<Response> DecryptCipherTextInVisibleBubble(Message message);

		Task<Response<string>> PeekIntoUnreadTextMessageWithoutSideEffects(Message message);
	}

	public class PortableChatEncryptionService : IChatEncryptionService
    {
        readonly IDependencyInjection dependencyInjection;
        readonly ILogger logger;
		readonly IXDSSecService ixdsCryptoService;
        readonly  E2ERatchet e2ERatchet;
		readonly AppRepository repo;
        readonly ProfileViewModel profileViewModel;

		public PortableChatEncryptionService(IDependencyInjection dependencyInjection, ILoggerFactory loggerFactory, IXDSSecService ixdsCryptoService, AppRepository appRepository, ProfileViewModel profileViewModel, E2ERatchet e2ERatchet)
        {
            this.logger = loggerFactory.CreateLogger<PortableChatEncryptionService>();
			this.ixdsCryptoService = ixdsCryptoService;
			this.repo = appRepository;
            this.profileViewModel = profileViewModel;
            this.e2ERatchet = e2ERatchet;
        }

        ChatWorker ChatWorker => this.dependencyInjection.ServiceProvider.Get<ChatWorker>(); // otherwise circular

        public async Task<Response> EncryptMessage(Message message, bool? isInitial = null)
		{
			var response = new Response();
			try
			{
				var roundsExponent = new RoundsExponent(RoundsExponent.DontMakeRounds);


				var initialKeyResult = await this.e2ERatchet.GetE2EEncryptionKeyCommonAsync(message.RecipientId, isInitial);
				var keyMaterial = initialKeyResult.Item1;
				message.DynamicPublicKey = initialKeyResult.Item2;
				message.DynamicPublicKeyId = initialKeyResult.Item3;
				message.PrivateKeyHint = initialKeyResult.Item4;

				// I this is not null, either isInitial was true, or the ratchet was just initialized the first time for an added user's first message.
				var initialMetadataKeyMaterial = initialKeyResult.Item5;


				await Task.Run(() => EncryptWithStrategy(message, keyMaterial, roundsExponent, initialMetadataKeyMaterial));
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e.Message);
			}
			return response;

		}

		void EncryptWithStrategy(Message message, KeyMaterial64 keyMaterial, RoundsExponent roundsExponent, KeyMaterial64 initialMetadataKeyMaterial)
		{
			XMessageMetaData messageMetadata = new XMessageMetaData { MessageType = message.MessageType };

			switch (message.MessageType)
			{
				case MessageType.Text:
					message.TextCipher = EncryptTextToBytes(message.ThreadText, keyMaterial, roundsExponent);
					messageMetadata.SenderLocalMessageId = int.Parse(message.Id);
					break;
				case MessageType.Media:
					message.ImageCipher = this.ixdsCryptoService.DefaultEncrypt(message.ThreadMedia, keyMaterial);
					messageMetadata.SenderLocalMessageId = int.Parse(message.Id);
					break;
				case MessageType.TextAndMedia:
				case MessageType.File:
					message.TextCipher = EncryptTextToBytes(message.ThreadText, keyMaterial, roundsExponent);
					message.ImageCipher = this.ixdsCryptoService.DefaultEncrypt(message.ThreadMedia, keyMaterial);
					messageMetadata.SenderLocalMessageId = int.Parse(message.Id);
					break;
				case MessageType.DeliveryReceipt:
				case MessageType.ReadReceipt:
					messageMetadata.SenderLocalMessageId = int.Parse(message.SenderLocalMessageId);
					break;
				default:
					throw new Exception("Invalid MessageType");
			}

			if (initialMetadataKeyMaterial != null) // initialMetadataKeyMaterial is only for resend requests
			{
				messageMetadata.SenderPublicKey = this.profileViewModel.PublicKey;
				message.MetaCipher = this.ixdsCryptoService.DefaultEncrypt(messageMetadata.SerializeCore(), initialMetadataKeyMaterial);
			}
			else
			{
				messageMetadata.SenderPublicKey = this.ixdsCryptoService.GetRandom(32).Result.X;
				message.MetaCipher = this.ixdsCryptoService.DefaultEncrypt(messageMetadata.SerializeCore(), keyMaterial);
			}

			if (!message.MessageType.IsReceipt())
				message.EncryptedE2EEncryptionKey = this.ixdsCryptoService.DefaultEncrypt(keyMaterial.GetBytes(), this.ixdsCryptoService.SymmetricKeyRepository.GetMasterRandomKey());
		}

		public async Task<Response<string>> PeekIntoUnreadTextMessageWithoutSideEffects(Message message)
		{
			var response = new Response<string>();
			if (message.MessageType == MessageType.Text && message.LocalMessageState == LocalMessageState.JustReceived)
			{

				GetE2EDecryptionKeyResult getE2EDecryptionKeyResult = await this.e2ERatchet.GetEndToEndDecryptionKeyAsync(message.SenderId, message.DynamicPublicKey, message.PrivateKeyHint);
				if (getE2EDecryptionKeyResult.E2EDecryptionKeyType != E2EDecryptionKeyType.UnavailableDynamicPrivateKey)
				{
					response.Result = DecryptBytesToText(message.TextCipher, getE2EDecryptionKeyResult.E2EDecryptionKeyMaterial);
					response.SetSuccess();
				}
				else
				{
					message.LocalMessageState = LocalMessageState.RatchetMismatchError;
					response.SetError(nameof(LocalMessageState.RatchetMismatchError));
				}
			}
			return response;
		}

		public async Task<Response> DecryptCipherTextInVisibleBubble(Message message)
		{
			var response = new Response();
			try
			{
				if (message.ThreadText != null && message.MessageType == MessageType.Text) // TODO: do this properly ....nothing to do, already decrypted
				{
					response.SetSuccess();
					return response;
				}

				KeyMaterial64 decryptionkey;
				if (message.LocalMessageState == LocalMessageState.JustReceived) // this is an incoming message
				{

					GetE2EDecryptionKeyResult getE2EDecryptionKeyResult = await this.e2ERatchet.GetEndToEndDecryptionKeyAsync(message.SenderId, message.DynamicPublicKey, message.PrivateKeyHint);
					if (getE2EDecryptionKeyResult.E2EDecryptionKeyType != E2EDecryptionKeyType.UnavailableDynamicPrivateKey)
					{
						decryptionkey = getE2EDecryptionKeyResult.E2EDecryptionKeyMaterial;
					}
					else
					{
						message.LocalMessageState = LocalMessageState.RatchetMismatchError;
						// When adding a contact and sending the first message to him, the contact can immediately read the message,
						// because the message is encrypted with the static public key of that contact.
						// But when one side deletes the contact, and the contact sends a message again, that's not possible,
						// because the sender dosn't know he would need to use only the static public key.
						// Instead, he's also use the last dynamic key, expecting the other side has matching material.
						// Then, we land here. A recovery would require that we ask the other side to resend the message.
						// Instead, just a delivery receipt is sent automatically, which repairs the ratchet.
						// The problem is, the other side does not know 
						message.LocalMessageState = LocalMessageState.RatchetMismatchError; // create anothe enum member for this case?
						await this.repo.UpdateMessage(message);
						response.SetError(nameof(LocalMessageState.RatchetMismatchError));
						return response;
					}

					await Task.Run(() => DecryptToCache(message, decryptionkey));
					await this.e2ERatchet.SaveIncomingDynamicPublicKeyOnSuccessfulDecryptionAsync(message.SenderId, message.DynamicPublicKey, message.DynamicPublicKeyId);
					message.EncryptedE2EEncryptionKey = this.ixdsCryptoService.DefaultEncrypt(decryptionkey.GetBytes(), this.ixdsCryptoService.SymmetricKeyRepository.GetMasterRandomKey());
					message.LocalMessageState = LocalMessageState.Integrated;

					Debug.Assert(message.MessageType != MessageType.ReadReceipt && message.MessageType != MessageType.DeliveryReceipt);

					await this.repo.UpdateMessage(message);
					await this.ChatWorker.SendReceipt(message, MessageType.ReadReceipt);
				}
				else // this is a message we have stored locally
				{
					if (message.EncryptedE2EEncryptionKey == null)
						message.LocalMessageState = LocalMessageState.LocalDecryptionError;
					else
					{
						decryptionkey = new KeyMaterial64(this.ixdsCryptoService.DefaultDecrypt(message.EncryptedE2EEncryptionKey, this.ixdsCryptoService.SymmetricKeyRepository.GetMasterRandomKey()));
						await Task.Run(() => DecryptToCache(message, decryptionkey));
					}
					// should we check that a read receipt has really been sent and retry till we are sure?

				}
				response.SetSuccess();
			}
			catch (Exception e)
			{
				response.SetError(e.Message);
				this.logger.LogError(e.Message);
			}
			return response;
		}



		byte[] EncryptTextToBytes(string clearText, KeyMaterial64 keyMaterial64, RoundsExponent roundsExponent)
		{
			var encryptResponse = this.ixdsCryptoService.Encrypt(new Cleartext(clearText), keyMaterial64, roundsExponent, null);
			if (!encryptResponse.IsSuccess)
				throw new InvalidOperationException(encryptResponse.Error);
			var encodeResponse = this.ixdsCryptoService.BinaryEncodeXDSSec(encryptResponse.Result, null);
			if (!encodeResponse.IsSuccess)
				throw new InvalidOperationException(encodeResponse.Error);
			return encodeResponse.Result;
		}






		void DecryptToCache(Message message, KeyMaterial64 keyMaterial64)
		{
			switch (message.MessageType)
			{
				case MessageType.Text:
				case MessageType.File:
					message.ThreadText = DecryptBytesToText(message.TextCipher, keyMaterial64);
					break;
				case MessageType.Media:
					message.ThreadMedia = this.ixdsCryptoService.DefaultDecrypt(message.ImageCipher, keyMaterial64);
					break;
				case MessageType.TextAndMedia:
					message.ThreadText = DecryptBytesToText(message.TextCipher, keyMaterial64);
					message.ThreadMedia = this.ixdsCryptoService.DefaultDecrypt(message.ImageCipher, keyMaterial64);
					break;
				default:
					throw new Exception("Invalid MessageType.");
			}
		}

		// same in ChatWorker
		string DecryptBytesToText(byte[] cipherBytes, KeyMaterial64 keyMaterial64)
		{

			var decodeResponse = this.ixdsCryptoService.BinaryDecodeXDSSec(cipherBytes, null);
			if (!decodeResponse.IsSuccess)
				throw new Exception(decodeResponse.Error);
			var decrpytResponse = this.ixdsCryptoService.Decrypt(decodeResponse.Result, keyMaterial64, null);
			if (!decrpytResponse.IsSuccess)
				throw new Exception(decrpytResponse.Error);
			Cleartext cleartext = decrpytResponse.Result;
			return cleartext.Text;
		}
	}
}