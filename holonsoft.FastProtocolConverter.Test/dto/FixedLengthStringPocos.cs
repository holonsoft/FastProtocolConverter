using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;

namespace holonsoft.FastProtocolConverter.Test.dto
{
	/// <summary>
	/// Fixed length string of 8 bytes with a one byte fill character, so the padding divides the
	/// field exactly. This is the easy case and the one every existing test POCO happens to use.
	/// </summary>
	public class AsciiFixedStringPoco
	{
		[ProtocolField(StartPos = 0)]
		public int Header;

		[ProtocolField(StartPos = 4)]
		[ProtocolStringField(FillupCharWhenShorter = 'Z', StringMaxLengthInByteArray = 8, Encoder = SupportedEncoder.ASCIIEncoder)]
		public string Text;

		[ProtocolField(StartPos = 12)]
		public int Trailer;
	}


	/// <summary>
	/// Fixed length string of 10 bytes with the unicode encoder, so every character costs two bytes
	/// and so does the fill character. 10 divides by 2, so the padding still fits exactly.
	/// </summary>
	public class UnicodeFixedStringPoco
	{
		[ProtocolField(StartPos = 0)]
		public int Header;

		[ProtocolField(StartPos = 4)]
		[ProtocolStringField(FillupCharWhenShorter = '\0', StringMaxLengthInByteArray = 10, Encoder = SupportedEncoder.UnicodeEncoder)]
		public string Text;

		[ProtocolField(StartPos = 14)]
		public int Trailer;
	}


	/// <summary>
	/// The awkward one: a unicode field whose declared length is **odd**, so the two byte fill
	/// character can never land on the field boundary exactly. Nothing stops a protocol from
	/// declaring this, so the converter has to have an answer for it.
	/// </summary>
	public class UnicodeOddLengthStringPoco
	{
		[ProtocolField(StartPos = 0)]
		public int Header;

		[ProtocolField(StartPos = 4)]
		[ProtocolStringField(FillupCharWhenShorter = '\0', StringMaxLengthInByteArray = 11, Encoder = SupportedEncoder.UnicodeEncoder)]
		public string Text;

		[ProtocolField(StartPos = 15)]
		public int Trailer;
	}
}
