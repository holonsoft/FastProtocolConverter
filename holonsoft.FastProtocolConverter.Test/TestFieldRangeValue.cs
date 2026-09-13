using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.dto;
using Shouldly;
using System;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	public class TestFieldRangeValue
	{
		/// <summary>
		/// Verifies that the boundaries of a range are inclusive and that values outside of it are rejected.
		/// The values are passed per type because <see cref="FieldRangeValue{T}"/> is strongly typed.
		/// </summary>
		private static void VerifyRange<T>(T min, T max, T[] inRange, T[] outOfRange)
			where T : IComparable<T>
		{
			var range = new FieldRangeValue<T>(min, max);

			foreach (var value in inRange)
			{
				range.IsInRange(value).ShouldBeTrue($"{typeof(T).Name}: {value} should be inside [{min};{max}]");
			}

			foreach (var value in outOfRange)
			{
				range.IsInRange(value).ShouldBeFalse($"{typeof(T).Name}: {value} should be outside [{min};{max}]");
			}
		}


		private static void VerifyDefaultValue<T>(T min, T max, T defaultValue, T otherValue)
			where T : IComparable<T>
		{
			var range = new FieldRangeValue<T>(min, max, defaultValue);

			range.IsDefaultValue(defaultValue).ShouldBeTrue($"{typeof(T).Name}: {defaultValue} is the default value");
			range.IsDefaultValue(otherValue).ShouldBeFalse($"{typeof(T).Name}: {otherValue} is not the default value");
		}


		[Fact]
		public void TestRangeNoDefault()
		{
			VerifyRange<int>(0, 100, [10, 0, 100], [-1, 101]);
			VerifyRange<uint>(0, 100, [10, 0, 100], [101]);
			VerifyRange<long>(0, 100, [10, 0, 100], [-1, 101]);
			VerifyRange<ulong>(0, 100, [10, 0, 100], [101]);
			VerifyRange<short>(0, 100, [10, 0, 100], [-1, 101]);
			VerifyRange<ushort>(0, 100, [10, 0, 100], [101]);
			VerifyRange<float>(0, 100, [10, 0, 100], [-1, 101]);
			VerifyRange<double>(0, 100, [10, 0, 100], [-1, 101]);
		}


		[Fact]
		public void TestRangeWithDefault()
		{
			VerifyDefaultValue<int>(0, 100, 50, 150);
			VerifyDefaultValue<uint>(0, 100, 50, 150);
			VerifyDefaultValue<long>(0, 100, 50, 150);
			VerifyDefaultValue<ulong>(0, 100, 50, 150);
			VerifyDefaultValue<short>(0, 100, 50, 150);
			VerifyDefaultValue<ushort>(0, 100, 50, 150);
			VerifyDefaultValue<float>(0.00f, 100.00f, 50.00f, 150.00f);
			VerifyDefaultValue<double>(0.0000d, 100.0000d, 50.0000d, 150.0000d);

			Should.Throw<ProtocolConverterException>(() => new FieldRangeValue<string>("", "", ""));
		}
	}
}
