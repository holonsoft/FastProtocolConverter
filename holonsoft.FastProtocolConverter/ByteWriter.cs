using System;
using System.Runtime.CompilerServices;

namespace holonsoft.FastProtocolConverter
{
	/// <summary>
	/// Append only cursor over a byte buffer, used by both write entry points so there is exactly one
	/// copy of the field writing logic and no way for the two to drift apart.
	///
	/// The buffer never grows. ConvertToByteArray knows the exact size before it writes a byte and
	/// allocates the result at that size, and TryConvertToByteArray writes into a buffer the caller
	/// owns, which by definition cannot be resized from in here.
	///
	/// Running out of room sets Overflowed instead of throwing. A throw in the middle of writing would
	/// leave the caller's buffer half filled with no way to tell how far it got, and it would put an
	/// exception frame into the hot path of a loop that is allowed to probe with a small buffer first.
	/// Once the writer has overflowed the buffer is dropped, so every later write is a no operation as
	/// well and nothing can run past the end of the span.
	/// </summary>
	internal ref struct ByteWriter
	{
		private Span<byte> _buffer;
		private int _position;
		private bool _overflow;


		public ByteWriter(Span<byte> destination)
		{
			_buffer = destination;
			_position = 0;
			_overflow = false;
		}


		/// <summary>
		/// Number of bytes written so far. Meaningless once Overflowed is set.
		/// </summary>
		public readonly int Position => _position;


		/// <summary>
		/// True when the destination ran out of room. Nothing meaningful has been written in that
		/// case: every write from the first one that did not fit onwards was dropped.
		/// </summary>
		public readonly bool Overflowed => _overflow;


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(byte value)
		{
			if ((uint) _position >= (uint) _buffer.Length)
			{
				Overflow();
				return;
			}

			_buffer[_position++] = value;
		}


		// scoped, otherwise the compiler has to assume the writer keeps the span and refuses a
		// stackalloc argument, which is exactly how the Guid path writes
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddRange(scoped ReadOnlySpan<byte> values)
		{
			if (_position + values.Length > _buffer.Length)
			{
				Overflow();
				return;
			}

			values.CopyTo(_buffer.Slice(_position));
			_position += values.Length;
		}


		/// <summary>
		/// Reserves <paramref name="length"/> bytes and hands them out to be filled directly, so an
		/// encoder can write into the destination instead of into a temporary of its own.
		///
		/// The returned span is **empty** when the destination has no room left, which is not the same
		/// length as what was asked for, so a caller has to compare before it writes.
		/// </summary>
		public Span<byte> GetSpanAndAdvance(int length)
		{
			if (_position + length > _buffer.Length)
			{
				Overflow();
				return Span<byte>.Empty;
			}

			var slice = _buffer.Slice(_position, length);
			_position += length;

			return slice;
		}


		/// <summary>
		/// Dropping the buffer is what makes every later write a no operation too, without costing the
		/// hot path a second branch: the bounds check above already fails for an empty span.
		/// </summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void Overflow()
		{
			_overflow = true;
			_buffer = Span<byte>.Empty;
		}
	}
}
