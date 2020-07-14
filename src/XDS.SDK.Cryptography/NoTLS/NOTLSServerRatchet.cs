using XDS.SDK.Cryptography.Api.Infrastructure;

namespace XDS.SDK.Cryptography.NoTLS
{
    public class NOTLSServerRatchet
    {
		public NOTLSEnvelope EncryptRequest(byte[] clearPacket)
	    {
		    Guard.NotNull(clearPacket);
		    return new NOTLSEnvelope(clearPacket, clearPacket.Length);
	    }

	    public NOTLSRequest DecryptRequest(IEnvelope tlsEnvelope)
	    {
		    Guard.NotNull(tlsEnvelope);
		    var ar = new NOTLSRequest
		    {
			    CommandData = tlsEnvelope.EncipheredPayload,
			    IsAuthenticated = false,
			    UserId = "Anonym. N."
		    };
		    return ar;
	    }
	}
}