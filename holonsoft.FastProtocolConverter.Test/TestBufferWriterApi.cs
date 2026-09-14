using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using System.Buffers;
using System.Collections.Generic;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	/// <summary>
	/// The <see cref="IBufferWriter{T}"/> write overload, which is how a pipeline hands out its
	/// buffer. It writes into the writer's own memory, so nothing is copied.
	///
	/// The trap this has to avoid is that <c>GetSpan(n)</c> is free to return **more** than n bytes.
	/// A converter that wrote into the whole span, or advanced by its length, would corrupt the
	/// stream with whatever the writer had spare.
	/// </summary>
	public class TestBufferWriterApi
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
		/// A writer that always hands out far more room than was asked for, which is legal and is what
		/// makes advancing by the span length instead of the frame length a real bug.
		/// </summary>
		private sealed class OverReturningBufferWriter : IBufferWriter<byte>
		{
			private byte[] _buffer = new byte[1024];

			public int Written { get; private set; }

			public void Advance(int count) => Written += count;

			public Memory<byte> GetMemory(int sizeHint = 0) => _buffer.AsMemory(Written);

			public Span<byte> GetSpan(int sizeHint = 0)
			{
				// deliberately hand out everything that is left, never just the hint
				if (_buffer.Length - Written < sizeHint + 512) Array.Resize(ref _buffer, _buffer.Length * 4);

				// fill the spare room with a marker, so anything the converter writes past the frame
				// stays visible
				_buffer.AsSpan(Written).Fill(0xCC);

				return _buffer.AsSpan(Written);
			}

			public byte[] ToArray() => _buffer.AsSpan(0, Written).ToArray();
		}


		private static DumbPoco SampleDumbPoco() => new()
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


		[Fact]
		public void TestBufferWriterProducesTheSameBytesAsTheArrayOverload()
		{
			var converter = CreateConverter<DumbPoco>();

			var source = SampleDumbPoco();

			var expected = converter.ConvertToByteArray(source);

			var writer = new ArrayBufferWriter<byte>();
			converter.ConvertToByteArray(source, writer);

			writer.WrittenCount.ShouldBe(expected.Length);
			writer.WrittenSpan.ToArray().ShouldBe(expected);
		}


		/// <summary>
		/// The whole point of a buffer writer: frame after frame into one stream, each one advancing
		/// by exactly its own length.
		/// </summary>
		[Fact]
		public void TestSeveralFramesAppendOneAfterTheOther()
		{
			var converter = CreateConverter<DumbPoco>();

			var source = SampleDumbPoco();
			var single = converter.ConvertToByteArray(source);

			var writer = new ArrayBufferWriter<byte>();

			for (var i = 0; i < 5; i++)
			{
				converter.ConvertToByteArray(source, writer);
			}

			writer.WrittenCount.ShouldBe(single.Length * 5, "no frame may advance by more than its own length");

			var all = writer.WrittenSpan.ToArray();

			for (var i = 0; i < 5; i++)
			{
				all.AsSpan(i * single.Length, single.Length).ToArray()
					.ShouldBe(single, $"frame {i} has to be identical to the others");
			}
		}


		/// <summary>
		/// A writer that hands out far more than the frame needs must still end up with exactly the
		/// frame, and nothing of the spare room may leak into it.
		/// </summary>
		[Fact]
		public void TestOverReturningWriterGetsExactlyTheFrame()
		{
			var converter = CreateConverter<DumbPoco>();

			var source = SampleDumbPoco();
			var expected = converter.ConvertToByteArray(source);

			var writer = new OverReturningBufferWriter();

			converter.ConvertToByteArray(source, writer);

			writer.Written.ShouldBe(expected.Length, "advance by the frame length, not by the span length");
			writer.ToArray().ShouldBe(expected);
		}


		/// <summary>
		/// Same writer, several frames, which is where an advance by the span length would show up as
		/// a stream full of filler.
		/// </summary>
		[Fact]
		public void TestOverReturningWriterKeepsFramesContiguous()
		{
			var converter = CreateConverter<DumbPoco>();

			var source = SampleDumbPoco();
			var single = converter.ConvertToByteArray(source);

			var writer = new OverReturningBufferWriter();

			for (var i = 0; i < 3; i++)
			{
				converter.ConvertToByteArray(source, writer);
			}

			writer.Written.ShouldBe(single.Length * 3);

			var all = writer.ToArray();

			for (var i = 0; i < 3; i++)
			{
				all.AsSpan(i * single.Length, single.Length).ToArray().ShouldBe(single, $"frame {i}");
			}
		}


		/// <summary>
		/// A protocol whose length depends on the values, so the size is computed per frame.
		/// </summary>
		[Fact]
		public void TestVariableLengthFramesAdvanceByTheirOwnLength()
		{
			var converter = CreateConverter<SequencePropertyPoco>();

			var texts = new[] { "a", "abcdefgh", string.Empty, "hello world" };

			var writer = new ArrayBufferWriter<byte>();
			var expected = new List<byte>();

			foreach (var text in texts)
			{
				var source = new SequencePropertyPoco { Text = text, Trailer = text.Length };

				expected.AddRange(converter.ConvertToByteArray(source));

				converter.ConvertToByteArray(new SequencePropertyPoco { Text = text, Trailer = text.Length }, writer);
			}

			writer.WrittenCount.ShouldBe(expected.Count);
			writer.WrittenSpan.ToArray().ShouldBe(expected.ToArray());
		}


		/// <summary>
		/// And the stream reads back frame by frame, which is what a receiver does.
		/// </summary>
		[Fact]
		public void TestFramesWrittenToABufferWriterReadBack()
		{
			var converter = CreateConverter<BoolPropertyPoco>();

			var writer = new ArrayBufferWriter<byte>();

			for (var i = 0; i < 4; i++)
			{
				converter.ConvertToByteArray(new BoolPropertyPoco
				{
					First = i % 2 == 0, Second = i % 3 == 0, Trailer = i,
				}, writer);
			}

			var stream = writer.WrittenSpan.ToArray();
			var frameLength = converter.GetByteCount(new BoolPropertyPoco());

			stream.Length.ShouldBe(frameLength * 4);

			for (var i = 0; i < 4; i++)
			{
				var frame = converter.ConvertFromByteArray(stream.AsSpan(i * frameLength, frameLength));

				frame.First.ShouldBe(i % 2 == 0, $"frame {i}");
				frame.Second.ShouldBe(i % 3 == 0, $"frame {i}");
				frame.Trailer.ShouldBe(i, $"frame {i}");
			}
		}


		[Fact]
		public void TestNullArgumentsAreRejected()
		{
			var converter = CreateConverter<DumbPoco>();

			Should.Throw<Exception>(() => converter.ConvertToByteArray(null, new ArrayBufferWriter<byte>()));
			Should.Throw<Exception>(() => converter.ConvertToByteArray(SampleDumbPoco(), (IBufferWriter<byte>) null));
		}


		[Fact]
		public void TestWriteBeforePrepareIsRejected()
		{
			var converter = new ProtocolConverter<DumbPoco>(_logger) as IProtocolConverter<DumbPoco>;

			Should.Throw<Exception>(() => converter.ConvertToByteArray(new DumbPoco(), new ArrayBufferWriter<byte>()));
		}
	}
}
