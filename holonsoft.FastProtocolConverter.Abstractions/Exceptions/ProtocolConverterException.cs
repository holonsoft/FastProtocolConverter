using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Exceptions
{
	/// <summary>
	/// Defines a converter specific exception
	/// Mostly used if range violations are detected (and the proper behaviour is defined)
	/// </summary>
	public class ProtocolConverterException : Exception
	{
		public ProtocolConverterException()
		{
		}

		public ProtocolConverterException(string message) : base(message)
		{
		}

		public ProtocolConverterException(string message, Exception innerException) : base(message, innerException)
		{
		}

	}
}