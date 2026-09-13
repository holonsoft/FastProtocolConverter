using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace holonsoft.FastProtocolConverter
{
	/// <summary>
	/// Append only cursor over a byte buffer, used by both write entry points so there is exactly one
	/// copy of the field writing logic.
	///
	/// It comes in two shapes:
	///
	/// * <b>growable</b>, created by <see cref="Rented"/>. The buffer comes from the array pool and is
	///   exchanged for a bigger one when a protocol with variable length strings needs more room than
	///   the estimate. This is what <c>ConvertToByteArray</c> uses.
	/// * <b>fixed</b>, created from a caller supplied span. It cannot grow, so running out of room
	///   sets <see cref="Overflowed"/> instead of throwing, which is what the Try pattern of
	///   <c>TryConvertToByteArray</c> reports as a false return value.
	///
	/// Overflow is recorded rather than thrown on purpose. A throw in the middle of writing would
	/// leave the caller's buffer half filled with no way to tell how far it got, and it would put an
	/// exception frame into the hot path of a loop that is allowed to probe with a small buffer first.
	/// Every write after the overflow is dropped, so nothing runs past the end of the span.
	/// </summary>
	internal ref struct ByteWriter
	{
		private Span<byte> _buffer;
		private byte[] _pooled;
		private int _position;
		private bool _overflow;


		private ByteWriter(Span<byte> buffer, byte[] pooled)
		{
			_buffer = buffer;
			_pooled = pooled;
			_position = 0;
			_overflow = false;
		}


		/// <summary>
		/// Writes into the caller's buffer and never grows.
		/// </summary>
		public ByteWriter(Span<byte> destination) : this(destination, null)
		{
		}


		/// <summary>
		/// Writes into a pooled buffer of at least <paramref name="initialCapacity"/> bytes and grows
		/// when it has to. <see cref="Dispose"/> returns the buffer to the pool.
		/// </summary>
		public static ByteWriter Rented(int initialCapacity)
		{
			var pooled = ArrayPool<byte>.Shared.Rent(initialCapacity);

			return new ByteWriter(pooled, pooled);
		}


		/// <summary>
		/// Number of bytes written so far. Meaningless once <see cref="Overflowed"/> is set.
		/// </summary>
		public readonly int Position => _position;


		/// <summary>
		/// True when a fixed buffer ran out of room. A growable writer never sets it.
		/// </summary>
		public readonly bool Overflowed => _overflow;


		public readonly ReadOnlySpan<byte> WrittenSpan => _buffer.Slice(0, _position);


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(byte value)
		{
			if ((uint) _position >= (uint) _buffer.Length && !TryMakeRoom(1)) return;

			_buffer[_position++] = value;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// scoped, otherwise the compiler has to assume the writer keeps the span and refuses a
		// stackalloc argument, which is exactly how the Guid path writes
		public void AddRange(scoped ReadOnlySpan<byte> values)
		{
			if (_position + values.Length > _buffer.Length && !TryMakeRoom(values.Length)) return;

			values.CopyTo(_buffer.Slice(_position));
			_position += values.Length;
		}


		[MethodImpl(MethodImplOptions.NoInlining)]
		private bool TryMakeRoom(int extraBytes)
		{
			if (_pooled is null)
			{
				// caller supplied buffer, nothing to grow into
				_overflow = true;
				return false;
			}

			var needed = _position + extraBytes;
			var newSize = Math.Max(needed, _buffer.Length * 2);

			var replacement = ArrayPool<byte>.Shared.Rent(newSize);

			_buffer.Slice(0, _position).CopyTo(replacement);

			ArrayPool<byte>.Shared.Return(_pooled);

			_pooled = replacement;
			_buffer = replacement;

			return true;
		}


		public void Dispose()
		{
			if (_pooled is null) return;

			ArrayPool<byte>.Shared.Return(_pooled);
			_pooled = null;
			_buffer = Span<byte>.Empty;
		}
	}
}
