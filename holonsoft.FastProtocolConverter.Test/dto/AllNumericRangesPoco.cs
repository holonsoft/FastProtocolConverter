using holonsoft.FastProtocolConverter.Abstractions.Attributes;

namespace holonsoft.FastProtocolConverter.Test.dto
{
	/// <summary>
	/// Every numeric type that supports a range, each with its own limits and default. Used to prove
	/// that all five ConverterRangeViolationBehaviour values behave identically across the type
	/// families, which a single test on a single int field cannot show.
	///
	/// Layout: sbyte 0, short 1, ushort 3, int 5, uint 9, long 13, ulong 21, float 29, double 33,
	/// decimal 41, total 57 bytes.
	/// </summary>
	public class AllNumericRangesPoco
	{
		[ProtocolField(StartPos = 0)]
		[ProtocolFieldRange(MinValue = "-10", MaxValue = "10", DefaultValue = "7")]
		public sbyte SByteField;

		[ProtocolField(StartPos = 1)]
		[ProtocolFieldRange(MinValue = "-1000", MaxValue = "1000", DefaultValue = "700")]
		public short ShortField;

		[ProtocolField(StartPos = 3)]
		[ProtocolFieldRange(MinValue = "10", MaxValue = "1000", DefaultValue = "700")]
		public ushort UShortField;

		[ProtocolField(StartPos = 5)]
		[ProtocolFieldRange(MinValue = "-100000", MaxValue = "100000", DefaultValue = "70000")]
		public int IntField;

		[ProtocolField(StartPos = 9)]
		[ProtocolFieldRange(MinValue = "10", MaxValue = "100000", DefaultValue = "70000")]
		public uint UIntField;

		[ProtocolField(StartPos = 13)]
		[ProtocolFieldRange(MinValue = "-10000000000", MaxValue = "10000000000", DefaultValue = "7000000000")]
		public long LongField;

		[ProtocolField(StartPos = 21)]
		[ProtocolFieldRange(MinValue = "10", MaxValue = "10000000000", DefaultValue = "7000000000")]
		public ulong ULongField;

		[ProtocolField(StartPos = 29)]
		[ProtocolFieldRange(MinValue = "-1.5", MaxValue = "1.5", DefaultValue = "0.5")]
		public float FloatField;

		[ProtocolField(StartPos = 33)]
		[ProtocolFieldRange(MinValue = "-2.5", MaxValue = "2.5", DefaultValue = "0.5")]
		public double DoubleField;

		[ProtocolField(StartPos = 41)]
		[ProtocolFieldRange(MinValue = "-3.5", MaxValue = "3.5", DefaultValue = "0.5")]
		public decimal DecimalField;
	}


	/// <summary>
	/// Same as <see cref="AllNumericRangesPoco"/> but big endian, so the range handling is proven
	/// for both byte orders.
	/// </summary>
	[ProtocolSetupArgument(UseBigEndian = true)]
	public class AllNumericRangesBigEndianPoco
	{
		[ProtocolField(StartPos = 0)]
		[ProtocolFieldRange(MinValue = "-10", MaxValue = "10", DefaultValue = "7")]
		public sbyte SByteField;

		[ProtocolField(StartPos = 1)]
		[ProtocolFieldRange(MinValue = "-1000", MaxValue = "1000", DefaultValue = "700")]
		public short ShortField;

		[ProtocolField(StartPos = 3)]
		[ProtocolFieldRange(MinValue = "10", MaxValue = "1000", DefaultValue = "700")]
		public ushort UShortField;

		[ProtocolField(StartPos = 5)]
		[ProtocolFieldRange(MinValue = "-100000", MaxValue = "100000", DefaultValue = "70000")]
		public int IntField;

		[ProtocolField(StartPos = 9)]
		[ProtocolFieldRange(MinValue = "10", MaxValue = "100000", DefaultValue = "70000")]
		public uint UIntField;

		[ProtocolField(StartPos = 13)]
		[ProtocolFieldRange(MinValue = "-10000000000", MaxValue = "10000000000", DefaultValue = "7000000000")]
		public long LongField;

		[ProtocolField(StartPos = 21)]
		[ProtocolFieldRange(MinValue = "10", MaxValue = "10000000000", DefaultValue = "7000000000")]
		public ulong ULongField;

		[ProtocolField(StartPos = 29)]
		[ProtocolFieldRange(MinValue = "-1.5", MaxValue = "1.5", DefaultValue = "0.5")]
		public float FloatField;

		[ProtocolField(StartPos = 33)]
		[ProtocolFieldRange(MinValue = "-2.5", MaxValue = "2.5", DefaultValue = "0.5")]
		public double DoubleField;

		[ProtocolField(StartPos = 41)]
		[ProtocolFieldRange(MinValue = "-3.5", MaxValue = "3.5", DefaultValue = "0.5")]
		public decimal DecimalField;
	}
}
