using System;
using XDS.SDK.Cryptography.Api.Infrastructure;

namespace XDS.SDK.Cryptography.Api.Interfaces
{
	public interface IEncryptionProgress : IProgress<EncryptionProgress>
	{
		int Percent { get; set; }

		string Message { get; set; }
	}
}