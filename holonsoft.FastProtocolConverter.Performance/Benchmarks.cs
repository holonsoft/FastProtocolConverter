using BenchmarkDotNet.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using System;

namespace holonsoft.FastProtocolConverter.Performance
{
	/// <summary>
	/// Baseline for the v4 performance work. Measures both directions separately, because the read
	/// path uses a compiled setter while the write path still goes through FieldInfo.GetValue.
	/// Allocation numbers matter as much as time here: in a high rate signal environment the garbage
	/// produced per message drives the collection pressure.
	/// </summary>
	[MemoryDiagnoser]
	public class ConverterBenchmarks
	{
		private IProtocolConverter<BenchmarkPoco> _littleEndian;
		private IProtocolConverter<BenchmarkPocoBigEndian> _bigEndian;
		private IProtocolConverter<BenchmarkStringPoco> _withStrings;

		private BenchmarkPoco _littleSource;
		private BenchmarkPocoBigEndian _bigSource;
		private BenchmarkStringPoco _stringSource;

		private byte[] _littlePayload;
		private byte[] _bigPayload;
		private byte[] _stringPayload;

		private BenchmarkPoco _reusableInstance;

		/// <summary>
		/// A receive buffer holding the frame somewhere in the middle, which is what a socket or a
		/// pipe actually hands over. Before the span overloads existed the caller had to copy the
		/// frame out of here into its own byte[] before the converter would look at it.
		/// </summary>
		private byte[] _receiveBuffer;

		private IProtocolConverter<BenchmarkAdvancedPoco> _advanced;
		private IProtocolConverter<BenchmarkAdvancedPocoBigEndian> _advancedBigEndian;
		private BenchmarkAdvancedPoco _advancedSource;
		private BenchmarkAdvancedPocoBigEndian _advancedBigEndianSource;
		private byte[] _advancedPayload;
		private byte[] _advancedBigEndianPayload;

		/// <summary>
		/// A buffer the caller owns and reuses, which is the point of TryConvertToByteArray.
		/// </summary>
		private byte[] _writeBuffer;
		private int _frameOffset;
		private int _frameLength;


		[GlobalSetup]
		public void Setup()
		{
			_littleEndian = new ProtocolConverter<BenchmarkPoco>(null);
			_littleEndian.Prepare();

			_bigEndian = new ProtocolConverter<BenchmarkPocoBigEndian>(null);
			_bigEndian.Prepare();

			_withStrings = new ProtocolConverter<BenchmarkStringPoco>(null);
			_withStrings.Prepare();

			_littleSource = new BenchmarkPoco
			{
				IntField = -4812,
				UIntField = 4812,
				ShortField = -4711,
				UShortField = 4711,
				ByteField = 255,
				BoolField = true,
				FloatField = 1.0815f,
				DoubleField = Math.PI,
				LongField = -481247114812,
				EnumField = BenchmarkEnum.Second,
			};

			_bigSource = new BenchmarkPocoBigEndian
			{
				IntField = -4812,
				UIntField = 4812,
				ShortField = -4711,
				UShortField = 4711,
				ByteField = 255,
				BoolField = true,
				FloatField = 1.0815f,
				DoubleField = Math.PI,
				LongField = -481247114812,
				EnumField = BenchmarkEnum.Second,
			};

			_stringSource = new BenchmarkStringPoco
			{
				JobId = 4711,
				Code = "ABCDEFGH",
			};

			_littlePayload = _littleEndian.ConvertToByteArray(_littleSource);
			_bigPayload = _bigEndian.ConvertToByteArray(_bigSource);
			_stringPayload = _withStrings.ConvertToByteArray(_stringSource);

			_reusableInstance = new BenchmarkPoco();

			// the frame sits at a non zero offset, so the span is never the whole array
			_frameOffset = 7;
			_frameLength = _littlePayload.Length;
			_receiveBuffer = new byte[_frameOffset + _frameLength + 5];
			_littlePayload.CopyTo(_receiveBuffer, _frameOffset);

			_advanced = new ProtocolConverter<BenchmarkAdvancedPoco>(null);
			_advanced.Prepare();

			_advancedBigEndian = new ProtocolConverter<BenchmarkAdvancedPocoBigEndian>(null);
			_advancedBigEndian.Prepare();

			var stamp = new DateTime(2026, 9, 13, 21, 47, 11, DateTimeKind.Utc);
			var guid = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");

			_advancedSource = new BenchmarkAdvancedPoco
			{
				DateTime32Field = stamp,
				DateTime64Field = stamp,
				GuidField = guid,
				DecimalField = -12345.6789m,
			};

			_advancedBigEndianSource = new BenchmarkAdvancedPocoBigEndian
			{
				DateTime32Field = stamp,
				DateTime64Field = stamp,
				GuidField = guid,
				DecimalField = -12345.6789m,
			};

			_advancedPayload = _advanced.ConvertToByteArray(_advancedSource);
			_advancedBigEndianPayload = _advancedBigEndian.ConvertToByteArray(_advancedBigEndianSource);

			_writeBuffer = new byte[256];
		}


