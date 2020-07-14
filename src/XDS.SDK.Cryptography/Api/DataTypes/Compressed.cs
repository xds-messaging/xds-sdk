namespace XDS.SDK.Cryptography.Api.DataTypes
{
	public sealed class Compressed : SecureBytes
	{
		public Compressed(byte[] data) : base(data)
		{
			// perform datatype-specific validation here
		}
	}
}