using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using System;

namespace holonsoft.FastProtocolConverter.Test.dto
{
	/// <summary>
	/// Every supported type, declared as fields. Paired with <see cref="AllTypesPropertyPoco"/>, which
	/// is the same protocol declared as properties. The two must produce byte identical frames, which
	/// is the real proof that property support changed nothing about the wire format.
	///
	/// Layout: sbyte 0, byte 1, bool 2, short 3, ushort 5, int 7, uint 11, long 15, ulong 23,
	/// float 31, double 35, decimal 43, Guid 59, DateTime 75, enum 79, string 83, total 91.
	/// </summary>
	public class AllTypesFieldPoco
	{
		[ProtocolField(StartPos = 0)] public sbyte SByteValue;
		[ProtocolField(StartPos = 1)] public byte ByteValue;
		[ProtocolField(StartPos = 2)] public bool BoolValue;
		[ProtocolField(StartPos = 3)] public short ShortValue;
		[ProtocolField(StartPos = 5)] public ushort UShortValue;
		[ProtocolField(StartPos = 7)] public int IntValue;
		[ProtocolField(StartPos = 11)] public uint UIntValue;
		[ProtocolField(StartPos = 15)] public long LongValue;
		[ProtocolField(StartPos = 23)] public ulong ULongValue;
		[ProtocolField(StartPos = 31)] public float FloatValue;
		[ProtocolField(StartPos = 35)] public double DoubleValue;
		[ProtocolField(StartPos = 43)] public decimal DecimalValue;
		[ProtocolField(StartPos = 59)] public Guid GuidValue;

		[ProtocolField(StartPos = 75)]
		[ProtocolDateTimeField(DateTimeKind = DateTimeKind.Utc, DateTimeByteFormat = DateTimeByteFormat.UnixTimeStamp32Bit)]
		public DateTime DateTimeValue;

		[ProtocolField(StartPos = 79, TypeInByteArray = DestinationType.Int32)]
		public MyImportantEnum EnumValue;

		[ProtocolField(StartPos = 83)]
		[ProtocolStringField(FillupCharWhenShorter = 'x', StringMaxLengthInByteArray = 8, Encoder = SupportedEncoder.ASCIIEncoder)]
		public string StringValue;
	}


	/// <summary>
	/// <see cref="AllTypesFieldPoco"/> declared as properties instead of fields, same layout.
	/// </summary>
	public class AllTypesPropertyPoco
	{
		[ProtocolField(StartPos = 0)] public sbyte SByteValue { get; set; }
		[ProtocolField(StartPos = 1)] public byte ByteValue { get; set; }
		[ProtocolField(StartPos = 2)] public bool BoolValue { get; set; }
		[ProtocolField(StartPos = 3)] public short ShortValue { get; set; }
		[ProtocolField(StartPos = 5)] public ushort UShortValue { get; set; }
		[ProtocolField(StartPos = 7)] public int IntValue { get; set; }
		[ProtocolField(StartPos = 11)] public uint UIntValue { get; set; }
		[ProtocolField(StartPos = 15)] public long LongValue { get; set; }
		[ProtocolField(StartPos = 23)] public ulong ULongValue { get; set; }
		[ProtocolField(StartPos = 31)] public float FloatValue { get; set; }
		[ProtocolField(StartPos = 35)] public double DoubleValue { get; set; }
		[ProtocolField(StartPos = 43)] public decimal DecimalValue { get; set; }
		[ProtocolField(StartPos = 59)] public Guid GuidValue { get; set; }

		[ProtocolField(StartPos = 75)]
		[ProtocolDateTimeField(DateTimeKind = DateTimeKind.Utc, DateTimeByteFormat = DateTimeByteFormat.UnixTimeStamp32Bit)]
		public DateTime DateTimeValue { get; set; }

		[ProtocolField(StartPos = 79, TypeInByteArray = DestinationType.Int32)]
		public MyImportantEnum EnumValue { get; set; }

		[ProtocolField(StartPos = 83)]
		[ProtocolStringField(FillupCharWhenShorter = 'x', StringMaxLengthInByteArray = 8, Encoder = SupportedEncoder.ASCIIEncoder)]
		public string StringValue { get; set; }
	}


	/// <summary>
	/// Same protocol again, big endian, to prove the byte order path does not care either.
	/// </summary>
	[ProtocolSetupArgument(UseBigEndian = true)]
	public class AllTypesPropertyBigEndianPoco
	{
		[ProtocolField(StartPos = 0)] public short ShortValue { get; set; }
		[ProtocolField(StartPos = 2)] public int IntValue { get; set; }
		[ProtocolField(StartPos = 6)] public long LongValue { get; set; }
		[ProtocolField(StartPos = 14)] public double DoubleValue { get; set; }
		[ProtocolField(StartPos = 22)] public Guid GuidValue { get; set; }
	}


