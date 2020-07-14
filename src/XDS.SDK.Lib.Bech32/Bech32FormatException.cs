using System;

namespace XDS.SDK.Lib.Bech32
{
	public class Bech32FormatException : FormatException
	{
		public Bech32FormatException(string message, int[] indexes) : base(message)
		{
            this.ErrorIndexes = indexes ?? throw new ArgumentNullException(nameof(indexes));

			Array.Sort(this.ErrorIndexes);
		}
		public int[] ErrorIndexes
		{
			get;
        }
	}
}