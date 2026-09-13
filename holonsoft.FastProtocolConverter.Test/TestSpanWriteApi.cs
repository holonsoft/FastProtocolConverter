using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	/// <summary>
	/// The caller supplied buffer write path, <c>TryConvertToByteArray</c> and <c>GetByteCount</c>.
	///
	/// Two things have to hold. The bytes must be identical to what the array overload produces, for
	/// every protocol shape, because both go through the same writer and a divergence would mean the
	/// wire format depends on which overload a caller picked. And <c>GetByteCount</c> has to be exact
	/// rather than an upper bound, because callers will use it to size a buffer and then trust
	/// <c>bytesWritten</c>.
	/// </summary>
	public class TestSpanWriteApi
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
		/// Writes the POCO both ways and demands the very same bytes, plus a GetByteCount that matches
		/// the real length.
		/// </summary>
		private void ShouldWriteIdenticalBytes<TPoco>(TPoco source)
			where TPoco : class, new()
		{
			var converter = CreateConverter<TPoco>();

			var fromArray = converter.ConvertToByteArray(source);

			converter.GetByteCount(source).ShouldBe(fromArray.Length, "GetByteCount has to be exact");

			var destination = new byte[fromArray.Length];

			converter.TryConvertToByteArray(source, destination, out var written).ShouldBeTrue();

			written.ShouldBe(fromArray.Length);
			destination.ShouldBe(fromArray);
		}


		[Fact]
		public void TestFixedPositionProtocolWritesIdenticalBytes()
			=> ShouldWriteIdenticalBytes(new DumbPoco
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
			});


		[Fact]
		public void TestBigEndianProtocolWritesIdenticalBytes()
			=> ShouldWriteIdenticalBytes(new PocoWithBigEndianessFlag
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


		[Fact]
		public void TestAdvancedTypesWriteIdenticalBytes()
			=> ShouldWriteIdenticalBytes(new AdvancedTypesPoco
			{
				DateTimeField = new DateTime(2026, 9, 13, 12, 30, 0, DateTimeKind.Utc),
				GuidField = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"),
				SomeValue = 200,
			});


		[Fact]
		public void TestDecimalAndSByteWriteIdenticalBytes()
			=> ShouldWriteIdenticalBytes(new DecimalAndSBytePoco
			{
				SByteField = -42,
				DecimalField = -12345.6789m,
				TrailingField = 4711,
			});


		/// <summary>
		/// A sequence protocol with two variable length strings is the case where the size is not
		/// known from the protocol definition alone, so GetByteCount has to look at the values.
		/// </summary>
		[Fact]
		public void TestSequenceProtocolWithVariableStringsWritesIdenticalBytes()
			=> ShouldWriteIdenticalBytes(new ComplexProtocol
			{
				LengthOfStr1 = 5, String1 = "Hello",
				LengthOfStr2 = 5, String2 = "World",
				IntField = 4711, ShortField = 815, ByteField = 7,
				FloatField = 3.5f, DoubleField = 2.5d,
				UIntField = 70_000, UShortField = 40_000,
			});


		/// <summary>
		/// The same protocol with strings of a different length has to give a different byte count,
		/// otherwise GetByteCount is not really looking at the values.
		/// </summary>
		[Fact]
		public void TestGetByteCountFollowsTheStringLength()
		{
			var converter = CreateConverter<ComplexProtocol>();

			ComplexProtocol Make(string one, string two) => new()
			{
				LengthOfStr1 = one.Length, String1 = one,
				LengthOfStr2 = two.Length, String2 = two,
				IntField = 1, ShortField = 1, ByteField = 1, FloatField = 1f, DoubleField = 1d,
				UIntField = 1, UShortField = 1,
			};

			var shortOne = Make("ab", "cd");
			var longOne = Make("abcdefghij", "klmnopqrst");

			converter.GetByteCount(shortOne).ShouldBe(converter.ConvertToByteArray(shortOne).Length);
			converter.GetByteCount(longOne).ShouldBe(converter.ConvertToByteArray(longOne).Length);

			converter.GetByteCount(longOne).ShouldBeGreaterThan(converter.GetByteCount(shortOne));
		}


		/// <summary>
		/// An empty and a null string must not break the sizing.
		/// </summary>
		[Fact]
		public void TestGetByteCountHandlesEmptyAndNullStrings()
		{
			var converter = CreateConverter<ComplexProtocol>();

			var empty = new ComplexProtocol
			{
				LengthOfStr1 = 0, String1 = string.Empty,
				LengthOfStr2 = 0, String2 = null,
				IntField = 1, ShortField = 1, ByteField = 1, FloatField = 1f, DoubleField = 1d,
				UIntField = 1, UShortField = 1,
			};

			converter.GetByteCount(empty).ShouldBe(converter.ConvertToByteArray(empty).Length);
		}


		/// <summary>
		/// A destination that is one byte short must be refused, not filled to the brim and reported
		/// as a success. Nothing may be written past the end of it either.
		/// </summary>
		[Fact]
		public void TestTooSmallDestinationIsRefused()
		{
			var converter = CreateConverter<DumbPoco>();

			var source = new DumbPoco { IntField = 4711, ShortField = 815, ByteField = 3 };
			var exactSize = converter.GetByteCount(source);

			for (var size = 0; size < exactSize; size++)
			{
				var guarded = new byte[exactSize];

				converter.TryConvertToByteArray(source, guarded.AsSpan(0, size), out var written)
					.ShouldBeFalse($"a destination of {size} bytes cannot hold {exactSize}");

				written.ShouldBe(0, $"nothing is reported as written for a destination of {size} bytes");
			}
		}


		/// <summary>
		/// Exactly the right size has to succeed, that is the whole point of GetByteCount.
		/// </summary>
		[Fact]
		public void TestExactlySizedDestinationSucceeds()
		{
			var converter = CreateConverter<DumbPoco>();

			var source = new DumbPoco { IntField = 4711, ShortField = 815, ByteField = 3 };

			Span<byte> destination = stackalloc byte[converter.GetByteCount(source)];

			converter.TryConvertToByteArray(source, destination, out var written).ShouldBeTrue();

			written.ShouldBe(destination.Length);
		}


		/// <summary>
		/// A bigger destination is fine and must not be touched beyond bytesWritten, so a caller can
		/// keep one large buffer for frames of different sizes.
		/// </summary>
		[Fact]
		public void TestOversizedDestinationLeavesTheTailAlone()
		{
			var converter = CreateConverter<DumbPoco>();

			var source = new DumbPoco { IntField = 4711, ShortField = 815, ByteField = 3 };
			var exactSize = converter.GetByteCount(source);

			var destination = new byte[exactSize + 16];

			for (var i = 0; i < destination.Length; i++)
			{
				destination[i] = 0xCC;
			}

			converter.TryConvertToByteArray(source, destination, out var written).ShouldBeTrue();

			written.ShouldBe(exactSize);

			for (var i = exactSize; i < destination.Length; i++)
			{
				destination[i].ShouldBe((byte) 0xCC, $"byte {i} is past the frame and must be untouched");
			}
		}


		/// <summary>
		/// The complete loop a caller writes: serialise into a buffer they own, read it back through
		/// the span overload, no allocation on either side.
		/// </summary>
		[Fact]
		public void TestRoundTripThroughCallerOwnedBuffers()
		{
			var converter = CreateConverter<PocoWithBigEndianessFlag>();

			var source = new PocoWithBigEndianessFlag
			{
				IntField = -42, UIntField = 42, ShortField = -7, UShortField = 7,
				FloatField = 1.25f, DoubleField = -2.5d, LongField = -8, ULongField = 8,
			};

			Span<byte> frame = stackalloc byte[converter.GetByteCount(source)];

			converter.TryConvertToByteArray(source, frame, out var written).ShouldBeTrue();

			var result = new PocoWithBigEndianessFlag();
			converter.ConvertFromByteArray(frame.Slice(0, written), result);

			result.IntField.ShouldBe(-42);
			result.UIntField.ShouldBe(42u);
			result.ShortField.ShouldBe((short) -7);
			result.UShortField.ShouldBe((ushort) 7);
			result.FloatField.ShouldBe(1.25f);
			result.DoubleField.ShouldBe(-2.5d);
			result.LongField.ShouldBe(-8L);
			result.ULongField.ShouldBe(8ul);
		}


		/// <summary>
		/// Writing the same POCO into the same buffer twice has to give the same bytes both times.
		/// A cursor that is not reset per call would show up here and nowhere else.
		/// </summary>
		[Fact]
		public void TestRepeatedWritesIntoTheSameBufferAreStable()
		{
			var converter = CreateConverter<DumbPoco>();

			var source = new DumbPoco { IntField = 4711, ShortField = 815, ByteField = 3 };
			var expected = converter.ConvertToByteArray(source);

			var destination = new byte[expected.Length];

			for (var round = 0; round < 5; round++)
			{
				converter.TryConvertToByteArray(source, destination, out var written).ShouldBeTrue();

				written.ShouldBe(expected.Length, $"round {round}");
				destination.ShouldBe(expected, $"round {round}");
			}
		}


		[Fact]
		public void TestNullDataIsRejected()
		{
			var converter = CreateConverter<DumbPoco>();

			var buffer = new byte[64];

			Should.Throw<Exception>(() => converter.TryConvertToByteArray(null, buffer, out _));
			Should.Throw<Exception>(() => converter.GetByteCount(null));
		}


		[Fact]
		public void TestWriteBeforePrepareIsRejected()
		{
			var converter = new ProtocolConverter<DumbPoco>(_logger) as IProtocolConverter<DumbPoco>;

			var buffer = new byte[64];

			Should.Throw<Exception>(() => converter.TryConvertToByteArray(new DumbPoco(), buffer, out _));
			Should.Throw<Exception>(() => converter.GetByteCount(new DumbPoco()));
		}
	}
}
