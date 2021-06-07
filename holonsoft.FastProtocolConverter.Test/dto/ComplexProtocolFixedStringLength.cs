using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;

namespace holonsoft.FastProtocolConverter.Test.dto
{
	/// <summary>
	/// Example for FIXED LENGTH STRING fields
	/// </summary>
	public class ComplexProtocolFixedStringLength
	{
		// will be filled to 10 chars (ascii)
		[ProtocolField(StartPos = 0, SequenceNo = 1)]
		[ProtocolStringField(LengthFieldName = "", FillupCharWhenShorter = 'Z', StringMaxLengthInByteArray = 10, Encoder = SupportedEncoder.ASCIIEncoder)]
		public string StrField1;

		// will be shortened to 10 chars (ascii)
		[ProtocolField(StartPos = -1, SequenceNo = 2)]
		[ProtocolStringField(LengthFieldName = "", StringMaxLengthInByteArray = 10, Encoder = SupportedEncoder.ASCIIEncoder)]
		public string StrField2;
	}
}
