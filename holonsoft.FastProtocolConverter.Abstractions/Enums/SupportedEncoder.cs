using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Enums
{
	// Defines the Encoder for string conversions
	[Flags]
	public enum SupportedEncoder
	{
	    None = 0x00,

	    Default = 0x01,
	    ASCIIEncoder = 0x01,

	    UnicodeEncoder = Default << 1,
	}
}