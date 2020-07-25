using System.Numerics;
using XDS.SDK.Cryptography.Api.DataTypes;
using XDS.SDK.Cryptography.Api.Implementations;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Cryptography.ECC;

namespace XDS.SDK.Cryptography.Api.Interfaces
{
    public interface IXDSSecService
    {
        void Init(IPlatform platform, string name = "Default");

        string Name { get; }

        SymmetricKeyRepository SymmetricKeyRepository { get; }

        Response<QualifiedRandom> GetRandom(int randomLenght, byte[] seed = null);

        Response<QualifiedRandom> TestRandomNumberGeneration(int sampleSize, int randomLenght);

        Response<string> SuggestRandomPassword();
        
        Response<NormalizedPassword> NormalizePassword(string rawPassword);

        Response<KeyMaterial64> HashPassword(NormalizedPassword normalizedPassword);

        Response<CipherV2> Encrypt(Cleartext cleartext, KeyMaterial64 keyMaterial64, RoundsExponent roundsExponent, LongRunningOperationContext context);

        Response<Cleartext> Decrypt(CipherV2 cipherV2, KeyMaterial64 keyMaterial64, LongRunningOperationContext context);

        Response<XDSSecText> EncodeXDSSec(CipherV2 cipherV2);

        Response<CipherV2> DecodeXDSSec(string xdsSecText, LongRunningOperationContext context);

        Response<CipherV2> BinaryEncrypt(Clearbytes clearBytes, KeyMaterial64 keyMaterial64, RoundsExponent roundsExponent, LongRunningOperationContext context);
       
        Response<Clearbytes> BinaryDecrypt(CipherV2 cipherV2, KeyMaterial64 keyMaterial64, LongRunningOperationContext context);

        Response<CipherV2> BinaryDecodeXDSSec(byte[] xdsSecBytes, LongRunningOperationContext context);

        Response<byte[]> BinaryEncodeXDSSec(CipherV2 cipherV2, LongRunningOperationContext context);
       
        Response<ECKeyPair> GenerateCurve25519KeyPairExact(byte[] privateKey);

        byte[] CalculateAndHashSharedSecret(byte[] privateKey, byte[] publicKey);

        byte[] DefaultEncrypt(byte[] plaintextBytes, KeyMaterial64 keyMaterial64);

        byte[] DefaultDecrypt(byte[] cipherTextBytes, KeyMaterial64 keyMaterial64, LongRunningOperationContext context = null);

        BigInteger GetPositive520BitInteger();

        Response<byte[]> CombinePseudoRandomWithRandom(byte[] pseudoRandomBytes, int entropyLength);

        byte[] ComputeSHA512(byte[] data);
    }
}