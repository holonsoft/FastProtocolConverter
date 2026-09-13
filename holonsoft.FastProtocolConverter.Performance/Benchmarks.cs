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
		}


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


		[Benchmark(Description = "Read  POCO with fixed length string")]
		public BenchmarkStringPoco ReadWithString()
			=> _withStrings.ConvertFromByteArray(_stringPayload);


		[Benchmark(Description = "Write POCO with fixed length string")]
		public byte[] WriteWithString()
			=> _withStrings.ConvertToByteArray(_stringSource);
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
