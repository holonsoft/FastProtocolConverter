using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Enums
{
	/// <summary>
	/// Defines the destination type during conversion operations 
	/// </summary>
	[Flags]
	public enum DestinationType
	{
	    None = 0x00,

	    Default = 0x01,

	    Int16 = Default << 1,
	    Int32 = Default << 2,
	    Int64 = Default << 3,

	    UInt16 = Default << 4,
	    UInt32 = Default << 5,
	    UInt64 = Default << 6,

	    Single = Default << 7,
	    Double = Default << 8,

	    Byte = Default << 9,

			// very special treatment, one bye in source will be splitted up to 8 bool values in POCO
			Bits = Default << 10,
	}
}