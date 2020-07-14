using System;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
	public static class RandomCapture
	{
		public const int BytesNeeded = 1024;
		static readonly string[] _hexStrings = new string[256];
		public static int BytesGenerated;
		public static byte[] GeneratedBytes = new byte[BytesNeeded];

		static RandomCapture()
		{
			CreateHexStrings();
		}

		static void CreateHexStrings()
		{
			for (var i = 0; i <= 255; i++)
				_hexStrings[i] = i.ToString("X2");
		}

		/// <summary>
		/// xPos and yPos should be in the range 0...255 or larger!
		/// </summary>
		public static CapturedData CaputureFromPointer(double xPos, double yPos)
		{
			byte tick = (byte)(DateTime.UtcNow.Ticks % 256);

			byte x = (byte)(xPos % 256.0);
			x ^= tick;
			GeneratedBytes[BytesGenerated++] = x;
			byte y = (byte)(yPos % 256.0);
			y ^= tick;
			GeneratedBytes[BytesGenerated++] = y;

			return new CapturedData(x, _hexStrings[y], $"{BytesGenerated * 100 / BytesNeeded } %", BytesGenerated * 100/BytesNeeded);
		}

		public static void Reset()
		{
			BytesGenerated = 0;
			GeneratedBytes = new byte[BytesNeeded];
		}


		/// <summary>
		/// Data for the Matrix.
		/// </summary>
		public sealed class CapturedData
		{
			public CapturedData(byte cellIndex, string cellHextext, string progress, int percent)
			{
				this.CellIndex = cellIndex; this.CellHexText = cellHextext; this.Progress = progress;
                this.Percent = percent;
            }
			/// <summary>
			/// from 0...255.
			/// </summary>
			public readonly byte CellIndex;

			/// <summary>
			/// e.g. F0
			/// </summary>
			public readonly string CellHexText;

			/// <summary>
			/// e.g. 12 %.
			/// </summary>
			public readonly string Progress;

            /// <summary>
            /// e.g. 0, 21, 100.
            /// </summary>
            public readonly int Percent;
		}
	}
}
