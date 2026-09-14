using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using System.Globalization;
using System.Reflection;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	/// <summary>
	/// Covers the decimal and sbyte support introduced in v4. Both types were advertised
	/// in the readme before, but had no converter at all.
	/// </summary>
	public class TestDecimalAndSByte
	{
		private readonly ILogger _logger = null;

		private static IProtocolConverter<TPoco> CreateConverter<TPoco>(ILogger logger)
			where TPoco : class, new()
		{
			var converter = new ProtocolConverter<TPoco>(logger) as IProtocolConverter<TPoco>;
			converter.Prepare();
			return converter;
		}


		/// <summary>
		/// sbyte covers the full signed range, the most interesting values are the boundaries
		/// and the sign flip at 0x80 which a naive byte cast gets wrong.
		/// </summary>
		[Theory]
		[InlineData((sbyte) 0)]
		[InlineData((sbyte) 1)]
		[InlineData((sbyte) -1)]
		[InlineData(sbyte.MinValue)]
		[InlineData(sbyte.MaxValue)]
		public void TestSByteRoundTrip(sbyte value)
		{
			var converter = CreateConverter<DecimalAndSBytePoco>(_logger);

			var source = new DecimalAndSBytePoco { SByteField = value, DecimalField = 0m, TrailingField = 4711 };

			var bytes = converter.ConvertToByteArray(source);
			var roundTrip = converter.ConvertFromByteArray(bytes);

			roundTrip.SByteField.ShouldBe(value);
			roundTrip.TrailingField.ShouldBe(4711);
		}


		/// <summary>
		/// The sign bit must survive as a single raw byte, so -1 has to be 0xFF on the wire
		/// and sbyte.MinValue has to be 0x80.
		/// </summary>
		[Theory]
		[InlineData((sbyte) -1, (byte) 0xFF)]
		[InlineData(sbyte.MinValue, (byte) 0x80)]
		[InlineData(sbyte.MaxValue, (byte) 0x7F)]
		[InlineData((sbyte) 0, (byte) 0x00)]
		public void TestSByteWireRepresentation(sbyte value, byte expectedByte)
		{
			var converter = CreateConverter<DecimalAndSBytePoco>(_logger);

			var bytes = converter.ConvertToByteArray(new DecimalAndSBytePoco { SByteField = value });

			bytes[0].ShouldBe(expectedByte);
		}


		public static TheoryData<string> DecimalValues()
			=> new()
			{
				"0",
				"1",
				"-1",
				"0.0000000000000000000000000001",   // smallest positive, full scale
				"-0.0000000000000000000000000001",
				"79228162514264337593543950335",    // decimal.MaxValue
				"-79228162514264337593543950335",   // decimal.MinValue
				"123.456",
				"-123.456",
				"1.000",                            // trailing zeros carry scale information
			};


		/// <summary>
		/// A decimal keeps its scale, so 1.000 and 1 are equal but not identical. The round trip
		/// must preserve the exact bit representation, which GetBits/new decimal(bits) guarantees.
		/// </summary>
		[Theory]
		[MemberData(nameof(DecimalValues))]
		public void TestDecimalRoundTripLittleEndian(string literal)
		{
			var value = decimal.Parse(literal, CultureInfo.InvariantCulture);
			var converter = CreateConverter<DecimalAndSBytePoco>(_logger);

			var bytes = converter.ConvertToByteArray(new DecimalAndSBytePoco { DecimalField = value, TrailingField = -1 });
			var roundTrip = converter.ConvertFromByteArray(bytes);

			roundTrip.DecimalField.ShouldBe(value);
			// scale must survive, not only the numeric value
			decimal.GetBits(roundTrip.DecimalField).ShouldBe(decimal.GetBits(value));
			roundTrip.TrailingField.ShouldBe(-1);
		}


		[Theory]
		[MemberData(nameof(DecimalValues))]
		public void TestDecimalRoundTripBigEndian(string literal)
		{
			var value = decimal.Parse(literal, CultureInfo.InvariantCulture);
			var converter = CreateConverter<DecimalAndSBytePocoBigEndian>(_logger);

			var bytes = converter.ConvertToByteArray(new DecimalAndSBytePocoBigEndian { DecimalField = value, TrailingField = -1 });
			var roundTrip = converter.ConvertFromByteArray(bytes);

			roundTrip.DecimalField.ShouldBe(value);
			decimal.GetBits(roundTrip.DecimalField).ShouldBe(decimal.GetBits(value));
			roundTrip.TrailingField.ShouldBe(-1);
		}


		/// <summary>
		/// Little and big endian must produce the same 16 bytes in reversed component order,
		/// otherwise the two paths are not consistent with each other.
		/// </summary>
		[Fact]
		public void TestDecimalEndianessIsMirrored()
		{
			var value = 123.456m;

			var little = CreateConverter<DecimalAndSBytePoco>(_logger)
				.ConvertToByteArray(new DecimalAndSBytePoco { DecimalField = value });

			var big = CreateConverter<DecimalAndSBytePocoBigEndian>(_logger)
				.ConvertToByteArray(new DecimalAndSBytePocoBigEndian { DecimalField = value });

			// the decimal occupies bytes 1..16, four components of four bytes each
			for (var component = 0; component < 4; component++)
			{
				for (var b = 0; b < 4; b++)
				{
					big[1 + (component * 4) + b].ShouldBe(little[1 + (component * 4) + (3 - b)],
						$"component {component}, byte {b}");
				}
			}
		}


		[Fact]
		public void TestDecimalOccupiesSixteenBytes()
		{
			var converter = CreateConverter<DecimalAndSBytePoco>(_logger);

			var bytes = converter.ConvertToByteArray(new DecimalAndSBytePoco());

			// 1 byte sbyte + 16 byte decimal + 4 byte int
			bytes.Length.ShouldBe(21);
		}


		/// <summary>
		/// Not every 16 byte pattern is a valid decimal. The flags component has reserved bits,
		/// and an invalid scale must surface as a ProtocolConverterException, never as a raw
		/// ArgumentException from the BCL.
		/// </summary>
		[Fact]
		public void TestInvalidDecimalBitPatternThrowsProtocolConverterException()
		{
			var converter = CreateConverter<DecimalAndSBytePoco>(_logger);

			var bytes = new byte[21];
			// flags component (bytes 13..16 little endian): scale 40 is out of the allowed 0..28
			bytes[13 + 2] = 40;

			Should.Throw<ProtocolConverterException>(() => converter.ConvertFromByteArray(bytes));
		}


		/// <summary>
		/// Range definitions live in attributes as strings. They must be read with the invariant
		/// culture, otherwise the very same protocol definition means different things on a
		/// German and an English machine.
		/// </summary>
		[Fact]
		public void TestRangeAttributesAreCultureInvariant()
		{
			var previous = CultureInfo.CurrentCulture;
			try
			{
				CultureInfo.CurrentCulture = new CultureInfo("de-DE");

				var converter = CreateConverter<DecimalAndSByteWithRangesPoco>(_logger);

				var inRange = converter.ConvertToByteArray(
					new DecimalAndSByteWithRangesPoco { SByteField = 5, DecimalField = 100.5m });

				var roundTrip = converter.ConvertFromByteArray(inRange);

				roundTrip.DecimalField.ShouldBe(100.5m);
				roundTrip.SByteField.ShouldBe((sbyte) 5);
			}
			finally
			{
				CultureInfo.CurrentCulture = previous;
			}
		}


		[Theory]
		[InlineData(ConverterRangeViolationBehaviour.SetToMinValue, "-10")]
		[InlineData(ConverterRangeViolationBehaviour.SetToMaxValue, "10")]
		[InlineData(ConverterRangeViolationBehaviour.SetToDefaultValue, "7")]
		[InlineData(ConverterRangeViolationBehaviour.IgnoreAndContinue, "100")]
		public void TestSByteRangeViolationBehaviour(ConverterRangeViolationBehaviour behaviour, string expected)
		{
			var converter = CreateConverter<DecimalAndSByteWithRangesPoco>(_logger);

			void Handler(MemberInfo member, out ConverterRangeViolationBehaviour chosen) => chosen = behaviour;

			converter.OnRangeViolation += Handler;
			try
			{
				// 100 is far outside of the configured -10..10 range
				var bytes = new byte[17];
				bytes[0] = 100;

				var result = converter.ConvertFromByteArray(bytes);

				result.SByteField.ShouldBe(sbyte.Parse(expected, CultureInfo.InvariantCulture));
			}
			finally
			{
				converter.OnRangeViolation -= Handler;
			}
		}


		[Fact]
		public void TestSByteRangeViolationCanThrow()
		{
			var converter = CreateConverter<DecimalAndSByteWithRangesPoco>(_logger);

			void Handler(MemberInfo member, out ConverterRangeViolationBehaviour chosen)
				=> chosen = ConverterRangeViolationBehaviour.ThrowException;

			converter.OnRangeViolation += Handler;
			try
			{
				var bytes = new byte[17];
				bytes[0] = 100;

				Should.Throw<ProtocolConverterException>(() => converter.ConvertFromByteArray(bytes));
			}
			finally
			{
				converter.OnRangeViolation -= Handler;
			}
		}


		/// <summary>
		/// A protocol definition may name its own culture for the range limits. That is safe because
		/// the culture is declared in the source and travels with it, unlike the ambient culture of
		/// the machine, which used to decide the meaning of "1.100" silently.
		/// </summary>
		[Fact]
		public void TestExplicitRangeCultureIsHonoured()
		{
			var previous = CultureInfo.CurrentCulture;
			try
			{
				// deliberately hostile ambient culture, it must not influence anything
				CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

				var converter = CreateConverter<GermanRangeCulturePoco>(_logger);

				var payload = converter.ConvertToByteArray(new GermanRangeCulturePoco { FloatField = 99f });

				void SetToMin(MemberInfo member, out ConverterRangeViolationBehaviour chosen)
					=> chosen = ConverterRangeViolationBehaviour.SetToMinValue;

				converter.OnRangeViolation += SetToMin;
				try
				{
					// "1,1" read with de-DE is one point one
					converter.ConvertFromByteArray(payload).FloatField.ShouldBe(1.1f);
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
		/// A typo in the culture name must fail loudly at Prepare time, not silently fall back to
		/// something else and change what the range means.
		/// </summary>
		[Fact]
		public void TestInvalidRangeCultureThrowsAtPrepare()
		{
			var converter = new ProtocolConverter<InvalidRangeCulturePoco>(_logger) as IProtocolConverter<InvalidRangeCulturePoco>;

			var error = Should.Throw<ProtocolConverterException>(() => converter.Prepare());

			error.Message.ShouldContain("not-a-culture");
			error.Message.ShouldContain("RangeCulture");
		}


		/// <summary>
		/// Without an explicit RangeCulture the invariant culture applies, whatever the machine is
		/// configured to. This is the regression guard for the original bug.
		/// </summary>
		[Fact]
		public void TestRangeCultureDefaultsToInvariantOnAnyMachine()
		{
			var previous = CultureInfo.CurrentCulture;
			try
			{
				CultureInfo.CurrentCulture = new CultureInfo("de-DE");

				var converter = CreateConverter<DecimalAndSByteWithRangesPoco>(_logger);

				// "-100.5" must stay minus one hundred point five, not minus one thousand and five
				var roundTrip = converter.ConvertFromByteArray(converter.ConvertToByteArray(
					new DecimalAndSByteWithRangesPoco { SByteField = 0, DecimalField = -100.5m }));

				roundTrip.DecimalField.ShouldBe(-100.5m);
			}
			finally
			{
				CultureInfo.CurrentCulture = previous;
			}
		}


		[Fact]
		public void TestDecimalRangeViolationCanThrow()
		{
			var converter = CreateConverter<DecimalAndSByteWithRangesPoco>(_logger);

			void Handler(MemberInfo member, out ConverterRangeViolationBehaviour chosen)
				=> chosen = ConverterRangeViolationBehaviour.ThrowException;

			converter.OnRangeViolation += Handler;
			try
			{
				// build a payload whose decimal is 1000, well outside -100.5..100.5
				var outOfRange = converter.ConvertToByteArray(
					new DecimalAndSByteWithRangesPoco { SByteField = 0, DecimalField = 1000m });

				Should.Throw<ProtocolConverterException>(() => converter.ConvertFromByteArray(outOfRange));
			}
			finally
			{
				converter.OnRangeViolation -= Handler;
			}
		}
	}
}
