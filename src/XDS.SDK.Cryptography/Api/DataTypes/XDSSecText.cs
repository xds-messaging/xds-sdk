using System;

namespace XDS.SDK.Cryptography.Api.DataTypes
{
	public sealed class XDSSecText
	{
		/// <summary>
		/// Guaranteed to be non-null.
		/// </summary>
		public string Text
		{
			get { return this._text; }
		}

		readonly string _text;

		public XDSSecText(string text)
		{
			if (text == null)
				throw new ArgumentNullException("text");

			this._text = text;
		}
	}
}