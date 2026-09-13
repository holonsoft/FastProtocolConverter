using holonsoft.FastProtocolConverter.Abstractions.Attributes;

namespace holonsoft.FastProtocolConverter.Test.dto
{
	/// <summary>
	/// Little endian layout for the decimal and sbyte support added in v4.
	/// sbyte occupies one byte, decimal occupies 16 bytes (four component integers).
	/// </summary>
	public class DecimalAndSBytePoco
	{
		[ProtocolField(StartPos = 0)]
		public sbyte SByteField;

		[ProtocolField(StartPos = 1)]
		public decimal DecimalField;

		[ProtocolField(StartPos = 17)]
		public int TrailingField;
	}


	/// <summary>
	/// Same layout as <see cref="DecimalAndSBytePoco"/>, but big endian.
	/// </summary>
	[ProtocolSetupArgument(UseBigEndian = true)]
	public class DecimalAndSBytePocoBigEndian
	{
		[ProtocolField(StartPos = 0)]
		public sbyte SByteField;

		[ProtocolField(StartPos = 1)]
		public decimal DecimalField;

		[ProtocolField(StartPos = 17)]
		public int TrailingField;
	}


	/// <summary>
	/// Carries range definitions so the range violation behaviour can be exercised
	/// for the two newly supported types.
	/// </summary>
	public class DecimalAndSByteWithRangesPoco
	{
		[ProtocolField(StartPos = 0)]
		[ProtocolFieldRange(MinValue = "-10", MaxValue = "10", DefaultValue = "7")]
		public sbyte SByteField;

		[ProtocolField(StartPos = 1)]
		[ProtocolFieldRange(MinValue = "-100.5", MaxValue = "100.5", DefaultValue = "42.25")]
		public decimal DecimalField;
	}
}
