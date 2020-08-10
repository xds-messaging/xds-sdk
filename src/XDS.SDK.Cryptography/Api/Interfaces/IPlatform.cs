using XDS.SDK.Cryptography.Api.Implementations;

namespace XDS.SDK.Cryptography.Api.Interfaces
{
	public interface IPlatform
	{
		byte[] GenerateRandomBytes(int length);

		byte[] ComputeSHA512(byte[] data);

        byte[] ComputeSHA512(byte[] data, int offset, int count);

		byte[] ComputeSHA256(byte[] data);

        byte[] ComputeAESRound(AESDir aesDir, byte[] currentIV, byte[] inputData, byte[] keyBytes);

	}
}