	[ProtocolSetupArgument(UseBigEndian = true)]
	public class AllTypesFieldBigEndianPoco
	{
		[ProtocolField(StartPos = 0)] public short ShortValue;
		[ProtocolField(StartPos = 2)] public int IntValue;
		[ProtocolField(StartPos = 6)] public long LongValue;
		[ProtocolField(StartPos = 14)] public double DoubleValue;
		[ProtocolField(StartPos = 22)] public Guid GuidValue;
	}


	/// <summary>
	/// Fields and properties side by side in one protocol, which is what a POCO looks like when it
	/// grows over time.
	/// </summary>
	public class MixedFieldAndPropertyPoco
	{
		[ProtocolField(StartPos = 0)] public int FromAField;
		[ProtocolField(StartPos = 4)] public int FromAProperty { get; set; }
		[ProtocolField(StartPos = 8)] public short AlsoAField;
		[ProtocolField(StartPos = 10)] public short AlsoAProperty { get; set; }
	}


	/// <summary>
	/// A setter the outside world cannot call is still assignable through the compiled accessor, so
	/// an immutable looking POCO can be filled from a frame.
	/// </summary>
	public class InitAndPrivateSetterPoco
	{
		[ProtocolField(StartPos = 0)] public int InitOnly { get; init; }
		[ProtocolField(StartPos = 4)] public int PrivateSetter { get; private set; }

		public void SetPrivate(int value) => PrivateSetter = value;
	}


	/// <summary>
	/// The members the discovery has to ignore rather than choke on: a computed property, an indexer
	/// and a static property, none of which carry the protocol attribute.
	/// </summary>
	public class PocoWithUnrelatedMembers
	{
		[ProtocolField(StartPos = 0)] public int Real { get; set; }
		[ProtocolField(StartPos = 4)] public int AlsoReal;

		public int Computed => Real * 2;

		public static int Ambient { get; set; }

		public int this[int index] => index;
	}


	/// <summary>
	/// A get only property that claims to be part of the protocol. The converter could never fill it
	/// when reading a frame, so Prepare has to say so.
	/// </summary>
	public class GetOnlyProtocolPropertyPoco
	{
		[ProtocolField(StartPos = 0)] public int Real { get; set; }

		[ProtocolField(StartPos = 4)] public int Computed => 42;
	}


	/// <summary>
	/// The same get only property, but explicitly excluded, which has to be accepted.
	/// </summary>
	public class GetOnlyIgnoredPropertyPoco
	{
		[ProtocolField(StartPos = 0)] public int Real { get; set; }

		[ProtocolField(IgnoreField = true)] public int Computed => 42;
	}


	/// <summary>
	/// A bool property, because Marshal.SizeOf(typeof(bool)) is 4 and a bool occupies exactly one
	/// byte in a frame. The trailing field only moves if that is wrong.
	/// </summary>
	public class BoolPropertyPoco
	{
		[ProtocolField(StartPos = 0)] public bool First { get; set; }
		[ProtocolField(StartPos = 1)] public bool Second { get; set; }
		[ProtocolField(StartPos = 2)] public int Trailer { get; set; }
	}


	/// <summary>
	/// Range limits declared on properties, so the violation handler reports a PropertyInfo.
	/// </summary>
	public class RangeOnPropertyPoco
	{
		[ProtocolField(StartPos = 0)]
		[ProtocolFieldRange(MinValue = "-100", MaxValue = "100", DefaultValue = "7")]
		public int Value { get; set; }

		[ProtocolField(StartPos = 4)]
		[ProtocolFieldRange(MinValue = "-1.5", MaxValue = "1.5", DefaultValue = "0.5")]
		public float Ratio { get; set; }
	}


	/// <summary>
	/// A sequence protocol built from properties, including a variable length string whose length
	/// travels in a property of its own.
	/// </summary>
	public class SequencePropertyPoco
	{
		[ProtocolField(StartPos = 0, SequenceNo = 1)]
		public int TextLength { get; set; }

		[ProtocolField(StartPos = -1, SequenceNo = 2)]
		[ProtocolStringField(LengthFieldName = "TextLength")]
		public string Text { get; set; }

		[ProtocolField(StartPos = -1, SequenceNo = 3)]
		public int Trailer { get; set; }
	}
}
