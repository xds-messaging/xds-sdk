using System.Collections.Generic;
using System.Threading.Tasks;
using XDS.Messaging.SDK.ApplicationBehavior.Models.Chat;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Workers
{
    public interface IChatClient
    {
        void Init(string myId, byte[] myPrivateKey);

        string MyId { get; }
        byte[] MyPrivateKey { get; }


        // UDP - Fire and forget
        Task<Response<byte>> AnyNews(string myId);

	    Task<Response<byte>> CheckForResendRequest(XResendRequest resendRequest);

		void ReceiveAnyNewsResponse(byte messageCount);

        Task<Response<IReadOnlyCollection<XMessage>>> DownloadMessages(string myId);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="identity"></param>
        /// <returns>Returns the IdentityID that was successfilly registered.</returns>
        Task<Response<string>> PublishIdentityAsync(XIdentity identity);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contactId"></param>
        /// <returns>The Identity of the contactID.</returns>
        Task<Response<XIdentity>> GetIdentityAsync(string contactId);

        /// <summary>
        /// Uploads a message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>A string in the format [messageId];[recipientId]</returns>
        Task<Response<NetworkPayloadAdded>> UploadMessage(Message message);

	    /// <summary>
	    /// Uploads a resend request.
	    /// </summary>
	    /// <param name="resendRequest">The XResendRequest containing only the NetworkPayloadHash of the message that the other party should resend.</param>
	    /// <returns>The NetworkPayloadHash GuidString, to indicate success.</returns>
		Task<Response<NetworkPayloadAdded>> UploadResendRequest(XResendRequest resendRequest);

		
	}
}