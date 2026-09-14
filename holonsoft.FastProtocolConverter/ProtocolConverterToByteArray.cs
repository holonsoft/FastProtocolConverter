using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using holonsoft.FluentConditions;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.dto;
using System.Runtime.CompilerServices;


namespace holonsoft.FastProtocolConverter
{
	public partial class ProtocolConverter<T>
		where T : class, new()
	{
		private byte[] ConvertToByteArray(T data)
		{
			ThrowIfNotPrepared();
			ThrowIfNull(data, nameof(data));

			// The size is known before a single byte is written, so the result array is allocated once
			// and written into directly. It used to be a List<byte>, which cost the list object, its
			// backing array and a ToArray copy on top. A pooled buffer was tried instead and was
			// slower: renting and returning costs more than the one allocation it saves on a frame
			// this small.
			var result = new byte[GetByteCountCore(data)];

			var writer = new ByteWriter(result);

			WriteAllFields(ref writer, data);

			// GetByteCount and the writer disagreeing would be a bug in this library, not anything the
			// caller did, and it is the one thing that could hand back a half written or zero padded
			// frame that still looks well formed. So it is checked on every call and reported loudly
			// rather than papered over.
			if (writer.Overflowed || (writer.Position != result.Length))
			{
				throw new ProtocolConverterException(
					$"Internal size mismatch while writing {typeof(T).Name}: {result.Length} bytes were calculated"
					+ $" but the writer produced {(writer.Overflowed ? "more" : writer.Position.ToString())}."
					+ " This is a bug in FastProtocolConverter, please report it with the protocol definition.");
			}

			return result;
		}


		/// <summary>
		/// Writes the POCO into a buffer the caller owns, so a hot loop can serialise into a rented
		/// or stack allocated frame without this library allocating anything at all.
		///
		/// Returns false and writes nothing meaningful when the destination is too small, which is the
		/// usual Try pattern: either size the buffer with <see cref="GetByteCount"/> beforehand, or
		/// probe with a buffer and grow it on a false.
		/// </summary>
		private bool TryConvertToByteArray(T data, Span<byte> destination, out int bytesWritten)
		{
			ThrowIfNotPrepared();
			ThrowIfNull(data, nameof(data));

			var writer = new ByteWriter(destination);

			WriteAllFields(ref writer, data);

			if (writer.Overflowed)
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = writer.Position;
			return true;
		}


		/// <summary>
		/// Writes the POCO into an <see cref="IBufferWriter{T}"/>, which is how a pipeline hands out
		/// its buffer. The frame is written into the writer's own memory, so nothing is copied and
		/// nothing is allocated here.
		/// </summary>
		private void ConvertToByteArray(T data, IBufferWriter<byte> bufferWriter)
		{
			ThrowIfNotPrepared();
			ThrowIfNull(data, nameof(data));
			ThrowIfNull(bufferWriter, nameof(bufferWriter));

			var size = GetByteCountCore(data);

			// GetSpan is free to hand out more than was asked for, and Advance must be told the real
			// length, so the cursor gets exactly the frame and not whatever the writer had spare
			var destination = bufferWriter.GetSpan(size).Slice(0, size);

			var writer = new ByteWriter(destination);

			WriteAllFields(ref writer, data);

			if (writer.Overflowed || (writer.Position != size))
			{
				throw new ProtocolConverterException(
					$"Internal size mismatch while writing {typeof(T).Name}: {size} bytes were calculated"
					+ $" but the writer produced {(writer.Overflowed ? "more" : writer.Position.ToString())}."
					+ " This is a bug in FastProtocolConverter, please report it with the protocol definition.");
			}

			bufferWriter.Advance(size);
		}


		/// <summary>
		/// Exact number of bytes <paramref name="data"/> will produce, so a caller can size a buffer
		/// for <see cref="TryConvertToByteArray"/>.
		///
		/// For a protocol whose length does not depend on the values, which is every protocol without
		/// a variable length string, this is a field read and costs nothing. A variable length string
		/// has to be measured, which is a scan of the value but not an encode of it.
		/// </summary>
		private int GetByteCount(T data)
		{
			ThrowIfNotPrepared();
			ThrowIfNull(data, nameof(data));

			return GetByteCountCore(data);
		}


		private int GetByteCountCore(T data)
		{
			if (!_hasVariableLengthStrings) return _writeFixedSize;

			var total = _writeFixedSize;

			foreach (var kvp in _seqPosFields)
			{
				var field = kvp.Value;

				if (!field.IsString || field.StrAttribute.IsFixedLengthString) continue;

				var value = (string) field.Getter(data) ?? string.Empty;

				total += field.StringEncoding.GetByteCount(value);
			}

			return total;
		}


		private void WriteAllFields(ref ByteWriter writer, T data)
		{
			if (_seqPosFields.Length == 0)
			{
				foreach (var kvp in _fixPosFields)
				{
					WriteFieldValueToArray(ref writer, kvp, data);
				}

				return;
			}

			// the length of a variable string travels in a field of its own, which comes earlier in
			// the sequence than the string, so every one of them has to be known before the first
			// byte is written
			SetLengthFieldsForStrings(data);

			foreach (var kvp in _seqPosFields)
			{
				WriteFieldValueToArray(ref writer, kvp, data);
			}
		}


		private void SetLengthFieldsForStrings(T data)
		{
			foreach (var kvp in _seqPosFields)
			{
				var field = kvp.Value;

				if (!field.IsString || field.StrAttribute.IsFixedLengthString) continue;

				var value = (string) field.Getter(data) ?? string.Empty;

				// measured, not encoded. This pre pass used to encode every string into a scratch
				// buffer only to throw it away and encode it again while writing
				SetLengthField(data, field, field.StringEncoding.GetByteCount(value));
			}
		}


		/// <summary>
		/// Writes a string field straight into the destination. There is no scratch buffer: the
		/// encoder writes into the span the frame itself occupies.
		///
		/// A fixed length field always occupies exactly its declared length. A shorter value is padded
		/// with the fill character, cutting the last one if it does not divide the remaining room, and
		/// a longer one is cut at the byte level, which is what this wire format has always done and
		/// which can therefore split a surrogate pair.
		/// </summary>
		private void WriteStringField(ref ByteWriter writer, T data, ConverterFieldInfo<T> field)
		{
			var strAttr = field.StrAttribute;
			var encoding = field.StringEncoding;

			var value = (string) field.Getter(data) ?? string.Empty;
			var byteCount = encoding.GetByteCount(value);

			if (!strAttr.IsFixedLengthString)
			{
				// a fixed position protocol never runs the pre pass, so the length field is assigned
				// here as well. Assigning it twice for a sequence protocol is harmless, it is the
				// same number
				SetLengthField(data, field, byteCount);

				var variableTarget = writer.GetSpanAndAdvance(byteCount);

				if (variableTarget.Length == byteCount) encoding.GetBytes(value, variableTarget);

				return;
			}

			var declaredLength = strAttr.StringMaxLengthInByteArray;

			var target = writer.GetSpanAndAdvance(declaredLength);

			if (target.Length != declaredLength) return;

			if (byteCount <= declaredLength)
			{
				encoding.GetBytes(value, target);

				PadWithFillCharacter(target.Slice(byteCount), field.StringFillupBytes);
				return;
			}

			// The value does not fit and has to be cut at the byte level, which the encoder cannot do
			// into a span that is too small for it, so this is the one case that still needs a buffer
			// of its own. It is rented rather than allocated, and only values that overflow their
			// field ever reach it.
			var scratch = ArrayPool<byte>.Shared.Rent(byteCount);

			try
			{
				encoding.GetBytes(value, scratch);

				scratch.AsSpan(0, declaredLength).CopyTo(target);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(scratch);
			}
		}


		/// <summary>
		/// Repeats the encoded fill character over what is left of the field. The fill character can
		/// be wider than one byte, so the last repetition is cut at the field boundary rather than
		/// allowed to overshoot it.
		/// </summary>
		private static void PadWithFillCharacter(Span<byte> remainder, byte[] fillCharacter)
		{
			if (fillCharacter.Length == 0) return;

			var position = 0;

			while (position < remainder.Length)
			{
				var take = Math.Min(fillCharacter.Length, remainder.Length - position);

				fillCharacter.AsSpan(0, take).CopyTo(remainder.Slice(position));

				position += take;
			}
		}


		/// <summary>
		/// Assigns the byte length of a variable length string back onto the field the protocol names
		/// as its length field.
		/// </summary>
		private void SetLengthField(T data, ConverterFieldInfo<T> field, int effectiveLength)
		{
			var lengthField = _fieldListByName[field.StrAttribute.LengthFieldName];

			var lengthFieldType = lengthField.MemberType;

			if (lengthFieldType == typeof(int))
			{
				lengthField.Setter(data, effectiveLength);
				return;
			}

			if (lengthFieldType == typeof(uint))
			{
				lengthField.Setter(data, (uint) effectiveLength);
				return;
			}

			if (lengthFieldType == typeof(short))
			{
				lengthField.Setter(data, (short) effectiveLength);
				return;
			}

			if (lengthFieldType == typeof(ushort))
			{
				lengthField.Setter(data, (ushort) effectiveLength);
				return;
			}

			if (lengthFieldType == typeof(byte))
			{
				lengthField.Setter(data, (byte) effectiveLength);
				return;
			}

			throw new ProtocolConverterException(
				"Setting a string length field of type " + lengthFieldType + " is not supported yet");
		}


		/// <summary>
		/// Writes a decimal as its four component integers (low, mid, high, flags) in exactly that
		/// order. UseBigEndian swaps the bytes inside every component, the order of the components
		/// themselves never changes. Counterpart of SetFieldHandleDecimalValues.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void WriteDecimalToArray(ref ByteWriter result, decimal value)
		{
			Span<int> bits = stackalloc int[4];
			decimal.GetBits(value, bits);

			foreach (var part in bits)
			{
				if (UseBigEndian)
				{
					result.Add((byte) (part >> 24));
					result.Add((byte) (part >> 16));
					result.Add((byte) (part >> 8));
					result.Add((byte) part);
				}
				else
				{
					result.Add((byte) part);
					result.Add((byte) (part >> 8));
					result.Add((byte) (part >> 16));
					result.Add((byte) (part >> 24));
				}
			}
		}


		/// <summary>
		/// Appends a value in the configured byte order without allocating. BitConverter.GetBytes
		/// allocates a fresh byte[] per call and the LINQ Reverse for big endian allocated an
		/// iterator on top of it, both once per field per message.
		/// Float and double go through their IEEE754 bit patterns, which is bit exact.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Append16(ref ByteWriter target, ushort value, bool bigEndian)
		{
			if (bigEndian)
			{
				target.Add((byte) (value >> 8));
				target.Add((byte) value);
			}
			else
			{
				target.Add((byte) value);
				target.Add((byte) (value >> 8));
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Append32(ref ByteWriter target, uint value, bool bigEndian)
		{
			if (bigEndian)
			{
				target.Add((byte) (value >> 24));
				target.Add((byte) (value >> 16));
				target.Add((byte) (value >> 8));
				target.Add((byte) value);
			}
			else
			{
				target.Add((byte) value);
				target.Add((byte) (value >> 8));
				target.Add((byte) (value >> 16));
				target.Add((byte) (value >> 24));
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Append64(ref ByteWriter target, ulong value, bool bigEndian)
		{
			if (bigEndian)
			{
				for (var shift = 56; shift >= 0; shift -= 8) target.Add((byte) (value >> shift));
			}
			else
			{
				for (var shift = 0; shift <= 56; shift += 8) target.Add((byte) (value >> shift));
			}
		}


		private void WriteFieldValueToArray(ref ByteWriter result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, T data)
		{
			// Every branch reads the field through the strongly typed accessor, so no value type is
			// boxed on the way out. It used to be one Getter call returning object up front.
			var field = kvp.Value;

			if (field.IsEnum)
			{
				var enumValue = field.Get<int>(data);

				switch (field.Attribute.TypeInByteArray)
				{
					case DestinationType.None:
					case DestinationType.Default:
					case DestinationType.Int32:
						Append32(ref result, unchecked((uint) enumValue), UseBigEndian);
						break;
					case DestinationType.Int16:
						Append16(ref result, unchecked((ushort) (short) enumValue), UseBigEndian);
						break;
					case DestinationType.Byte:
						result.Add((byte) enumValue);
						break;
				}

				return;
			}

			if (field.IsBitValue)
			{
				byte consolidatedBits = 0;
				OnConsolidateBitValues?.Invoke(data, out consolidatedBits);

				result.Add(consolidatedBits);
				return;
			}

			if (field.IsGuid)
			{
				// written straight into the destination, ToByteArray() allocated a byte[16] for every
				// Guid of every message
				Span<byte> guidBytes = stackalloc byte[16];
				field.Get<Guid>(data).TryWriteBytes(guidBytes);

				// the reader reverses all 16 bytes when big endian is set, so the writer has to do
				// the same, otherwise a Guid cannot be read back by this very converter.
				// Note that this order is a full reversal of Guid.ToByteArray(), it is NOT RFC 4122.
				if (UseBigEndian) guidBytes.Reverse();

				result.AddRange(guidBytes);
				return;
			}

			switch (field.FieldTypeCode)
			{
				case TypeCode.Int32:
				{
					Append32(ref result, unchecked((uint) field.Get<int>(data)), UseBigEndian);
					return;
				}
				case TypeCode.UInt32:
				{
					Append32(ref result, field.Get<uint>(data), UseBigEndian);
					return;
				}
				case TypeCode.Int16:
				{
					Append16(ref result, unchecked((ushort) field.Get<short>(data)), UseBigEndian);
					return;
				}
				case TypeCode.UInt16:
				{
					Append16(ref result, field.Get<ushort>(data), UseBigEndian);
					return;
				}
				case TypeCode.Int64:
				{
					Append64(ref result, unchecked((ulong) field.Get<long>(data)), UseBigEndian);
					return;
				}
				case TypeCode.UInt64:
				{
					Append64(ref result, field.Get<ulong>(data), UseBigEndian);
					return;
				}
				case TypeCode.Single:
				{
					Append32(ref result, unchecked((uint) BitConverter.SingleToInt32Bits(field.Get<float>(data))), UseBigEndian);
					return;
				}
				case TypeCode.Double:
				{
					Append64(ref result, unchecked((ulong) BitConverter.DoubleToInt64Bits(field.Get<double>(data))), UseBigEndian);
					return;
				}
				case TypeCode.SByte:
					// a single byte has no byte order, so UseBigEndian is irrelevant here
					result.Add(unchecked((byte) field.Get<sbyte>(data)));
					return;
				case TypeCode.Decimal:
					WriteDecimalToArray(ref result, field.Get<decimal>(data));
					return;
				case TypeCode.Byte:
				{
					var xByte = field.Get<byte>(data);

					var repeat = field.IsPaddingByte ? field.BytePaddingAttribute.Padding : 1;

					for (var i = 0; i < repeat; i++)
					{
						result.Add(xByte);
					}

					return;
				}
				case TypeCode.Boolean:
					result.Add(field.Get<bool>(data) ? (byte) 1 : (byte) 0);
					return;
				case TypeCode.DateTime:
				{
					var dtf = field.Get<DateTime>(data);
					dtf = field.DateTimeAttribute.DateTimeKind == DateTimeKind.Utc ? dtf.ToUniversalTime() : dtf.ToLocalTime();
					// this is what holonsoft.FluentDateTime's ToUnixTimeSeconds does, inlined here. That
					// package is a non optimized build, so the JIT could neither optimize nor inline the
					// call, and the Kind is already normalised to Utc or Local on the line above, which is
					// what makes the DateTimeOffset conversion well defined
					var uts = new DateTimeOffset(dtf).ToUnixTimeSeconds();

					if (field.DateTimeAttribute.DateTimeByteFormat == DateTimeByteFormat.UnixTimeStamp32Bit)
					{
						Append32(ref result, unchecked((uint) (int) uts), UseBigEndian);
						return;
					}

					Append64(ref result, unchecked((ulong) uts), UseBigEndian);
					return;
				}
			}

			if (field.IsString)
			{
				WriteStringField(ref result, data, field);
				return;
			}

			throw new ProtocolConverterException("WriteFieldValueToArray has no converter for " + field.FieldName);
		}
	}
}

