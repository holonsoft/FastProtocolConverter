using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	/// <summary>
	/// Byte exact characterization tests. Every vector pins the wire format that the converter
	/// produces today, so any later refactoring of the hot path has to reproduce it bit for bit.
	///
	/// These vectors describe CURRENT behaviour, not necessarily desirable behaviour. A vector that
	/// changes is not automatically a bug, but it is always a deliberate decision that has to be
	/// reviewed.
	///
	/// To regenerate after an intended format change, run the test suite once with the environment
	/// variable FPC_REGENERATE_GOLDEN=1 and review the resulting diff of golden-vectors.txt.
	/// </summary>
	public class TestGoldenVectors
	{
		private readonly ILogger _logger = null;

		private const string RegenerateEnvironmentVariable = "FPC_REGENERATE_GOLDEN";

		/// <summary>
		/// Fixed point in time so the DateTime vectors stay stable: 2020-01-02 03:04:05 UTC.
		/// </summary>
		private static readonly DateTime FixedUtc = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

		private static readonly Guid FixedGuid = new("0f1e2d3c-4b5a-6978-8796-a5b4c3d2e1f0");


		private static string ToHex(byte[] data)
			=> Convert.ToHexString(data);


		private static byte[] Serialize<TPoco>(ILogger logger, TPoco instance, Action<IProtocolConverter<TPoco>> configure = null)
			where TPoco : class, new()
		{
			var converter = new ProtocolConverter<TPoco>(logger) as IProtocolConverter<TPoco>;
			converter.Prepare();
			configure?.Invoke(converter);

			return converter.ConvertToByteArray(instance);
		}


		/// <summary>
		/// Every vector is produced here. Keep this deterministic: no DateTime.Now, no Guid.NewGuid,
		/// no culture dependent formatting.
		/// </summary>
		private Dictionary<string, string> BuildVectors()
		{
			var vectors = new Dictionary<string, string>(StringComparer.Ordinal);

			vectors["DumbPoco"] = ToHex(Serialize(_logger, new DumbPoco
			{
				ShortField = -4711,
				IntField = -4812,
				ByteField = 255,
				EnumField1 = MyImportantEnum.B,
				EnumField2 = MyImportantEnum.C,
				EnumField3 = MyImportantEnum.D,
				FloatField = 1.0815f,
				DoubleField = Math.PI,
				UIntField = 4812,
				UShortField = 4711,
			}));

			vectors["PocoNoBigEndianessFlag"] = ToHex(Serialize(_logger, new PocoNoBigEndianessFlag
			{
				IntField = -4812,
				UIntField = 4812,
				ShortField = -4711,
				UShortField = 4711,
				FloatField = 1.0815f,
				DoubleField = Math.PI,
				LongField = -481247114812,
				ULongField = 481247114812,
			}));

			vectors["PocoWithBigEndianessFlag"] = ToHex(Serialize(_logger, new PocoWithBigEndianessFlag
			{
				IntField = -4812,
				UIntField = 4812,
				ShortField = -4711,
				UShortField = 4711,
				FloatField = 1.0815f,
				DoubleField = Math.PI,
				LongField = -481247114812,
				ULongField = 481247114812,
			}));

			vectors["SimplePocoWithGap"] = ToHex(Serialize(_logger, new SimplePocoWithGap
			{
				JobNumberIn = 4711,
				Request = true,
				Start = true,
				MachineMode = PLCMachineMode.C,
			}));

			vectors["SimplePocoWithGapAndString"] = ToHex(Serialize(_logger, new SimplePocoWithGapAndString
			{
				JobId = 4711,
				IsRequest = true,
				IsStart = true,
				SomeImportantCode = "ABCDE",
			}));

			vectors["SimplePocoWithUnicodeString"] = ToHex(Serialize(_logger, new SimplePocoWithUnicodeString
			{
				JobId = 4711,
				SomeImportantCode = "Gruesse",
			}));

			// non ASCII content is the whole reason to choose the unicode encoder, so pin it too
			vectors["SimplePocoWithUnicodeStringNonAscii"] = ToHex(Serialize(_logger, new SimplePocoWithUnicodeString
			{
				JobId = 4711,
				SomeImportantCode = "Grüße €",
			}));

			vectors["ComplexProtocolFixedStringLength"] = ToHex(Serialize(_logger, new ComplexProtocolFixedStringLength
			{
				StrField1 = "ABC",
				StrField2 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
			}));

			vectors["AdvancedTypesPoco"] = ToHex(Serialize(_logger, new AdvancedTypesPoco
			{
				DateTimeField = FixedUtc,
				GuidField = FixedGuid,
				SomeValue = 255,
			}));

			vectors["DecimalAndSBytePoco"] = ToHex(Serialize(_logger, new DecimalAndSBytePoco
			{
				SByteField = -128,
				DecimalField = -123.456m,
				TrailingField = -4812,
			}));

			vectors["DecimalAndSBytePocoBigEndian"] = ToHex(Serialize(_logger, new DecimalAndSBytePocoBigEndian
			{
				SByteField = -128,
				DecimalField = -123.456m,
				TrailingField = -4812,
			}));

			vectors["GuidLittleEndian"] = ToHex(Serialize(_logger, new GuidLittleEndianPoco { GuidField = FixedGuid }));

			// fixed in v4, the writer used to ignore UseBigEndian here
			vectors["GuidBigEndian"] = ToHex(Serialize(_logger, new GuidBigEndianPoco { GuidField = FixedGuid }));

			vectors["ComplexProtocol"] = ToHex(Serialize<ComplexProtocol>(_logger, new ComplexProtocol
			{
				String1 = "Hello",
				String2 = "World!",
				ShortField = -4711,
				IntField = -4812,
				ByteField = 255,
				EnumField1 = MyImportantEnum.B,
				EnumField2 = MyImportantEnum.C,
				EnumField3 = MyImportantEnum.D,
				FloatField = 1.0815f,
				DoubleField = Math.PI,
				UIntField = 4812,
				UShortField = 4711,
				Bit0 = true,
				Bit2 = true,
				Bit4 = false,
			}, converter => converter.OnConsolidateBitValues += (ComplexProtocol instance, out byte data) =>
			{
				data = 0;
				if (instance.Bit0) data |= 0x01;
				if (instance.Bit2) data |= 0x04;
				if (instance.Bit4) data |= 0x10;
			}));

			return vectors;
		}


		private static string GoldenFilePath()
		{
			// the file lives next to the test sources, not in the output folder, so it can be committed
			var directory = Path.GetDirectoryName(typeof(TestGoldenVectors).Assembly.Location);

			return Path.GetFullPath(Path.Combine(directory, "..", "..", "..", "golden-vectors.txt"));
		}


		private static Dictionary<string, string> ReadGoldenFile(string path)
		{
			var result = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (var line in File.ReadAllLines(path))
			{
				if (line.Length == 0 || line[0] == '#') continue;

				var separator = line.IndexOf('=');
				if (separator < 0) continue;

				result[line[..separator].Trim()] = line[(separator + 1)..].Trim();
			}

			return result;
		}


		[Fact]
		public void TestWireFormatIsUnchanged()
		{
			var actual = BuildVectors();
			var path = GoldenFilePath();

			// Regeneration is deliberate and explicit. A missing file must FAIL, never silently
			// regenerate: otherwise a fresh clone in CI would pin whatever the code does that day
			// and the safety net would disarm itself exactly when it is needed.
			if (Environment.GetEnvironmentVariable(RegenerateEnvironmentVariable) == "1")
			{
				var builder = new StringBuilder();
				builder.AppendLine("# Byte exact wire format of the converter, uppercase hex, one vector per line.");
				builder.AppendLine("# Regenerate deliberately with FPC_REGENERATE_GOLDEN=1 and review the diff.");

				foreach (var entry in actual.OrderBy(x => x.Key, StringComparer.Ordinal))
				{
					builder.AppendLine($"{entry.Key}={entry.Value}");
				}

				File.WriteAllText(path, builder.ToString());
			}

			File.Exists(path).ShouldBeTrue(
				$"golden vector file missing at {path}. It must be committed. "
				+ $"To create it deliberately, run with {RegenerateEnvironmentVariable}=1 and review the diff.");

			var expected = ReadGoldenFile(path);

			// a vector that disappears is as interesting as one that changes
			actual.Keys.OrderBy(x => x, StringComparer.Ordinal)
				.ShouldBe(expected.Keys.OrderBy(x => x, StringComparer.Ordinal));

			foreach (var entry in actual.OrderBy(x => x.Key, StringComparer.Ordinal))
			{
				entry.Value.ShouldBe(expected[entry.Key],
					$"wire format of {entry.Key} changed, review before regenerating the golden file");
			}
		}


		/// <summary>
		/// Every vector must survive a full round trip, otherwise the pinned bytes describe a format
		/// that the converter itself cannot read back.
		/// </summary>
		[Fact]
		public void TestEveryVectorSurvivesRoundTrip()
		{
			// DumbPoco is deliberately absent, see TestGlobalOffsetIsNotSymmetric below.

			RoundTrip(new PocoWithBigEndianessFlag { IntField = -4812, UIntField = 4812, ShortField = -4711, UShortField = 4711, FloatField = 1.0815f, DoubleField = Math.PI, LongField = -481247114812, ULongField = 481247114812 },
				(a, b) => { b.LongField.ShouldBe(a.LongField); b.ULongField.ShouldBe(a.ULongField); b.FloatField.ShouldBe(a.FloatField); });

			RoundTrip(new DecimalAndSBytePoco { SByteField = -128, DecimalField = -123.456m, TrailingField = -4812 },
				(a, b) => { b.SByteField.ShouldBe(a.SByteField); b.DecimalField.ShouldBe(a.DecimalField); b.TrailingField.ShouldBe(a.TrailingField); });

			RoundTrip(new DecimalAndSBytePocoBigEndian { SByteField = -128, DecimalField = -123.456m, TrailingField = -4812 },
				(a, b) => { b.SByteField.ShouldBe(a.SByteField); b.DecimalField.ShouldBe(a.DecimalField); b.TrailingField.ShouldBe(a.TrailingField); });

			RoundTrip(new AdvancedTypesPoco { DateTimeField = FixedUtc, GuidField = FixedGuid, SomeValue = 255 },
				(a, b) => { b.GuidField.ShouldBe(a.GuidField); b.SomeValue.ShouldBe(a.SomeValue); });

			// umlauts and the euro sign must survive the unicode encoder unchanged
			RoundTrip(new SimplePocoWithUnicodeString { JobId = 4711, SomeImportantCode = "Grüße €" },
				(a, b) => { b.JobId.ShouldBe(a.JobId); b.SomeImportantCode.ShouldBe(a.SomeImportantCode); });

			RoundTrip(new SimplePocoWithGapAndString { JobId = 4711, IsRequest = true, IsStart = true, SomeImportantCode = "ABCDE" },
				(a, b) => { b.JobId.ShouldBe(a.JobId); b.IsRequest.ShouldBe(a.IsRequest); b.SomeImportantCode.ShouldBe(a.SomeImportantCode); });

			RoundTrip(new ComplexProtocolFixedStringLength { StrField1 = "ABC", StrField2 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" },
				(a, b) => { b.StrField1.ShouldBe("ABCZZZZZZZ"); b.StrField2.ShouldBe("ABCDEFGHIJ"); });
		}


		private void RoundTrip<TPoco>(TPoco source, Action<TPoco, TPoco> assert)
			where TPoco : class, new()
		{
			var converter = new ProtocolConverter<TPoco>(_logger) as IProtocolConverter<TPoco>;
			converter.Prepare();

			var bytes = converter.ConvertToByteArray(source);
			var roundTrip = converter.ConvertFromByteArray(bytes);

			assert(source, roundTrip);
		}


		/// <summary>
		/// The range limits of float and double are written as strings in the attributes. They must be
		/// read with the invariant culture, so "1.100" is one point one, never one thousand one hundred.
		/// The existing range test only asserted on an integer field, so this semantic was unpinned.
		/// </summary>
		[Fact]
		public void TestFloatAndDoubleRangeLimitsAreInvariant()
		{
			var previous = CultureInfo.CurrentCulture;
			try
			{
				// on a German machine "1.100" used to be parsed as one thousand one hundred
				CultureInfo.CurrentCulture = new CultureInfo("de-DE");

				var converter = new ProtocolConverter<PocoWithRanges>(_logger) as IProtocolConverter<PocoWithRanges>;
				converter.Prepare();

				// 99 is outside of 1.1 .. 2.2 as well as outside of the wrongly parsed 1100 .. 2200,
				// so the violation fires either way and only the replacement value tells them apart
				var payload = converter.ConvertToByteArray(new PocoWithRanges { FloatField = 99f, DoubleField = 99d });

				void SetToMin(System.Reflection.MemberInfo member, out Abstractions.Enums.ConverterRangeViolationBehaviour chosen)
					=> chosen = Abstractions.Enums.ConverterRangeViolationBehaviour.SetToMinValue;

				converter.OnRangeViolation += SetToMin;
				try
				{
					var result = converter.ConvertFromByteArray(payload);

					result.FloatField.ShouldBe(1.1f, "\"1.100\" must mean one point one, not one thousand one hundred");
					result.DoubleField.ShouldBe(3.1415d, "\"3.1415\" must mean pi, not thirty one thousand");
				}
				finally
				{
					converter.OnRangeViolation -= SetToMin;
				}
			}
			finally
			{
				CultureInfo.CurrentCulture = previous;
			}
		}


		/// <summary>
		/// FIXED in v4: writing a Guid used to ignore UseBigEndian while reading reversed all 16
		/// bytes, so a big endian Guid came back mirrored. Both directions reverse now.
		///
		/// The resulting order is a full 16 byte reversal of Guid.ToByteArray() and deliberately
		/// NOT RFC 4122. It was chosen to keep the existing reader working.
		/// </summary>
		[Fact]
		public void TestGuidRoundTripsInBothEndianess()
		{
			var little = new ProtocolConverter<GuidLittleEndianPoco>(_logger) as IProtocolConverter<GuidLittleEndianPoco>;
			little.Prepare();

			var littleWritten = little.ConvertToByteArray(new GuidLittleEndianPoco { GuidField = FixedGuid });

			littleWritten.ShouldBe(FixedGuid.ToByteArray(), "little endian must be the plain ToByteArray order");
			little.ConvertFromByteArray(littleWritten).GuidField.ShouldBe(FixedGuid);

			var big = new ProtocolConverter<GuidBigEndianPoco>(_logger) as IProtocolConverter<GuidBigEndianPoco>;
			big.Prepare();

			var bigWritten = big.ConvertToByteArray(new GuidBigEndianPoco { GuidField = FixedGuid });

			bigWritten.ShouldBe(FixedGuid.ToByteArray().Reverse().ToArray(), "big endian must be the reversed order");
			big.ConvertFromByteArray(bigWritten).GuidField.ShouldBe(FixedGuid, "a big endian Guid must round trip");

			// the two encodings must be mirror images of each other
			bigWritten.ShouldBe(littleWritten.Reverse().ToArray());
		}


		/// <summary>
		/// DOCUMENTED BEHAVIOUR, not a bug: ProtocolSetupArgument.OffsetInByteArray is honoured when
		/// reading but ignored when writing. The readme describes it as "the converter skips n bytes
		/// at the beginning" and ComplexProtocolWithOffset carries the comment
		/// "this counts only for conversion FROM byte array".
		///
		/// The consequence is that a POCO with an offset cannot be fed its own output back. This test
		/// pins that so the day someone decides to make it symmetric, it is a visible change.
		/// </summary>
		[Fact]
		public void TestGlobalOffsetAppliesToReadingOnly()
		{
			var converter = new ProtocolConverter<DumbPoco>(_logger) as IProtocolConverter<DumbPoco>;
			converter.Prepare();

			var written = converter.ConvertToByteArray(new DumbPoco
			{
				ShortField = -4711,
				IntField = -4812,
				UShortField = 4711,
			});

			// the offset of 4 bytes is not written, so the payload is 4 bytes short for the reader
			written.Length.ShouldBe(32);

			var error = Should.Throw<ProtocolConverterException>(() => converter.ConvertFromByteArray(written));

			// the message must explain the offset, this is the trap people fall into
			error.Message.ShouldContain("OffsetInByteArray");
			error.Message.ShouldContain("reading only");
			error.Message.ShouldContain("ProtocolBytePadding");

			// prefixing the missing offset bytes makes the very same payload readable
			var withOffset = new byte[written.Length + 4];
			Array.Copy(written, 0, withOffset, 4, written.Length);

			var roundTrip = converter.ConvertFromByteArray(withOffset);

			roundTrip.ShortField.ShouldBe((short) -4711);
			roundTrip.IntField.ShouldBe(-4812);
			roundTrip.UShortField.ShouldBe((ushort) 4711);
		}
	}
}