		[Benchmark(Description = "Read  from receive buffer, copy to array first")]
		public BenchmarkPoco ReadFromBufferWithCopy()
		{
			// what every caller had to write before the span overloads existed
			var frame = new byte[_frameLength];
			Array.Copy(_receiveBuffer, _frameOffset, frame, 0, _frameLength);

			return _littleEndian.ConvertFromByteArray(frame);
		}


		[Benchmark(Description = "Read  from receive buffer, span, no copy")]
		public BenchmarkPoco ReadFromBufferAsSpan()
			=> _littleEndian.ConvertFromByteArray(_receiveBuffer.AsSpan(_frameOffset, _frameLength));


		[Benchmark(Description = "Read  from receive buffer, span into reused instance")]
		public void ReadFromBufferAsSpanIntoExistingInstance()
			=> _littleEndian.ConvertFromByteArray(
					_receiveBuffer.AsSpan(_frameOffset, _frameLength), _reusableInstance);


		[Benchmark(Description = "Read  38 byte POCO, little endian")]
		public BenchmarkPoco ReadLittleEndian()
			=> _littleEndian.ConvertFromByteArray(_littlePayload);


		[Benchmark(Description = "Read  38 byte POCO, big endian")]
		public BenchmarkPocoBigEndian ReadBigEndian()
			=> _bigEndian.ConvertFromByteArray(_bigPayload);


		[Benchmark(Description = "Read  38 byte POCO into reused instance")]
		public void ReadIntoExistingInstance()
			=> _littleEndian.ConvertFromByteArray(_littlePayload, _reusableInstance);


		[Benchmark(Description = "Write 38 byte POCO, little endian")]
		public byte[] WriteLittleEndian()
			=> _littleEndian.ConvertToByteArray(_littleSource);


		[Benchmark(Description = "Write 38 byte POCO, big endian")]
		public byte[] WriteBigEndian()
			=> _bigEndian.ConvertToByteArray(_bigSource);


		[Benchmark(Description = "Read  DateTime/Guid/decimal POCO, little endian")]
		public BenchmarkAdvancedPoco ReadAdvanced()
			=> _advanced.ConvertFromByteArray(_advancedPayload);


		[Benchmark(Description = "Read  DateTime/Guid/decimal POCO, big endian")]
		public BenchmarkAdvancedPocoBigEndian ReadAdvancedBigEndian()
			=> _advancedBigEndian.ConvertFromByteArray(_advancedBigEndianPayload);


		[Benchmark(Description = "Write DateTime/Guid/decimal POCO, little endian")]
		public byte[] WriteAdvanced()
			=> _advanced.ConvertToByteArray(_advancedSource);


		[Benchmark(Description = "Write DateTime/Guid/decimal POCO, big endian")]
		public byte[] WriteAdvancedBigEndian()
			=> _advancedBigEndian.ConvertToByteArray(_advancedBigEndianSource);


		[Benchmark(Description = "Write 38 byte POCO into a reused buffer")]
		public int WriteIntoOwnBuffer()
		{
			_littleEndian.TryConvertToByteArray(_littleSource, _writeBuffer, out var written);
			return written;
		}


		[Benchmark(Description = "Read  POCO with fixed length string")]
		public BenchmarkStringPoco ReadWithString()
			=> _withStrings.ConvertFromByteArray(_stringPayload);


