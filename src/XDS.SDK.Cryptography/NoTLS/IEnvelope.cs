namespace XDS.SDK.Cryptography.NoTLS
{
    public interface IEnvelope
    {
	    byte[] EncipheredPayload { get; }
    }
}
