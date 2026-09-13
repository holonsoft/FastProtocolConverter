using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;

namespace holonsoft.FastProtocolConverter.Test.dto
{
	/// <summary>
	/// Carries an enum and a fixed length string, the two conversions that used shared mutable
	/// state on the converter. Used to prove that one prepared converter can serve many threads.
	/// </summary>
	public class ConcurrencyPoco
	{
		[ProtocolField(StartPos = 0)]
		public int Id;

		[ProtocolField(StartPos = 4, TypeInByteArray = DestinationType.Int32)]
		public MyImportantEnum EnumField;

		[ProtocolField(StartPos = 8, TypeInByteArray = DestinationType.None)]
		[ProtocolStringField(FillupCharWhenShorter = '\0', StringMaxLengthInByteArray = 16, Encoder = SupportedEncoder.ASCIIEncoder)]
		public string Code;
	}
}