		[Benchmark(Description = "Write POCO with fixed length string")]
		public byte[] WriteWithString()
			=> _withStrings.ConvertToByteArray(_stringSource);
	}


	/// <summary>
	/// The types that have their own handler and that no other benchmark POCO covers: DateTime in
	/// both unix formats, Guid and decimal. Without this the DateTime path, which reaches into
	/// holonsoft.FluentDateTime, and the Guid and decimal paths were never measured at all.
	/// </summary>
	public class BenchmarkAdvancedPoco
	{
		[ProtocolField(StartPos = 0)]
		[ProtocolDateTimeField(DateTimeKind = DateTimeKind.Utc, DateTimeByteFormat = DateTimeByteFormat.UnixTimeStamp32Bit)]
		public DateTime DateTime32Field;

		[ProtocolField(StartPos = 4)]
		[ProtocolDateTimeField(DateTimeKind = DateTimeKind.Utc, DateTimeByteFormat = DateTimeByteFormat.UnixTimeStamp64Bit)]
		public DateTime DateTime64Field;

		[ProtocolField(StartPos = 12)] public Guid GuidField;
		[ProtocolField(StartPos = 28)] public decimal DecimalField;
	}


	[ProtocolSetupArgument(UseBigEndian = true)]
	public class BenchmarkAdvancedPocoBigEndian
	{
		[ProtocolField(StartPos = 0)]
		[ProtocolDateTimeField(DateTimeKind = DateTimeKind.Utc, DateTimeByteFormat = DateTimeByteFormat.UnixTimeStamp32Bit)]
		public DateTime DateTime32Field;

		[ProtocolField(StartPos = 4)]
		[ProtocolDateTimeField(DateTimeKind = DateTimeKind.Utc, DateTimeByteFormat = DateTimeByteFormat.UnixTimeStamp64Bit)]
		public DateTime DateTime64Field;

		[ProtocolField(StartPos = 12)] public Guid GuidField;
		[ProtocolField(StartPos = 28)] public decimal DecimalField;
	}


	public enum BenchmarkEnum
	{
		First,
		Second,
		Third,
	}


	/// <summary>
	/// Representative small telemetry frame: every primitive the converter supports, 38 bytes.
	/// </summary>
	public class BenchmarkPoco
	{
		[ProtocolField(StartPos = 0)] public int IntField;
		[ProtocolField(StartPos = 4)] public uint UIntField;
		[ProtocolField(StartPos = 8)] public short ShortField;
		[ProtocolField(StartPos = 10)] public ushort UShortField;
		[ProtocolField(StartPos = 12)] public byte ByteField;
		[ProtocolField(StartPos = 13)] public bool BoolField;
		[ProtocolField(StartPos = 14)] public float FloatField;
		[ProtocolField(StartPos = 18)] public double DoubleField;
		[ProtocolField(StartPos = 26)] public long LongField;
		[ProtocolField(StartPos = 34, TypeInByteArray = DestinationType.Int32)] public BenchmarkEnum EnumField;
	}


	[ProtocolSetupArgument(UseBigEndian = true)]
	public class BenchmarkPocoBigEndian
	{
		[ProtocolField(StartPos = 0)] public int IntField;
		[ProtocolField(StartPos = 4)] public uint UIntField;
		[ProtocolField(StartPos = 8)] public short ShortField;
		[ProtocolField(StartPos = 10)] public ushort UShortField;
		[ProtocolField(StartPos = 12)] public byte ByteField;
		[ProtocolField(StartPos = 13)] public bool BoolField;
		[ProtocolField(StartPos = 14)] public float FloatField;
		[ProtocolField(StartPos = 18)] public double DoubleField;
		[ProtocolField(StartPos = 26)] public long LongField;
		[ProtocolField(StartPos = 34, TypeInByteArray = DestinationType.Int32)] public BenchmarkEnum EnumField;
	}


	public class BenchmarkStringPoco
	{
		[ProtocolField(StartPos = 0, TypeInByteArray = DestinationType.UInt32)]
		public uint JobId;

		[ProtocolField(StartPos = 4, TypeInByteArray = DestinationType.None)]
		[ProtocolStringField(FillupCharWhenShorter = '\0', StringMaxLengthInByteArray = 32, Encoder = SupportedEncoder.ASCIIEncoder)]
		public string Code;
	}
}
