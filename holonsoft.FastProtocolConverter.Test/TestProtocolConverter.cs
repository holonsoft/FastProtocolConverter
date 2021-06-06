using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	public class TestProtocolConverter
	{
		private ILogger<TestProtocolConverter> _logger = null;

		[Fact]
		public void TestMissingPrepareCall()
		{
			var converter = new ProtocolConverter<DumbPoco>(_logger) as IProtocolConverter<DumbPoco>;

			Assert.Throws<ArgumentOutOfRangeException>(() => converter.ConvertFromByteArray(new byte[10]));
		}


		[Fact]
		public void TestArgumentDetection()
		{
			var converter = new ProtocolConverter<DumbPoco>(_logger) as IProtocolConverter<DumbPoco>; 

			converter.Prepare();
		}

		[Fact]
		public void TestOverlappingFieldsException()
		{
			var converter = new ProtocolConverter<DumbPocoOverlappingFields>(_logger) as IProtocolConverter<DumbPocoOverlappingFields>;

			Assert.Throws<ProtocolConverterException>(() => converter.Prepare());
		}

		[Fact]
		public void TestDoublePositionException()
		{
			var converter = new ProtocolConverter<DumbPocoDoublePositionFields>(_logger) as IProtocolConverter<DumbPocoDoublePositionFields>; 

			Assert.Throws<ProtocolConverterException>(() => converter.Prepare());
		}


		[Fact]
		public void TestConvertFromByteArray()
		{
			var converter = new ProtocolConverter<DumbPoco>(_logger) as IProtocolConverter<DumbPoco>; 
			converter.Prepare();

			var byteList = new List<byte>();

			// dummy data for offset testing
			byteList.AddRange(BitConverter.GetBytes((int) 9999));


			// protocol data
			byteList.AddRange(BitConverter.GetBytes((short) -4711));
			byteList.AddRange(BitConverter.GetBytes((int) -4812));
			byteList.Add((byte) 255);
			byteList.AddRange(BitConverter.GetBytes((int) MyImportantEnum.B));
			byteList.AddRange(BitConverter.GetBytes((short) MyImportantEnum.C));
			byteList.Add(((byte) MyImportantEnum.D));
			byteList.AddRange(BitConverter.GetBytes((float) 1.0815));
			byteList.AddRange(BitConverter.GetBytes((double) Math.PI));
			byteList.AddRange(BitConverter.GetBytes((uint) 4812));
			byteList.AddRange(BitConverter.GetBytes((ushort) 4711));

			var myInstance1 = converter.ConvertFromByteArray(byteList.ToArray());

			Assert.Equal(-4711, myInstance1.ShortField);
			Assert.Equal(-4812, myInstance1.IntField);
			Assert.Equal(255, myInstance1.ByteField);
			Assert.Equal(MyImportantEnum.B, myInstance1.EnumField1);
			Assert.Equal(MyImportantEnum.C, myInstance1.EnumField2);
			Assert.Equal(MyImportantEnum.D, myInstance1.EnumField3);
			Assert.Equal(1.0815f, myInstance1.FloatField);
			Assert.Equal(Math.PI, myInstance1.DoubleField);
			Assert.Equal((uint) 4812, myInstance1.UIntField);
			Assert.Equal((ushort) 4711, myInstance1.UShortField);


			var myInstance2 = new DumbPoco();
			converter.ConvertFromByteArray(byteList.ToArray(), myInstance2);

			Assert.Equal(-4711, myInstance2.ShortField);
			Assert.Equal(-4812, myInstance2.IntField);
			Assert.Equal(255, myInstance2.ByteField);
			Assert.Equal(MyImportantEnum.B, myInstance2.EnumField1);
			Assert.Equal(MyImportantEnum.C, myInstance2.EnumField2);
			Assert.Equal(MyImportantEnum.D, myInstance2.EnumField3);
			Assert.Equal(1.0815f, myInstance2.FloatField);
			Assert.Equal(Math.PI, myInstance2.DoubleField);
			Assert.Equal((uint) 4812, myInstance2.UIntField);
			Assert.Equal((ushort) 4711, myInstance2.UShortField);

		}


		[Fact]
		public void TestConvertToByteArray()
		{
			var byteList = new List<byte>();

			// protocol data
			byteList.AddRange(BitConverter.GetBytes((short) -4711));
			byteList.AddRange(BitConverter.GetBytes((int) -4812));
			byteList.Add((byte) 255);
			byteList.AddRange(BitConverter.GetBytes((int) MyImportantEnum.B));
			byteList.AddRange(BitConverter.GetBytes((short) MyImportantEnum.C));
			byteList.Add(((byte) MyImportantEnum.D));
			byteList.AddRange(BitConverter.GetBytes((float) 1.0815));
			byteList.AddRange(BitConverter.GetBytes((double) Math.PI));
			byteList.AddRange(BitConverter.GetBytes((uint) 4812));
			byteList.AddRange(BitConverter.GetBytes((ushort) 4711));

			var expectedByteArray = byteList.ToArray();

			var myInstance = new DumbPoco()
			{
				IntField = -4812,
				ShortField = -4711,
				ByteField = 255,
				EnumField1 = MyImportantEnum.B,
				EnumField2 = MyImportantEnum.C,
				EnumField3 = MyImportantEnum.D,
				FloatField = 1.0815f,
				DoubleField = Math.PI,
				UIntField = 4812,
				UShortField = 4711,
			};

			var converter = new ProtocolConverter<DumbPoco>(_logger) as IProtocolConverter<DumbPoco>;
			converter.Prepare();

			var resultByteArray = converter.ConvertToByteArray(myInstance);

			Assert.Equal(expectedByteArray, resultByteArray);
		}



		[Fact]
		public void TestComplexProtocolFromByteArray()
		{
			var str1 = "This is a .net string (unicode)";
			var str2 = "Another .net string (unicode)";

			var byteList = new List<byte>();

			// dummy data for offset testing
			byteList.AddRange(BitConverter.GetBytes((int) 9999));

			// protocol data
			byteList.AddRange(BitConverter.GetBytes((int) Encoding.ASCII.GetBytes(str1).Length));
			byteList.AddRange(Encoding.ASCII.GetBytes(str1));
			byteList.AddRange(BitConverter.GetBytes((int) Encoding.ASCII.GetBytes(str2).Length));
			byteList.AddRange(Encoding.ASCII.GetBytes(str2));
			byteList.AddRange(BitConverter.GetBytes((int) 4812));
			byteList.AddRange(BitConverter.GetBytes((short) 4711));
			byteList.Add((byte) 255);
			byteList.AddRange(BitConverter.GetBytes((int) MyImportantEnum.B));
			byteList.AddRange(BitConverter.GetBytes((short) MyImportantEnum.C));
			byteList.Add((byte) MyImportantEnum.D);
			byteList.AddRange(BitConverter.GetBytes((float) 1.0815));
			byteList.AddRange(BitConverter.GetBytes((double) Math.PI));
			byteList.AddRange(BitConverter.GetBytes((uint) 4812));
			byteList.AddRange(BitConverter.GetBytes((ushort) 4711));
			byteList.AddRange(BitConverter.GetBytes(true));
			byteList.AddRange(BitConverter.GetBytes(false));

			var converter = new ProtocolConverter<ComplexProtocolWithOffset>(_logger) as IProtocolConverter<ComplexProtocolWithOffset>;
			converter.Prepare();

			var myInstance = converter.ConvertFromByteArray(byteList.ToArray());

			Assert.Equal(4812, myInstance.IntField);
			Assert.Equal(4711, myInstance.ShortField);
			Assert.Equal(255, myInstance.ByteField);
			Assert.Equal(str1, myInstance.String1);
			Assert.Equal(str2, myInstance.String2);
			Assert.Equal(MyImportantEnum.B, myInstance.EnumField1);
			Assert.Equal(MyImportantEnum.C, myInstance.EnumField2);
			Assert.Equal(MyImportantEnum.D, myInstance.EnumField3);
			Assert.Equal(1.0815f, myInstance.FloatField);
			Assert.Equal(Math.PI, myInstance.DoubleField);
			Assert.Equal((uint) 4812, myInstance.UIntField);
			Assert.Equal((ushort) 4711, myInstance.UShortField);
			Assert.Equal(true, myInstance.TrueField);
			Assert.Equal(false, myInstance.FalseField);
		}



		//[Fact (Skip = "Still under development")]
		[Fact]
		public void TestComplexProtocolToByteArray()
		{
			var str1 = "This is a .net string (unicode)";
			var str2 = "Another .net string (unicode)";

			var byteList = new List<byte>();

			// protocol data
			byteList.AddRange(BitConverter.GetBytes((int) Encoding.ASCII.GetBytes(str1).Length));
			byteList.AddRange(Encoding.ASCII.GetBytes(str1));
			byteList.AddRange(BitConverter.GetBytes((int) Encoding.ASCII.GetBytes(str2).Length));
			byteList.AddRange(Encoding.ASCII.GetBytes(str2));
			byteList.AddRange(BitConverter.GetBytes((int) 4812));
			byteList.AddRange(BitConverter.GetBytes((short) 4711));
			byteList.Add((byte) 255);
			byteList.AddRange(BitConverter.GetBytes((int) MyImportantEnum.B));
			byteList.AddRange(BitConverter.GetBytes((short) MyImportantEnum.C));
			byteList.Add((byte) MyImportantEnum.D);
			byteList.AddRange(BitConverter.GetBytes((float) 1.0815));
			byteList.AddRange(BitConverter.GetBytes((double) Math.PI));
			byteList.AddRange(BitConverter.GetBytes((uint) 4812));
			byteList.AddRange(BitConverter.GetBytes((ushort) 4711));
			byteList.AddRange(BitConverter.GetBytes(true));
			byteList.AddRange(BitConverter.GetBytes(false));


			var expectedByteArray = byteList.ToArray();

			var myInstance = new ComplexProtocolWithOffset()
			{
				IntField = 4812,
				ShortField = 4711,
				ByteField = 255,
				String1 = str1,
				String2 = str2,
				EnumField1 = MyImportantEnum.B,
				EnumField2 = MyImportantEnum.C,
				EnumField3 = MyImportantEnum.D,
				FloatField = 1.0815f,
				DoubleField = Math.PI,
				UIntField = 4812,
				UShortField = 4711,
				TrueField = true,
				FalseField = false,
			};

			var converter = new ProtocolConverter<ComplexProtocolWithOffset>(_logger) as IProtocolConverter<ComplexProtocolWithOffset>;
			converter.Prepare();

			var resultByteArray = converter.ConvertToByteArray(myInstance);
			Assert.Equal(expectedByteArray, resultByteArray);
		}


		[Fact]
		public void TestComplexProtocolToByteArrayRoundtrip()
		{
			var str1 = "This is a .net string (unicode)";
			var str2 = "Another .net string (unicode)";

			var myInstance = new ComplexProtocol()
			{
				IntField = 4812,
				ShortField = 4711,
				ByteField = 255,
				String1 = str1,
				String2 = str2,
				EnumField1 = MyImportantEnum.B,
				EnumField2 = MyImportantEnum.C,
				EnumField3 = MyImportantEnum.D,
				FloatField = 1.0815f,
				DoubleField = Math.PI,
				UIntField = 4812,
				UShortField = 4711,
				Bit0 = true,
				Bit2 = true,
				Bit4 = true,
			};


			var converter = new ProtocolConverter<ComplexProtocol>(_logger) as IProtocolConverter<ComplexProtocol>;
			converter.Prepare();
			converter.OnSplitBitValues += (data, instance) =>
			{
				instance.Bit0 = (data & 0b0000_0001) > 0;
				instance.Bit2 = (data & 0b0000_0100) > 0;
				instance.Bit4 = (data & 0b0001_0000) > 0;
			};


			converter.OnConsolidateBitValues += (ComplexProtocol instance, out byte data) =>
			{
				data = 0;

				if (instance.Bit0)
				{
					data |= 0b0000_0001;
				}

				if (instance.Bit2)
				{
					data |= 0b0000_0100;
				}

				if (instance.Bit4)
				{
					data |= 0b0001_0000;
				}
			};


			var byteArrayResult = converter.ConvertToByteArray(myInstance);

#if NET   // C#8 and higher  
			Assert.Equal(21, (int)byteArrayResult[^1]);
#else
			// ReSharper disable once UseIndexFromEndExpression
			Assert.Equal(21, (int) byteArrayResult[byteArrayResult.Length - 1]);
#endif


			var newInstance = converter.ConvertFromByteArray(byteArrayResult);

			Assert.Equal(myInstance.IntField, newInstance.IntField);
			Assert.Equal(myInstance.UIntField, newInstance.UIntField);
			Assert.Equal(myInstance.ByteField, newInstance.ByteField);
			Assert.Equal(myInstance.String1, newInstance.String1);
			Assert.Equal(myInstance.String2, newInstance.String2);
			Assert.Equal(myInstance.EnumField1, newInstance.EnumField1);
			Assert.Equal(myInstance.EnumField2, newInstance.EnumField2);
			Assert.Equal(myInstance.EnumField3, newInstance.EnumField3);
			Assert.Equal(myInstance.FloatField, newInstance.FloatField);
			Assert.Equal(myInstance.DoubleField, newInstance.DoubleField);
			Assert.Equal(myInstance.UShortField, newInstance.UShortField);
			Assert.Equal(myInstance.ShortField, newInstance.ShortField);

		}


		[Fact]
		public void TestFixedStringConversion()
		{
			var myInstance = new ComplexProtocolFixedStringLength()
			{
				StrField1 = "ABC",
				StrField2 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
			};

			var converter = new ProtocolConverter<ComplexProtocolFixedStringLength>(_logger) as IProtocolConverter<ComplexProtocolFixedStringLength>;
			converter.Prepare();

			var byteArrayResult = converter.ConvertToByteArray(myInstance);
			Assert.Equal(28, byteArrayResult.Length);

			var newInstance = converter.ConvertFromByteArray(byteArrayResult);

			Assert.Equal("ABCZZZZZZZ", newInstance.StrField1);
			Assert.Equal("ABCDEFGHIJ", newInstance.StrField2);
		}


		[Fact]
		public void TestRealLifeProtocolDefiniton()
		{
			var variance = 3;

			var timestamp = (ulong) new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();

			var header = new ExamplePocoHeader()
			{
				MsgTypeId = (ushort) (1 + variance),
				MsgVersionMajor = (byte) variance,
				MsgVersionMinor = (byte) (variance + 1),
				PayloadLength = 0,
				MsgTimestamp = timestamp,
			};


			var payload = new ExamplePocoPayloadMsgType01()
			{
				TimeOfDetection = timestamp,
				MyDouble1 = 1 + variance,
				MyDouble2 = -90 + variance,
				MyDouble3 = 270 + variance,
				MyDouble4 = 90 - variance,
				MyDouble5 = variance / 10,
				MyDouble6 = Math.PI,
				MyDouble7 = Math.PI,
				MyDouble8 = Math.E,
				MyDouble9 = variance ^ 2,
				ExampleEnum1 = ExamplePocoEnum.ShutdownByOutlaw,
				ExampleEnum2 = ExamplePocoClassificationEnum.FlyingSpaghettiMonster,
				ModelName = "Model No" + variance,
				SourceId = "Source is " + variance,
				DetectionId = "Detection Id " + variance,
			};


			var headerConverter = new ProtocolConverter<ExamplePocoHeader>(_logger) as IProtocolConverter<ExamplePocoHeader>;
			headerConverter.Prepare();

			var payloadConverter = new ProtocolConverter<ExamplePocoPayloadMsgType01>(_logger) as IProtocolConverter<ExamplePocoPayloadMsgType01>;
			payloadConverter.Prepare();


			var dataToSend = new List<byte>();

			var payloadArray = payloadConverter.ConvertToByteArray(payload);
			header.PayloadLength = (uint) payloadArray.Length;

			dataToSend.AddRange(headerConverter.ConvertToByteArray(header));
			dataToSend.AddRange(payloadArray);

			var data = dataToSend.ToArray();

			var resultHeader = headerConverter.ConvertFromByteArray(data);
			Assert.Equal(header.MsgTimestamp, resultHeader.MsgTimestamp);
			Assert.Equal(header.MsgVersionMajor, resultHeader.MsgVersionMajor);
			Assert.Equal(header.MsgVersionMinor, resultHeader.MsgVersionMinor);
			Assert.Equal(header.PayloadLength, resultHeader.PayloadLength);


			var resultPayload = payloadConverter.ConvertFromByteArray(data);

			Assert.Equal(payload.MyDouble1, resultPayload.MyDouble1);
			Assert.Equal(payload.MyDouble3, resultPayload.MyDouble3);
			Assert.Equal(payload.MyDouble2, resultPayload.MyDouble2);
			Assert.Equal(payload.MyDouble4, resultPayload.MyDouble4);
			Assert.Equal(payload.MyDouble5, resultPayload.MyDouble5);
			Assert.Equal(payload.MyDouble6, resultPayload.MyDouble6);
			Assert.Equal(payload.MyDouble7, resultPayload.MyDouble7);
			Assert.Equal(payload.MyDouble8, resultPayload.MyDouble8);
			Assert.Equal(payload.MyDouble9, resultPayload.MyDouble9);
			Assert.Equal(payload.ExampleEnum1, resultPayload.ExampleEnum1);
			Assert.Equal(payload.ExampleEnum2, resultPayload.ExampleEnum2);
			Assert.Equal(payload.ModelName, resultPayload.ModelName);
			Assert.Equal(payload.DetectionId, resultPayload.DetectionId);
			Assert.Equal(payload.SourceId, resultPayload.SourceId);
		}


		[Fact]
		public void TestEndianess()
		{
			var noEndianessSource = new PocoNoBigEndianessFlag()
			{
				IntField = 4711,
				UIntField = 4812,
				ShortField = 124,
				UShortField = 123,
				FloatField = 1.234f,
				DoubleField = 5.6789,
				LongField = long.MinValue,
				ULongField = ulong.MaxValue,
			};

			var endianessSource = new PocoWithBigEndianessFlag()
			{
				IntField = 4711,
				UIntField = 4812,
				ShortField = 124,
				UShortField = 123,
				FloatField = 1.234f,
				DoubleField = 5.6789,
				LongField = long.MinValue,
				ULongField = ulong.MaxValue,
			};

			var convNoEndianess = new ProtocolConverter<PocoNoBigEndianessFlag>(_logger) as IProtocolConverter<PocoNoBigEndianessFlag>;
			convNoEndianess.Prepare();

			var convWithEndianess = new ProtocolConverter<PocoWithBigEndianessFlag>(_logger) as IProtocolConverter<PocoWithBigEndianessFlag>;
			convWithEndianess.Prepare();

			var s1 = convNoEndianess.ConvertToByteArray(noEndianessSource);
			var s2 = convWithEndianess.ConvertToByteArray(endianessSource);

			Assert.True(40 == s1.Length);
			Assert.True(s1.Length == s2.Length);

			var b1 = GetReversedPartialArray(s1, 0, 4);
			var b2 = GetPartialArray(s2, 0, 4);
			Assert.Equal(b1, b2);

			var x = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(endianessSource.IntField));
			Assert.Equal(b2, x);

			b1 = GetReversedPartialArray(s1, 4, 4);
			b2 = GetPartialArray(s2, 4, 4);
			Assert.Equal(b1, b2);

			b1 = GetReversedPartialArray(s1, 8, 2);
			b2 = GetPartialArray(s2, 8, 2);
			Assert.Equal(b1, b2);

			b1 = GetReversedPartialArray(s1, 10, 2);
			b2 = GetPartialArray(s2, 10, 2);
			Assert.Equal(b1, b2);

			b1 = GetReversedPartialArray(s1, 12, 4);
			b2 = GetPartialArray(s2, 12, 4);
			Assert.Equal(b1, b2);

			b1 = GetReversedPartialArray(s1, 16, 8);
			b2 = GetPartialArray(s2, 16, 8);
			Assert.Equal(b1, b2);

			b1 = GetReversedPartialArray(s1, 24, 8);
			b2 = GetPartialArray(s2, 24, 8);
			Assert.Equal(b1, b2);
			x = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(endianessSource.LongField));
			Assert.Equal(b2, x);


			b1 = GetReversedPartialArray(s1, 32, 8);
			b2 = GetPartialArray(s2, 32, 8);
			Assert.Equal(b1, b2);


			var noEndianess = convNoEndianess.ConvertFromByteArray(s1);
			var withEndianess = convWithEndianess.ConvertFromByteArray(s2);

			Assert.Equal(noEndianessSource.DoubleField, noEndianess.DoubleField);
			Assert.Equal(noEndianessSource.FloatField, noEndianess.FloatField);
			Assert.Equal(noEndianessSource.IntField, noEndianess.IntField);
			Assert.Equal(noEndianessSource.UIntField, noEndianess.UIntField);
			Assert.Equal(noEndianessSource.ShortField, noEndianess.ShortField);
			Assert.Equal(noEndianessSource.UShortField, noEndianess.UShortField);
			Assert.Equal(noEndianessSource.LongField, noEndianess.LongField);
			Assert.Equal(noEndianessSource.ULongField, noEndianess.ULongField);

			Assert.Equal(endianessSource.DoubleField, withEndianess.DoubleField);
			Assert.Equal(endianessSource.FloatField, withEndianess.FloatField);
			Assert.Equal(endianessSource.IntField, withEndianess.IntField);
			Assert.Equal(endianessSource.UIntField, withEndianess.UIntField);
			Assert.Equal(endianessSource.ShortField, withEndianess.ShortField);
			Assert.Equal(endianessSource.UShortField, withEndianess.UShortField);
			Assert.Equal(endianessSource.LongField, withEndianess.LongField);
			Assert.Equal(endianessSource.ULongField, withEndianess.ULongField);
		}

		private byte[] GetPartialArray(byte[] data, int start, int length)
		{
			var result = new byte[length];
			Buffer.BlockCopy(data, start, result, 0, length);

			return result;
		}

		private byte[] GetReversedPartialArray(byte[] data, int start, int length)
		{
			var result = GetPartialArray(data, start, length);

			return result.Reverse().ToArray();
		}

		[Fact]
		public void TestPocoWithRangeInformations()
		{
			var r = new PocoWithRanges
			{
				IntField = 4200
			};

			var converter = new ProtocolConverter<PocoWithRanges>(_logger) as IProtocolConverter<PocoWithRanges>;
			converter.Prepare();

			var resultArray = converter.ConvertToByteArray(r);

			Assert.NotNull(resultArray);

			converter.OnRangeViolation += OnRangeViolationIgnore;
			var rr = converter.ConvertFromByteArray(resultArray);

			Assert.Equal(4200, rr.IntField);

			converter.OnRangeViolation -= OnRangeViolationIgnore;

			converter.OnRangeViolation += OnRangeViolationSetToMinVal;
			rr = converter.ConvertFromByteArray(resultArray);
			Assert.Equal(-2200, rr.IntField);


			converter.OnRangeViolation -= OnRangeViolationSetToMinVal;
			converter.OnRangeViolation += OnRangeViolationSetToMaxVal;
			rr = converter.ConvertFromByteArray(resultArray);
			Assert.Equal(-1200, rr.IntField);

			converter.OnRangeViolation -= OnRangeViolationSetToMaxVal;
			converter.OnRangeViolation += OnRangeViolationSetToDefaultVal;
			rr = converter.ConvertFromByteArray(resultArray);
			Assert.Equal(-2000, rr.IntField);

			converter.OnRangeViolation -= OnRangeViolationSetToMaxVal;
			converter.OnRangeViolation += OnRangeViolationStop;
			Assert.Throws<ProtocolConverterException>(() => converter.ConvertFromByteArray(resultArray));
		}


		[Fact]
		public void TestAdvancedTypesPoco()
		{
			var converter = new ProtocolConverter<AdvancedTypesPoco>(_logger) as IProtocolConverter<AdvancedTypesPoco>;
			converter.Prepare();

			var r = new AdvancedTypesPoco()
			{
				DateTimeField = new DateTime(2021, 06, 01, 13, 14, 15).ToUniversalTime(), 
				GuidField = Guid.NewGuid(),
			};
			
			var resultArray = converter.ConvertToByteArray(r);

			Assert.Equal(37, resultArray.Length);

			var rr = converter.ConvertFromByteArray(resultArray);

			Assert.Equal(r.Padding, rr.Padding);
			Assert.Equal(r.DateTimeField, rr.DateTimeField);
			Assert.Equal(r.GuidField, rr.GuidField);

		}


		private void OnRangeViolationSetToDefaultVal(FieldInfo FieldInfo, out ConverterRangeViolationBehaviour rangeViolationBehaviour)
		{
			rangeViolationBehaviour = ConverterRangeViolationBehaviour.SetToDefaultValue;
		}

		private void OnRangeViolationSetToMinVal(FieldInfo fieldInfo, out ConverterRangeViolationBehaviour rangeViolationBehaviour)
		{
			rangeViolationBehaviour = ConverterRangeViolationBehaviour.SetToMinValue;
		}


		private void OnRangeViolationSetToMaxVal(FieldInfo fieldInfo, out ConverterRangeViolationBehaviour rangeViolationBehaviour)
		{
			rangeViolationBehaviour = ConverterRangeViolationBehaviour.SetToMaxValue;
		}


		private void OnRangeViolationIgnore(FieldInfo fieldInfo, out ConverterRangeViolationBehaviour rangeViolationBehaviour)
		{
			rangeViolationBehaviour = ConverterRangeViolationBehaviour.IgnoreAndContinue;
		}


		private void OnRangeViolationStop(FieldInfo fieldInfo, out ConverterRangeViolationBehaviour rangeViolationBehaviour)
		{
			rangeViolationBehaviour = ConverterRangeViolationBehaviour.ThrowException;
		}

	}
}