using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	public class TestFieldRangeValue
	{
		[Fact]
		public void TestRangeNoDefault()
		{
			dynamic a = new FieldRangeValue<int>(0, 100);
			Assert.True(a.IsInRange(10));
			Assert.True(a.IsInRange(0));
			Assert.True(a.IsInRange(100));
			Assert.False(a.IsInRange(-1));
			Assert.False(a.IsInRange(101));

			a = new FieldRangeValue<uint>(0, 100);
			Assert.True(a.IsInRange(10));
			Assert.True(a.IsInRange(0));
			Assert.True(a.IsInRange(100));
			Assert.False(a.IsInRange(101));

			a = new FieldRangeValue<long>(0, 100);
			Assert.True(a.IsInRange(10));
			Assert.True(a.IsInRange(0));
			Assert.True(a.IsInRange(100));
			Assert.False(a.IsInRange(-1));
			Assert.False(a.IsInRange(101));

			a = new FieldRangeValue<ulong>(0, 100);
			Assert.True(a.IsInRange(10));
			Assert.True(a.IsInRange(0));
			Assert.True(a.IsInRange(100));
			Assert.False(a.IsInRange(101));

			a = new FieldRangeValue<short>(0, 100);
			Assert.True(a.IsInRange(10));
			Assert.True(a.IsInRange(0));
			Assert.True(a.IsInRange(100));
			Assert.False(a.IsInRange(-1));
			Assert.False(a.IsInRange(101));

			a = new FieldRangeValue<ushort>(0, 100);
			Assert.True(a.IsInRange(10));
			Assert.True(a.IsInRange(0));
			Assert.True(a.IsInRange(100));
			Assert.False(a.IsInRange(101));

			a = new FieldRangeValue<float>(0, 100);
			Assert.True(a.IsInRange(10));
			Assert.True(a.IsInRange(0));
			Assert.True(a.IsInRange(100));
			Assert.False(a.IsInRange(-1));
			Assert.False(a.IsInRange(101));

			a = new FieldRangeValue<double>(0, 100);
			Assert.True(a.IsInRange(10));
			Assert.True(a.IsInRange(0));
			Assert.True(a.IsInRange(100));
			Assert.False(a.IsInRange(-1));
			Assert.False(a.IsInRange(101));
		}


		[Fact]
		public void TestRangeWithDefault()
		{
			dynamic a = new FieldRangeValue<int>(0, 100, 50);
			Assert.True(a.IsDefaultValue(50));
			Assert.False(a.IsDefaultValue(150));

			a = new FieldRangeValue<uint>(0, 100, 50);
			Assert.True(a.IsDefaultValue(50));
			Assert.False(a.IsDefaultValue(150));

			a = new FieldRangeValue<long>(0, 100, 50);
			Assert.True(a.IsDefaultValue(50));
			Assert.False(a.IsDefaultValue(150));

			a = new FieldRangeValue<ulong>(0, 100, 50);
			Assert.True(a.IsDefaultValue(50));
			Assert.False(a.IsDefaultValue(150));

			a = new FieldRangeValue<short>(0, 100, 50);
			Assert.True(a.IsDefaultValue(50));
			Assert.False(a.IsDefaultValue(150));

			a = new FieldRangeValue<ushort>(0, 100, 50);
			Assert.True(a.IsDefaultValue(50));
			Assert.False(a.IsDefaultValue(150));

			a = new FieldRangeValue<float>(0.00f, 100.00f, 50.00f);
			Assert.True(a.IsDefaultValue(50.00f));
			Assert.False(a.IsDefaultValue(150.00f));

			a = new FieldRangeValue<double>(0.0000d, 100.0000d, 50.0000d);
			Assert.True(a.IsDefaultValue(50.00d));
			Assert.False(a.IsDefaultValue(150.00d));

			Assert.Throws<ProtocolConverterException>(() => new FieldRangeValue<string>("", "", ""));
		}
	}
}