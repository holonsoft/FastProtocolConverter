using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using System.Text;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	/// <summary>
	/// A fixed length string field occupies exactly its declared number of bytes, whatever the value
	/// is. Shorter values are padded with the fill character, longer ones are cut off. Getting that
	/// wrong by a single byte shifts every field behind it, so the frame stays the declared length is
	/// the property that matters most here.
	///
	/// These are write direction tests on purpose. The golden vectors pin one frame per protocol and
	/// cannot enumerate the interesting values of a field.
	/// </summary>
	public class TestFixedLengthStringWriting
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
		/// A value shorter than the field is padded with the fill character, and the frame keeps its
		/// declared length.
		/// </summary>
		[Fact]
		public void TestShortAsciiValueIsPaddedToTheDeclaredLength()
		{
			var converter = CreateConverter<AsciiFixedStringPoco>();

			var frame = converter.ConvertToByteArray(new AsciiFixedStringPoco
			{
				Header = 1, Text = "abc", Trailer = 2,
			});

			frame.Length.ShouldBe(16, "4 header + 8 string + 4 trailer");

			Encoding.ASCII.GetString(frame, 4, 8).ShouldBe("abcZZZZZ");
		}


		/// <summary>
		/// A value longer than the field is cut off, and the frame still keeps its declared length.
		/// Without this the following fields would all be shifted.
		/// </summary>
		[Fact]
		public void TestLongAsciiValueIsTruncatedToTheDeclaredLength()
		{
			var converter = CreateConverter<AsciiFixedStringPoco>();

			var frame = converter.ConvertToByteArray(new AsciiFixedStringPoco
			{
				Header = 1, Text = "abcdefghijklmno", Trailer = 2,
			});

			frame.Length.ShouldBe(16);

			Encoding.ASCII.GetString(frame, 4, 8).ShouldBe("abcdefgh");
		}


		/// <summary>
		/// An empty and a null value both give a field full of fill characters.
		/// </summary>
		[Theory]
		[InlineData("")]
		[InlineData(null)]
		public void TestEmptyAndNullValueFillTheWholeField(string value)
		{
			var converter = CreateConverter<AsciiFixedStringPoco>();

			var frame = converter.ConvertToByteArray(new AsciiFixedStringPoco
			{
				Header = 1, Text = value, Trailer = 2,
			});

			frame.Length.ShouldBe(16);

			Encoding.ASCII.GetString(frame, 4, 8).ShouldBe("ZZZZZZZZ");
		}


		/// <summary>
		/// A value that fits exactly needs neither padding nor truncation.
		/// </summary>
		[Fact]
		public void TestExactlyFittingValueIsUntouched()
		{
			var converter = CreateConverter<AsciiFixedStringPoco>();

			var frame = converter.ConvertToByteArray(new AsciiFixedStringPoco
			{
				Header = 1, Text = "abcdefgh", Trailer = 2,
			});

			frame.Length.ShouldBe(16);

			Encoding.ASCII.GetString(frame, 4, 8).ShouldBe("abcdefgh");
		}


		/// <summary>
		/// With the unicode encoder every character costs two bytes, so a 10 byte field holds five.
		/// </summary>
		[Fact]
		public void TestUnicodeValueIsPaddedInTwoByteUnits()
		{
			var converter = CreateConverter<UnicodeFixedStringPoco>();

			var frame = converter.ConvertToByteArray(new UnicodeFixedStringPoco
			{
				Header = 1, Text = "ab", Trailer = 2,
			});

			frame.Length.ShouldBe(18, "4 header + 10 string + 4 trailer");

			Encoding.Unicode.GetString(frame, 4, 10).ShouldBe("ab\0\0\0");
		}


		/// <summary>
		/// A unicode value longer than the field is cut off at the byte level, which is what this
		/// converter has always done. Five characters fit into ten bytes, the sixth does not.
		/// </summary>
		[Fact]
		public void TestLongUnicodeValueIsTruncated()
		{
			var converter = CreateConverter<UnicodeFixedStringPoco>();

			var frame = converter.ConvertToByteArray(new UnicodeFixedStringPoco
			{
				Header = 1, Text = "abcdefghij", Trailer = 2,
			});

			frame.Length.ShouldBe(18);

			Encoding.Unicode.GetString(frame, 4, 10).ShouldBe("abcde");
		}


		/// <summary>
		/// Truncation happens on bytes, not on characters, so a value made of surrogate pairs can be
		/// cut through the middle of one. That is not pretty, but it is the behaviour the wire format
		/// has always had, and changing it would change frames that existing systems already exchange.
		/// What must hold regardless is that the field occupies its declared ten bytes.
		/// </summary>
		[Fact]
		public void TestSurrogatePairIsCutAtTheByteBoundary()
		{
			var converter = CreateConverter<UnicodeFixedStringPoco>();

			// three emoji, two UTF-16 code units each, so 12 bytes for a 10 byte field
			var frame = converter.ConvertToByteArray(new UnicodeFixedStringPoco
			{
				Header = 1, Text = "\U0001F600\U0001F600\U0001F600", Trailer = 2,
			});

			frame.Length.ShouldBe(18, "the field keeps its declared length even mid surrogate");

			// two whole emoji, plus the orphaned high surrogate of the third. Decoding an unpaired
			// surrogate gives the replacement character back, which is how a reader sees the cut
			var decoded = Encoding.Unicode.GetString(frame, 4, 10);

			decoded.Length.ShouldBe(5, "two surrogate pairs and one orphan");
			decoded.Substring(0, 4).ShouldBe("😀😀");
			decoded[4].ShouldBe('�', "the third pair was cut through the middle");
		}


		/// <summary>
		/// The awkward case: a unicode field of eleven bytes, padded with a fill character that costs
		/// two. The padding cannot land on the boundary exactly, so the converter has to stop at
		/// eleven rather than overshoot to twelve and push every following field one byte along.
		/// </summary>
		[Fact]
		public void TestOddLengthUnicodeFieldKeepsItsDeclaredLength()
		{
			var converter = CreateConverter<UnicodeOddLengthStringPoco>();

			var source = new UnicodeOddLengthStringPoco { Header = 1, Text = "ab", Trailer = 0x0A0B0C0D };

			var frame = converter.ConvertToByteArray(source);

			frame.Length.ShouldBe(19, "4 header + 11 string + 4 trailer");

			// and the trailer really is where the protocol says it is
			var result = converter.ConvertFromByteArray(frame);

			result.Trailer.ShouldBe(0x0A0B0C0D);
			result.Header.ShouldBe(1);
		}


		/// <summary>
		/// Same field, this time with a value that is longer than it, so the truncation branch has to
		/// land on eleven as well.
		/// </summary>
		[Fact]
		public void TestOddLengthUnicodeFieldTruncatesToItsDeclaredLength()
		{
			var converter = CreateConverter<UnicodeOddLengthStringPoco>();

			var source = new UnicodeOddLengthStringPoco
			{
				Header = 1, Text = "abcdefghij", Trailer = 0x0A0B0C0D,
			};

			var frame = converter.ConvertToByteArray(source);

			frame.Length.ShouldBe(19);

			var result = converter.ConvertFromByteArray(frame);

			result.Trailer.ShouldBe(0x0A0B0C0D);
		}


		/// <summary>
		/// Whatever the value, both write overloads have to agree, because they share the writer.
		/// </summary>
		[Theory]
		[InlineData("")]
		[InlineData("ab")]
		[InlineData("abcdefgh")]
		[InlineData("abcdefghijklmnop")]
		public void TestBothWriteOverloadsAgreeForEveryValueLength(string value)
		{
			var converter = CreateConverter<AsciiFixedStringPoco>();

			var source = new AsciiFixedStringPoco { Header = 7, Text = value, Trailer = 9 };

			var fromArray = converter.ConvertToByteArray(source);

			converter.GetByteCount(source).ShouldBe(fromArray.Length);

			var destination = new byte[fromArray.Length];

			converter.TryConvertToByteArray(source, destination, out var written).ShouldBeTrue();

			written.ShouldBe(fromArray.Length);
			destination.ShouldBe(fromArray);
		}
	}
}
