using System;

namespace XDS.SDK.Cryptography.E2E
{
	public class E2ERatchetException : Exception
	{
		public E2ERatchetException() { }
		public E2ERatchetException(string message) : base(message) { }
		public E2ERatchetException(string message, Exception inner) : base(message, inner) { }
	}
}
