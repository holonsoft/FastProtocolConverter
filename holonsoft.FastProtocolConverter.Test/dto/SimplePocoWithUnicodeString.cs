using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;

namespace holonsoft.FastProtocolConverter.Test.dto
{
	/// <summary>
	/// Counterpart of <see cref="SimplePocoWithGapAndString"/>, but with the unicode encoder.
	/// Used to prove that <see cref="SupportedEncoder.UnicodeEncoder"/> reserves two bytes per character.
	/// </summary>
	public class SimplePocoWithUnicodeString
	{
		[ProtocolField(StartPos = 0, TypeInByteArray = DestinationType.UInt32)]
		public uint JobId;

		[ProtocolField(StartPos = 4, TypeInByteArray = DestinationType.None)]
		[ProtocolStringField(FillupCharWhenShorter = '\0', StringMaxLengthInByteArray = 64, Encoder = SupportedEncoder.UnicodeEncoder)]
		public string SomeImportantCode;
	}
}
