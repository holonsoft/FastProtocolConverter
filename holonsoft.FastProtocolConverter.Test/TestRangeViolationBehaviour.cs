using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using Shouldly;
using System.Reflection;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	/// <summary>
	/// Range violation handling across every numeric type that supports a range, for both byte
	/// orders. Before this existed each behaviour had exactly one test on one int field, so a
	/// per type mistake (for example SetToMinValue being wrong for ushort but right for int)
	/// would not have been noticed.
	///
	/// These are read direction paths, so the byte exact golden vectors cannot cover them.
	/// </summary>
	public class TestRangeViolationBehaviour
	{
		private readonly ILogger _logger = null;

		/// <summary>
		/// Every value is far outside the range declared for its field.
		/// </summary>
		private static AllNumericRangesPoco OutOfRangeLittleEndian() => new()
		{
			SByteField = 100,
			ShortField = 5000,
			UShortField = 5000,
			IntField = 500_000,
			UIntField = 500_000,
			LongField = 50_000_000_000,
			ULongField = 50_000_000_000,
			FloatField = 99.5f,
			DoubleField = 99.5d,
			DecimalField = 99.5m,
		};


		private static AllNumericRangesBigEndianPoco OutOfRangeBigEndian() => new()
		{
			SByteField = 100,
			ShortField = 5000,
			UShortField = 5000,
			IntField = 500_000,
			UIntField = 500_000,
			LongField = 50_000_000_000,
			ULongField = 50_000_000_000,
			FloatField = 99.5f,
			DoubleField = 99.5d,
			DecimalField = 99.5m,
		};


		private static IProtocolConverter<TPoco> CreateConverter<TPoco>(ILogger logger)
			where TPoco : class, new()
		{
			var converter = new ProtocolConverter<TPoco>(logger) as IProtocolConverter<TPoco>;
			converter.Prepare();
			return converter;
		}


		/// <summary>
		/// Expected field values per behaviour, so a per type mistake shows up as a named field.
		/// </summary>
		private static void AssertAllFields(
			ConverterRangeViolationBehaviour behaviour,
			sbyte sByteValue, short shortValue, ushort uShortValue, int intValue, uint uIntValue,
			long longValue, ulong uLongValue, float floatValue, double doubleValue, decimal decimalValue,
			AllNumericRangesPoco actual)
		{
			actual.SByteField.ShouldBe(sByteValue, $"{behaviour} on sbyte");
			actual.ShortField.ShouldBe(shortValue, $"{behaviour} on short");
			actual.UShortField.ShouldBe(uShortValue, $"{behaviour} on ushort");
			actual.IntField.ShouldBe(intValue, $"{behaviour} on int");
			actual.UIntField.ShouldBe(uIntValue, $"{behaviour} on uint");
			actual.LongField.ShouldBe(longValue, $"{behaviour} on long");
			actual.ULongField.ShouldBe(uLongValue, $"{behaviour} on ulong");
			actual.FloatField.ShouldBe(floatValue, $"{behaviour} on float");
			actual.DoubleField.ShouldBe(doubleValue, $"{behaviour} on double");
			actual.DecimalField.ShouldBe(decimalValue, $"{behaviour} on decimal");
		}


		private AllNumericRangesPoco ReadWithBehaviour(ConverterRangeViolationBehaviour behaviour)
		{
			var converter = CreateConverter<AllNumericRangesPoco>(_logger);
			var payload = converter.ConvertToByteArray(OutOfRangeLittleEndian());

			void Handler(FieldInfo field, out ConverterRangeViolationBehaviour chosen) => chosen = behaviour;

			converter.OnRangeViolation += Handler;
			try
			{
				return converter.ConvertFromByteArray(payload);
			}
			finally
			{
				converter.OnRangeViolation -= Handler;
			}
		}


		/// <summary>
		/// The value stays exactly as it was read, the violation is only reported.
		/// </summary>
		[Fact]
		public void TestIgnoreAndContinueKeepsTheReadValue()
		{
			var result = ReadWithBehaviour(ConverterRangeViolationBehaviour.IgnoreAndContinue);

			AssertAllFields(ConverterRangeViolationBehaviour.IgnoreAndContinue,
				100, 5000, 5000, 500_000, 500_000, 50_000_000_000, 50_000_000_000, 99.5f, 99.5d, 99.5m,
				result);
		}


		/// <summary>
		/// None is what the handler leaves behind when it does not assign anything, and it must
		/// behave like IgnoreAndContinue rather than silently zeroing the field.
		/// </summary>
		[Fact]
		public void TestNoneBehavesLikeIgnoreAndContinue()
		{
			var result = ReadWithBehaviour(ConverterRangeViolationBehaviour.None);

			AssertAllFields(ConverterRangeViolationBehaviour.None,
				100, 5000, 5000, 500_000, 500_000, 50_000_000_000, 50_000_000_000, 99.5f, 99.5d, 99.5m,
				result);
		}


		[Fact]
		public void TestSetToMinValueUsesThePerFieldMinimum()
		{
			var result = ReadWithBehaviour(ConverterRangeViolationBehaviour.SetToMinValue);

			AssertAllFields(ConverterRangeViolationBehaviour.SetToMinValue,
				-10, -1000, 10, -100_000, 10, -10_000_000_000, 10, -1.5f, -2.5d, -3.5m,
				result);
		}


		[Fact]
		public void TestSetToMaxValueUsesThePerFieldMaximum()
		{
			var result = ReadWithBehaviour(ConverterRangeViolationBehaviour.SetToMaxValue);

			AssertAllFields(ConverterRangeViolationBehaviour.SetToMaxValue,
				10, 1000, 1000, 100_000, 100_000, 10_000_000_000, 10_000_000_000, 1.5f, 2.5d, 3.5m,
				result);
		}


		[Fact]
		public void TestSetToDefaultValueUsesThePerFieldDefault()
		{
			var result = ReadWithBehaviour(ConverterRangeViolationBehaviour.SetToDefaultValue);

			AssertAllFields(ConverterRangeViolationBehaviour.SetToDefaultValue,
				7, 700, 700, 70_000, 70_000, 7_000_000_000, 7_000_000_000, 0.5f, 0.5d, 0.5m,
				result);
		}


		[Fact]
		public void TestThrowExceptionStopsProcessing()
		{
			var converter = CreateConverter<AllNumericRangesPoco>(_logger);
			var payload = converter.ConvertToByteArray(OutOfRangeLittleEndian());

			void Handler(FieldInfo field, out ConverterRangeViolationBehaviour chosen)
				=> chosen = ConverterRangeViolationBehaviour.ThrowException;

			converter.OnRangeViolation += Handler;
			try
			{
				Should.Throw<ProtocolConverterException>(() => converter.ConvertFromByteArray(payload));
			}
			finally
			{
				converter.OnRangeViolation -= Handler;
			}
		}


		/// <summary>
		/// The replacement values must not depend on the byte order.
		/// </summary>
		[Theory]
		[InlineData(ConverterRangeViolationBehaviour.SetToMinValue)]
		[InlineData(ConverterRangeViolationBehaviour.SetToMaxValue)]
		[InlineData(ConverterRangeViolationBehaviour.SetToDefaultValue)]
		[InlineData(ConverterRangeViolationBehaviour.IgnoreAndContinue)]
		public void TestBigEndianProducesTheSameReplacementValues(ConverterRangeViolationBehaviour behaviour)
		{
			var little = ReadWithBehaviour(behaviour);

			var converter = CreateConverter<AllNumericRangesBigEndianPoco>(_logger);
			var payload = converter.ConvertToByteArray(OutOfRangeBigEndian());

			void Handler(FieldInfo field, out ConverterRangeViolationBehaviour chosen) => chosen = behaviour;

			converter.OnRangeViolation += Handler;
			AllNumericRangesBigEndianPoco big;
			try
			{
				big = converter.ConvertFromByteArray(payload);
			}
			finally
			{
				converter.OnRangeViolation -= Handler;
			}

			big.SByteField.ShouldBe(little.SByteField, $"{behaviour} on sbyte");
			big.ShortField.ShouldBe(little.ShortField, $"{behaviour} on short");
			big.UShortField.ShouldBe(little.UShortField, $"{behaviour} on ushort");
			big.IntField.ShouldBe(little.IntField, $"{behaviour} on int");
			big.UIntField.ShouldBe(little.UIntField, $"{behaviour} on uint");
			big.LongField.ShouldBe(little.LongField, $"{behaviour} on long");
			big.ULongField.ShouldBe(little.ULongField, $"{behaviour} on ulong");
			big.FloatField.ShouldBe(little.FloatField, $"{behaviour} on float");
			big.DoubleField.ShouldBe(little.DoubleField, $"{behaviour} on double");
			big.DecimalField.ShouldBe(little.DecimalField, $"{behaviour} on decimal");
		}


		/// <summary>
		/// A value inside the range must never trigger the handler at all.
		/// </summary>
		[Fact]
		public void TestValuesInRangeDoNotTriggerTheHandler()
		{
			var converter = CreateConverter<AllNumericRangesPoco>(_logger);

			var inRange = new AllNumericRangesPoco
			{
				SByteField = 1, ShortField = 1, UShortField = 100, IntField = 1, UIntField = 100,
				LongField = 1, ULongField = 100, FloatField = 1.0f, DoubleField = 1.0d, DecimalField = 1.0m,
			};

			var payload = converter.ConvertToByteArray(inRange);

			var violations = 0;

			void Handler(FieldInfo field, out ConverterRangeViolationBehaviour chosen)
			{
				violations++;
				chosen = ConverterRangeViolationBehaviour.IgnoreAndContinue;
			}

			converter.OnRangeViolation += Handler;
			try
			{
				var result = converter.ConvertFromByteArray(payload);

				violations.ShouldBe(0, "no field is outside its range");
				result.DecimalField.ShouldBe(1.0m);
				result.ULongField.ShouldBe(100ul);
			}
			finally
			{
				converter.OnRangeViolation -= Handler;
			}
		}
	}
}
