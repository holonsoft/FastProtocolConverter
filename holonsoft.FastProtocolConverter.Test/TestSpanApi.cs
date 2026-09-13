using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using System.Reflection;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	/// <summary>
	/// The ReadOnlySpan read overloads. Their whole point is that a caller whose bytes already live
	/// somewhere else (a rented buffer, a slice of a receive buffer, the stack) does not have to copy
	/// into a byte[] first, and that copy is bigger than everything the converter itself allocates.
	///
	/// So the tests prove two things: the span path produces exactly the same POCO as the array path
	/// for every protocol shape, and it stays correct when the span is not backed by an array that
	/// starts at index 0.
	/// </summary>
	public class TestSpanApi
	{
		private readonly ILogger _logger = null;


		private IProtocolConverter<TPoco> CreateConverter<TPoco>()
			where TPoco : class, new()
		{
			var converter = new ProtocolConverter<TPoco>(_logger) as IProtocolConverter<TPoco>;
			converter.Prepare();
			return converter;
		}


		/// <summary>
		/// Compares every field by reflection instead of by hand, so a field added to a test POCO
		/// later is covered without anybody remembering to extend this.
		/// </summary>
		private static void ShouldHaveSameFieldValues<TPoco>(TPoco expected, TPoco actual)
		{
			foreach (var field in typeof(TPoco).GetFields(BindingFlags.Public | BindingFlags.Instance))
			{
				field.GetValue(actual).ShouldBe(field.GetValue(expected), $"field {field.Name}");
			}
		}


		/// <summary>
		/// Fixed position protocol, including an offset, enums of three widths and floating point.
		/// </summary>
		[Fact]
		public void TestSpanReadMatchesArrayReadForFixedPositionProtocol()
		{
			var converter = CreateConverter<DumbPoco>();

			var source = new DumbPoco
			{
				ShortField = -1234,
				IntField = 987_654,
				ByteField = 42,
				EnumField1 = MyImportantEnum.B,
				EnumField2 = MyImportantEnum.C,
				EnumField3 = MyImportantEnum.A,
				FloatField = -17.25f,
				DoubleField = 1234.5678d,
				UIntField = 4_000_000_000,
				UShortField = 60_000,
			};

			// DumbPoco declares OffsetInByteArray = 4 and that offset applies to reading only,
			// so the written frame has to be prefixed before it can be read back
			var written = converter.ConvertToByteArray(source);
			var frame = new byte[written.Length + 4];
			written.CopyTo(frame, 4);

			var fromArray = converter.ConvertFromByteArray(frame);
			var fromSpan = converter.ConvertFromByteArray(new ReadOnlySpan<byte>(frame));

			ShouldHaveSameFieldValues(fromArray, fromSpan);
			fromSpan.IntField.ShouldBe(987_654);
			fromSpan.UIntField.ShouldBe(4_000_000_000u);
		}


		/// <summary>
		/// Sequence protocol with two variable length strings and a bit field, so the span travels
		/// through ResolveComplexProtocol rather than the fixed position loop.
		/// </summary>
		[Fact]
		public void TestSpanReadMatchesArrayReadForSequenceProtocol()
		{
			var converter = CreateConverter<ComplexProtocol>();

			var source = new ComplexProtocol
			{
				LengthOfStr1 = 5,
				String1 = "Hello",
				LengthOfStr2 = 5,
				String2 = "World",
				IntField = 4711,
				ShortField = 815,
				ByteField = 7,
				EnumField1 = MyImportantEnum.C,
				EnumField2 = MyImportantEnum.B,
				EnumField3 = MyImportantEnum.A,
				FloatField = 3.5f,
				DoubleField = 2.5d,
				UIntField = 70_000,
				UShortField = 40_000,
			};

			var frame = converter.ConvertToByteArray(source);

			var fromArray = converter.ConvertFromByteArray(frame);
			var fromSpan = converter.ConvertFromByteArray(new ReadOnlySpan<byte>(frame));

			ShouldHaveSameFieldValues(fromArray, fromSpan);
			fromSpan.String1.ShouldBe("Hello");
			fromSpan.String2.ShouldBe("World");
		}


		/// <summary>
		/// Guid, DateTime and padding bytes, the types with their own handlers.
		/// </summary>
		[Fact]
		public void TestSpanReadMatchesArrayReadForAdvancedTypes()
		{
			var converter = CreateConverter<AdvancedTypesPoco>();

			var source = new AdvancedTypesPoco
			{
				DateTimeField = new DateTime(2026, 9, 13, 12, 30, 0, DateTimeKind.Utc),
				GuidField = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"),
				SomeValue = 200,
			};

			var frame = converter.ConvertToByteArray(source);

			var fromArray = converter.ConvertFromByteArray(frame);
			var fromSpan = converter.ConvertFromByteArray(new ReadOnlySpan<byte>(frame));

			ShouldHaveSameFieldValues(fromArray, fromSpan);
			fromSpan.GuidField.ShouldBe(new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"));
		}


		/// <summary>
		/// decimal and sbyte, both of which read component wise.
		/// </summary>
		[Fact]
		public void TestSpanReadMatchesArrayReadForDecimalAndSByte()
		{
			var converter = CreateConverter<DecimalAndSBytePoco>();

			var source = new DecimalAndSBytePoco
			{
				SByteField = -42,
				DecimalField = -12345.6789m,
			};

			var frame = converter.ConvertToByteArray(source);

			var fromArray = converter.ConvertFromByteArray(frame);
			var fromSpan = converter.ConvertFromByteArray(new ReadOnlySpan<byte>(frame));

			ShouldHaveSameFieldValues(fromArray, fromSpan);
			fromSpan.DecimalField.ShouldBe(-12345.6789m);
		}


		/// <summary>
		/// Big endian through the span path, because every multi byte read has two branches and the
		/// span conversion touched both of them.
		/// </summary>
		[Fact]
		public void TestSpanReadMatchesArrayReadForBigEndian()
		{
			var converter = CreateConverter<PocoWithBigEndianessFlag>();

			var frame = converter.ConvertToByteArray(new PocoWithBigEndianessFlag
			{
				IntField = -123456,
				UIntField = 4_000_000_000,
				ShortField = -321,
				UShortField = 60_000,
				FloatField = -2.75f,
				DoubleField = 98765.4321d,
				LongField = -9_000_000_000,
				ULongField = 18_000_000_000_000_000_000,
			});

			var fromArray = converter.ConvertFromByteArray(frame);
			var fromSpan = converter.ConvertFromByteArray(new ReadOnlySpan<byte>(frame));

			ShouldHaveSameFieldValues(fromArray, fromSpan);
		}


		/// <summary>
		/// The reason the overload exists: one big receive buffer holding several frames, each read
		/// straight out of it without a copy. A span that does not start at index 0 of its backing
		/// array is exactly where an off by one in the conversion would show up.
		/// </summary>
		[Fact]
		public void TestSpanReadWorksOnASliceOfALargerBuffer()
		{
			var converter = CreateConverter<ComplexProtocol>();

			var first = converter.ConvertToByteArray(new ComplexProtocol
			{
				LengthOfStr1 = 3, String1 = "aaa",
				LengthOfStr2 = 3, String2 = "bbb",
				IntField = 1, ShortField = 1, ByteField = 1, FloatField = 1f, DoubleField = 1d,
				UIntField = 1, UShortField = 1,
			});

			var second = converter.ConvertToByteArray(new ComplexProtocol
			{
				LengthOfStr1 = 3, String1 = "xxx",
				LengthOfStr2 = 3, String2 = "yyy",
				IntField = 2, ShortField = 2, ByteField = 2, FloatField = 2f, DoubleField = 2d,
				UIntField = 2, UShortField = 2,
			});

			// one receive buffer, two frames back to back, plus garbage in front and behind
			var receiveBuffer = new byte[8 + first.Length + second.Length + 8];
			for (var i = 0; i < receiveBuffer.Length; i++)
			{
				receiveBuffer[i] = 0xCC;
			}

			first.CopyTo(receiveBuffer, 8);
			second.CopyTo(receiveBuffer, 8 + first.Length);

			var readFirst = converter.ConvertFromByteArray(
				new ReadOnlySpan<byte>(receiveBuffer, 8, first.Length));

			var readSecond = converter.ConvertFromByteArray(
				new ReadOnlySpan<byte>(receiveBuffer, 8 + first.Length, second.Length));

			readFirst.String1.ShouldBe("aaa");
			readFirst.String2.ShouldBe("bbb");
			readFirst.IntField.ShouldBe(1);

			readSecond.String1.ShouldBe("xxx");
			readSecond.String2.ShouldBe("yyy");
			readSecond.IntField.ShouldBe(2);
		}


		/// <summary>
		/// A frame that never touches the managed heap at all.
		/// </summary>
		[Fact]
		public void TestSpanReadWorksOnAStackAllocatedFrame()
		{
			var converter = CreateConverter<DecimalAndSBytePoco>();

			var frame = converter.ConvertToByteArray(new DecimalAndSBytePoco
			{
				SByteField = 99,
				DecimalField = 42.42m,
			});

			Span<byte> onStack = stackalloc byte[frame.Length];
			frame.CopyTo(onStack);

			var result = converter.ConvertFromByteArray(onStack);

			result.SByteField.ShouldBe((sbyte) 99);
			result.DecimalField.ShouldBe(42.42m);
		}


		/// <summary>
		/// Span plus a reused instance is the fully allocation free read path.
		/// </summary>
		[Fact]
		public void TestSpanReadIntoAReusedInstance()
		{
			var converter = CreateConverter<DumbPoco>();

			var written = converter.ConvertToByteArray(new DumbPoco
			{
				ShortField = 7, IntField = 8, ByteField = 9,
				FloatField = 1.5f, DoubleField = 2.5d, UIntField = 10, UShortField = 11,
			});

			var frame = new byte[written.Length + 4];
			written.CopyTo(frame, 4);

			var instance = new DumbPoco();

			converter.ConvertFromByteArray(new ReadOnlySpan<byte>(frame), instance);

			instance.IntField.ShouldBe(8);
			instance.UShortField.ShouldBe((ushort) 11);

			// the same instance filled twice must not keep anything from the first round
			var otherWritten = converter.ConvertToByteArray(new DumbPoco
			{
				ShortField = 70, IntField = 80, ByteField = 90,
				FloatField = 15f, DoubleField = 25d, UIntField = 100, UShortField = 110,
			});

			var otherFrame = new byte[otherWritten.Length + 4];
			otherWritten.CopyTo(otherFrame, 4);

			converter.ConvertFromByteArray(new ReadOnlySpan<byte>(otherFrame), instance);

			instance.IntField.ShouldBe(80);
			instance.UShortField.ShouldBe((ushort) 110);
		}


		/// <summary>
		/// A short span must be rejected the same way a short array is, as a protocol condition and
		/// not as an IndexOutOfRangeException from somewhere inside the converter.
		/// </summary>
		[Fact]
		public void TestTooShortSpanThrowsProtocolConverterException()
		{
			var converter = CreateConverter<ComplexProtocol>();

			var frame = converter.ConvertToByteArray(new ComplexProtocol
			{
				LengthOfStr1 = 3, String1 = "abc",
				LengthOfStr2 = 3, String2 = "def",
				IntField = 1, ShortField = 1, ByteField = 1, FloatField = 1f, DoubleField = 1d,
				UIntField = 1, UShortField = 1,
			});

			Should.Throw<ProtocolConverterException>(
				() => converter.ConvertFromByteArray(new ReadOnlySpan<byte>(frame, 0, frame.Length - 1)));
		}


		/// <summary>
		/// An empty span is the degenerate case of a short one, and it must not come out as a null
		/// reference or an index exception.
		/// </summary>
		[Fact]
		public void TestEmptySpanThrowsProtocolConverterException()
		{
			var converter = CreateConverter<DumbPoco>();

			Should.Throw<ProtocolConverterException>(
				() => converter.ConvertFromByteArray(ReadOnlySpan<byte>.Empty));

			Should.Throw<ProtocolConverterException>(
				() => converter.ConvertFromByteArray(default(ReadOnlySpan<byte>), new DumbPoco()));
		}


		/// <summary>
		/// The array overloads keep their null check even though the length check moved to the span
		/// implementation, where a null simply cannot occur.
		/// </summary>
		[Fact]
		public void TestNullArrayIsStillRejected()
		{
			var converter = CreateConverter<DumbPoco>();

			Should.Throw<Exception>(() => converter.ConvertFromByteArray((byte[]) null));
			Should.Throw<Exception>(() => converter.ConvertFromByteArray((byte[]) null, new DumbPoco()));
		}


		/// <summary>
		/// Calling the span overload before Prepare must fail like the array overload does.
		/// </summary>
		[Fact]
		public void TestSpanReadBeforePrepareIsRejected()
		{
			var converter = new ProtocolConverter<DumbPoco>(_logger) as IProtocolConverter<DumbPoco>;

			var frame = new byte[64];

			Should.Throw<Exception>(() => converter.ConvertFromByteArray(new ReadOnlySpan<byte>(frame)));
		}


		/// <summary>
		/// A null instance must be rejected on the span overload as well.
		/// </summary>
		[Fact]
		public void TestSpanReadIntoNullInstanceIsRejected()
		{
			var converter = CreateConverter<DumbPoco>();

			var frame = new byte[64];

			Should.Throw<Exception>(() => converter.ConvertFromByteArray(new ReadOnlySpan<byte>(frame), null));
		}
	}
}
