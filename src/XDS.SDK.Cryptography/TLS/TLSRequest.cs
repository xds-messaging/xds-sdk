using XDS.SDK.Cryptography.NoTLS;

namespace XDS.SDK.Cryptography.TLS
{
    public class TLSRequest : IRequestCommandData
	{
        public string UserId;
        public byte[] CommandData { get; set; }
        public bool IsAuthenticated;
    }
}
