using System;
using XDS.SDK.Cryptography.Api.DataTypes;
using XDS.SDK.Cryptography.Api.Infrastructure;

namespace XDS.SDK.Cryptography.Api.Implementations
{
	public class SymmetricKeyRepository
	{
		KeyMaterial64 _masterRandomKey;

		public KeyMaterial64 GetMasterRandomKey()
		{
			if (this._masterRandomKey == null)
				throw new InvalidOperationException("SymmetricKeyRepository: _masterRandomKey is null.");
			return this._masterRandomKey;
		}

		public void SetDeviceVaultRandomKey(KeyMaterial64 masterRandomKey)
		{
			if (masterRandomKey == null)
				throw new ArgumentNullException(nameof(masterRandomKey));
			this._masterRandomKey = masterRandomKey;
		}

		public void ClearMasterRandomKey()
		{
			if (this._masterRandomKey != null)
				this._masterRandomKey.GetBytes().FillWithZeros();
			this._masterRandomKey = null;
		}

	}
}