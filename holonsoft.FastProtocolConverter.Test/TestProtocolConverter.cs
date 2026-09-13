using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using Shouldly;
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

			Should.Throw<ArgumentOutOfRangeException>(() => converter.ConvertFromByteArray(new byte[10]));
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

			Should.Throw<ProtocolConverterException>(() => converter.Prepare());
		}

		[Fact]
		public void TestDoublePositionException()
		{
			var converter = new ProtocolConverter<DumbPocoDoublePositionFields>(_logger) as IProtocolConverter<DumbPocoDoublePositionFields>;

			Should.Throw<ProtocolConverterException>(() => converter.Prepare());
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

			myInstance1.ShortField.ShouldBe((short) -4711);
			myInstance1.IntField.ShouldBe(-4812);
			myInstance1.ByteField.ShouldBe((byte) 255);
			myInstance1.EnumField1.ShouldBe(MyImportantEnum.B);
			myInstance1.EnumField2.ShouldBe(MyImportantEnum.C);
			myInstance1.EnumField3.ShouldBe(MyImportantEnum.D);
			myInstance1.FloatField.ShouldBe(1.0815f);
			myInstance1.DoubleField.ShouldBe(Math.PI);
			myInstance1.UIntField.ShouldBe((uint) 4812);
			myInstance1.UShortField.ShouldBe((ushort) 4711);


			var myInstance2 = new DumbPoco();
			converter.ConvertFromByteArray(byteList.ToArray(), myInstance2);

			myInstance2.ShortField.ShouldBe((short) -4711);
			myInstance2.IntField.ShouldBe(-4812);
			myInstance2.ByteField.ShouldBe((byte) 255);
			myInstance2.EnumField1.ShouldBe(MyImportantEnum.B);
			myInstance2.EnumField2.ShouldBe(MyImportantEnum.C);
			myInstance2.EnumField3.ShouldBe(MyImportantEnum.D);
			myInstance2.FloatField.ShouldBe(1.0815f);
			myInstance2.DoubleField.ShouldBe(Math.PI);
			myInstance2.UIntField.ShouldBe((uint) 4812);
			myInstance2.UShortField.ShouldBe((ushort) 4711);

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

			resultByteArray.ShouldBe(expectedByteArray);
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

			myInstance.IntField.ShouldBe(4812);
			myInstance.ShortField.ShouldBe((short) 4711);
			myInstance.ByteField.ShouldBe((byte) 255);
			myInstance.String1.ShouldBe(str1);
			myInstance.String2.ShouldBe(str2);
			myInstance.EnumField1.ShouldBe(MyImportantEnum.B);
			myInstance.EnumField2.ShouldBe(MyImportantEnum.C);
			myInstance.EnumField3.ShouldBe(MyImportantEnum.D);
			myInstance.FloatField.ShouldBe(1.0815f);
			myInstance.DoubleField.ShouldBe(Math.PI);
			myInstance.UIntField.ShouldBe((uint) 4812);
			myInstance.UShortField.ShouldBe((ushort) 4711);
			myInstance.TrueField.ShouldBeTrue();
			myInstance.FalseField.ShouldBeFalse();
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
			resultByteArray.ShouldBe(expectedByteArray);
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
			((int) byteArrayResult[^1]).ShouldBe(21);
#else
			// ReSharper disable once UseIndexFromEndExpression
			((int) byteArrayResult[byteArrayResult.Length - 1]).ShouldBe(21);
#endif


			var newInstance = converter.ConvertFromByteArray(byteArrayResult);

			newInstance.IntField.ShouldBe(myInstance.IntField);
			newInstance.UIntField.ShouldBe(myInstance.UIntField);
			newInstance.ByteField.ShouldBe(myInstance.ByteField);
			newInstance.String1.ShouldBe(myInstance.String1);
			newInstance.String2.ShouldBe(myInstance.String2);
			newInstance.EnumField1.ShouldBe(myInstance.EnumField1);
			newInstance.EnumField2.ShouldBe(myInstance.EnumField2);
			newInstance.EnumField3.ShouldBe(myInstance.EnumField3);
			newInstance.FloatField.ShouldBe(myInstance.FloatField);
			newInstance.DoubleField.ShouldBe(myInstance.DoubleField);
			newInstance.UShortField.ShouldBe(myInstance.UShortField);
			newInstance.ShortField.ShouldBe(myInstance.ShortField);

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
			byteArrayResult.Length.ShouldBe(20);

			var newInstance = converter.ConvertFromByteArray(byteArrayResult);

			newInstance.StrField1.ShouldBe("ABCZZZZZZZ");
			newInstance.StrField2.ShouldBe("ABCDEFGHIJ");
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
			resultHeader.MsgTimestamp.ShouldBe(header.MsgTimestamp);
			resultHeader.MsgVersionMajor.ShouldBe(header.MsgVersionMajor);
			resultHeader.MsgVersionMinor.ShouldBe(header.MsgVersionMinor);
			resultHeader.PayloadLength.ShouldBe(header.PayloadLength);


			var resultPayload = payloadConverter.ConvertFromByteArray(data);

			resultPayload.MyDouble1.ShouldBe(payload.MyDouble1);
			resultPayload.MyDouble3.ShouldBe(payload.MyDouble3);
			resultPayload.MyDouble2.ShouldBe(payload.MyDouble2);
			resultPayload.MyDouble4.ShouldBe(payload.MyDouble4);
			resultPayload.MyDouble5.ShouldBe(payload.MyDouble5);
			resultPayload.MyDouble6.ShouldBe(payload.MyDouble6);
			resultPayload.MyDouble7.ShouldBe(payload.MyDouble7);
			resultPayload.MyDouble8.ShouldBe(payload.MyDouble8);
			resultPayload.MyDouble9.ShouldBe(payload.MyDouble9);
			resultPayload.ExampleEnum1.ShouldBe(payload.ExampleEnum1);
			resultPayload.ExampleEnum2.ShouldBe(payload.ExampleEnum2);
			resultPayload.ModelName.ShouldBe(payload.ModelName);
			resultPayload.DetectionId.ShouldBe(payload.DetectionId);
			resultPayload.SourceId.ShouldBe(payload.SourceId);
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

			(40 == s1.Length).ShouldBeTrue();
			(s1.Length == s2.Length).ShouldBeTrue();

			var b1 = GetReversedPartialArray(s1, 0, 4);
			var b2 = GetPartialArray(s2, 0, 4);
			b2.ShouldBe(b1);

			var x = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(endianessSource.IntField));
			x.ShouldBe(b2);

			b1 = GetReversedPartialArray(s1, 4, 4);
			b2 = GetPartialArray(s2, 4, 4);
			b2.ShouldBe(b1);

			b1 = GetReversedPartialArray(s1, 8, 2);
			b2 = GetPartialArray(s2, 8, 2);
			b2.ShouldBe(b1);

			b1 = GetReversedPartialArray(s1, 10, 2);
			b2 = GetPartialArray(s2, 10, 2);
			b2.ShouldBe(b1);

			b1 = GetReversedPartialArray(s1, 12, 4);
			b2 = GetPartialArray(s2, 12, 4);
			b2.ShouldBe(b1);

			b1 = GetReversedPartialArray(s1, 16, 8);
			b2 = GetPartialArray(s2, 16, 8);
			b2.ShouldBe(b1);

			b1 = GetReversedPartialArray(s1, 24, 8);
			b2 = GetPartialArray(s2, 24, 8);
			b2.ShouldBe(b1);
			x = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(endianessSource.LongField));
			x.ShouldBe(b2);


			b1 = GetReversedPartialArray(s1, 32, 8);
			b2 = GetPartialArray(s2, 32, 8);
			b2.ShouldBe(b1);


			var noEndianess = convNoEndianess.ConvertFromByteArray(s1);
			var withEndianess = convWithEndianess.ConvertFromByteArray(s2);

			noEndianess.DoubleField.ShouldBe(noEndianessSource.DoubleField);
			noEndianess.FloatField.ShouldBe(noEndianessSource.FloatField);
			noEndianess.IntField.ShouldBe(noEndianessSource.IntField);
			noEndianess.UIntField.ShouldBe(noEndianessSource.UIntField);
			noEndianess.ShortField.ShouldBe(noEndianessSource.ShortField);
			noEndianess.UShortField.ShouldBe(noEndianessSource.UShortField);
			noEndianess.LongField.ShouldBe(noEndianessSource.LongField);
			noEndianess.ULongField.ShouldBe(noEndianessSource.ULongField);

			withEndianess.DoubleField.ShouldBe(endianessSource.DoubleField);
			withEndianess.FloatField.ShouldBe(endianessSource.FloatField);
			withEndianess.IntField.ShouldBe(endianessSource.IntField);
			withEndianess.UIntField.ShouldBe(endianessSource.UIntField);
			withEndianess.ShortField.ShouldBe(endianessSource.ShortField);
			withEndianess.UShortField.ShouldBe(endianessSource.UShortField);
			withEndianess.LongField.ShouldBe(endianessSource.LongField);
			withEndianess.ULongField.ShouldBe(endianessSource.ULongField);
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

			resultArray.ShouldNotBeNull();

			converter.OnRangeViolation += OnRangeViolationIgnore;
			var rr = converter.ConvertFromByteArray(resultArray);

			rr.IntField.ShouldBe(4200);

			converter.OnRangeViolation -= OnRangeViolationIgnore;

			converter.OnRangeViolation += OnRangeViolationSetToMinVal;
			rr = converter.ConvertFromByteArray(resultArray);
			rr.IntField.ShouldBe(-2200);


			converter.OnRangeViolation -= OnRangeViolationSetToMinVal;
			converter.OnRangeViolation += OnRangeViolationSetToMaxVal;
			rr = converter.ConvertFromByteArray(resultArray);
			rr.IntField.ShouldBe(-1200);

			converter.OnRangeViolation -= OnRangeViolationSetToMaxVal;
			converter.OnRangeViolation += OnRangeViolationSetToDefaultVal;
			rr = converter.ConvertFromByteArray(resultArray);
			rr.IntField.ShouldBe(-2000);

			converter.OnRangeViolation -= OnRangeViolationSetToMaxVal;
			converter.OnRangeViolation += OnRangeViolationStop;
			Should.Throw<ProtocolConverterException>(() => converter.ConvertFromByteArray(resultArray));
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

			resultArray.Length.ShouldBe(37);

			var rr = converter.ConvertFromByteArray(resultArray);

			rr.Padding.ShouldBe(r.Padding);
			rr.DateTimeField.ShouldBe(r.DateTimeField);
			rr.GuidField.ShouldBe(r.GuidField);

		}


		[Fact]
		public void TestSimplePocoWithGap()
		{
			var converter = new ProtocolConverter<SimplePocoWithGap>(_logger) as IProtocolConverter<SimplePocoWithGap>;
			converter.Prepare();

			var r = new SimplePocoWithGap()
			{
				JobNumberIn = 4711,
				MachineMode = PLCMachineMode.B,
				Request = true,
				Start = true,
			};

			var resultArray = converter.ConvertToByteArray(r);
			resultArray.Length.ShouldBe(1128);

			var rr = converter.ConvertFromByteArray(resultArray);

			rr.JobNumberIn.ShouldBe(r.JobNumberIn);
			rr.MachineMode.ShouldBe(r.MachineMode);
			rr.Request.ShouldBe(r.Request);
			rr.Start.ShouldBe(r.Start);

		}


		[Fact]
		public void TestSimplePocoWithGapAndString()
		{
			var converter = new ProtocolConverter<SimplePocoWithGapAndString>(_logger) as IProtocolConverter<SimplePocoWithGapAndString>;
			converter.Prepare();

			var r = new SimplePocoWithGapAndString()
			{
				JobId = 4711,
				SomeImportantCode = "ABCDE",
				IsRequest = true,
				IsStart = true,
			};

			var resultArray = converter.ConvertToByteArray(r);
			resultArray.Length.ShouldBe(272);

			var rr = converter.ConvertFromByteArray(resultArray);

			rr.JobId.ShouldBe(r.JobId);
			rr.IsRequest.ShouldBe(r.IsRequest);
			rr.IsStart.ShouldBe(r.IsStart);
			rr.SomeImportantCode.ShouldBe(r.SomeImportantCode);
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


		/// <summary>
		/// A byte array that is shorter than the minimum length of a fixed position protocol
		/// must be rejected before any field is read. Since v4 a short frame is reported as a
		/// ProtocolConverterException, it is a protocol condition and not a programmer error.
		/// </summary>
		[Fact]
		public void TestTooShortByteStreamThrows()
		{
			var converter = new ProtocolConverter<SimplePocoWithGapAndString>(_logger) as IProtocolConverter<SimplePocoWithGapAndString>;
			converter.Prepare();

			Should.Throw<ProtocolConverterException>(() => converter.ConvertFromByteArray(new byte[2]));
		}


		/// <summary>
		/// The minimum length must cover the whole protocol, including fixed length strings and the
		/// repeat count of padding bytes. Before v4 it was computed from ExpectedFieldSize, which is
		/// -1 for strings, so SimplePocoWithGapAndString claimed a minimum of 14 for a 272 byte
		/// protocol and every length between 14 and 271 produced a raw ArgumentException.
		/// </summary>
		[Fact]
		public void TestMinimumLengthCoversTheWholeProtocol()
		{
			var converter = new ProtocolConverter<SimplePocoWithGapAndString>(_logger) as IProtocolConverter<SimplePocoWithGapAndString>;
			converter.Prepare();

			var complete = converter.ConvertToByteArray(new SimplePocoWithGapAndString
			{
				JobId = 4711,
				IsRequest = true,
				IsStart = true,
				SomeImportantCode = "ABCDE",
			});

			complete.Length.ShouldBe(272);

			// every truncation must be a clean protocol error, never a leaked BCL exception
			foreach (var length in new[] { 0, 1, 13, 14, 15, 100, 271 })
			{
				var truncated = complete.Take(length).ToArray();

				Should.Throw<ProtocolConverterException>(() => converter.ConvertFromByteArray(truncated),
					$"a {length} byte frame must be rejected as a protocol error");
			}

			// the complete frame still reads
			converter.ConvertFromByteArray(complete).SomeImportantCode.ShouldBe("ABCDE");
		}


		/// <summary>
		/// Sequence protocols had no minimum length check at all, a short frame was only noticed when
		/// a read ran past the end of the array and surfaced as a raw ArgumentException.
		/// </summary>
		[Fact]
		public void TestSequenceProtocolRejectsShortFrames()
		{
			var converter = new ProtocolConverter<ComplexProtocolFixedStringLength>(_logger) as IProtocolConverter<ComplexProtocolFixedStringLength>;
			converter.Prepare();

			var complete = converter.ConvertToByteArray(new ComplexProtocolFixedStringLength
			{
				StrField1 = "ABC",
				StrField2 = "ABCDEFGHIJ",
			});

			complete.Length.ShouldBe(20);

			for (var length = 0; length < complete.Length; length++)
			{
				var truncated = complete.Take(length).ToArray();

				Should.Throw<ProtocolConverterException>(() => converter.ConvertFromByteArray(truncated),
					$"a {length} byte frame must be rejected as a protocol error");
			}

			converter.ConvertFromByteArray(complete).StrField1.ShouldBe("ABCZZZZZZZ");
		}


		/// <summary>
		/// The unicode encoder stores two bytes per character, so the reserved space must be twice
		/// the one of the ASCII encoder and the round trip must return the original string.
		/// </summary>
		[Fact]
		public void TestUnicodeStringConversion()
		{
			var converter = new ProtocolConverter<SimplePocoWithUnicodeString>(_logger) as IProtocolConverter<SimplePocoWithUnicodeString>;
			converter.Prepare();

			var source = new SimplePocoWithUnicodeString()
			{
				JobId = 4711,
				SomeImportantCode = "Gruesse",
			};

			var resultArray = converter.ConvertToByteArray(source);
			resultArray.Length.ShouldBe(4 + 64);

			var roundTrip = converter.ConvertFromByteArray(resultArray);

			roundTrip.JobId.ShouldBe(source.JobId);
			roundTrip.SomeImportantCode.ShouldBe(source.SomeImportantCode);
		}

	}
}