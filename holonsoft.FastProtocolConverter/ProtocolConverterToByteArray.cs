using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using holonsoft.FluentConditions;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.dto;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


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
			// Scratch buffer for string encoding. It is local to this call on purpose: it used to be
			// a List<byte> on the shared ConverterFieldInfo, so two threads serialising different
			// values through one prepared converter produced mixed up or truncated output.
			// A protocol without strings allocates nothing here.
			var stringBuffer = _hasStringFields ? new List<byte>(_stringBufferCapacity) : null;

			if (_fieldListSeqPos.Count == 0)
			{
				foreach (var kvp in _fixPosFields)
				{
					WriteFieldValueToArray(ref writer, kvp, data, stringBuffer);
				}

				return;
			}

			CalculateStringForWriting(data, stringBuffer);

			foreach (var kvp in _seqPosFields)
			{
				WriteFieldValueToArray(ref writer, kvp, data, stringBuffer);
			}
		}


		private void CalculateStringForWriting(T data, List<byte> stringBuffer)
		{
			// calc length fields for strings
			foreach (var kvp in _seqPosFields)
			{
				if (!kvp.Value.IsString) continue;

				CalculateBufferForString(data, kvp, stringBuffer);
			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void CalculateBufferForString(T data, KeyValuePair<int, ConverterFieldInfo<T>> kvp, List<byte> stringBuffer)
		{
			var field = kvp.Value;
			var strAttr = field.StrAttribute;

			var value = (string) field.Getter(data) ?? string.Empty;
			var encoding = field.StringEncoding;

			// encoded straight into the backing array of the list. Encoding.GetBytes(string)
			// allocated a byte[] for every string of every message.
			var byteCount = encoding.GetByteCount(value);

			CollectionsMarshal.SetCount(stringBuffer, byteCount);
			encoding.GetBytes(value, CollectionsMarshal.AsSpan(stringBuffer));

			var fillupChar = field.StringFillupBytes;

			int effectiveLength = byteCount;

			if (strAttr.IsFixedLengthString)
			{
				var declaredLength = strAttr.StringMaxLengthInByteArray;

				while (effectiveLength < declaredLength)
				{
					stringBuffer.AddRange(fillupChar);
					effectiveLength += fillupChar.Length;
				}

				// One truncation for both branches, and it is not only for values that are too long.
				// The fill character can be wider than one byte, for example any character under the
				// unicode encoder, and then the padding loop above overshoots whenever the declared
				// length is not a multiple of that width. That used to grow the frame by a byte and
				// shift every field behind the string, silently, because the buffer it was written
				// into could grow. A fixed length field is exactly its declared length, always.
				if (stringBuffer.Count > declaredLength)
				{
					stringBuffer.RemoveRange(declaredLength, stringBuffer.Count - declaredLength);
				}

				return;
			}

			var correspondingLengthField = _fieldListByName[strAttr.LengthFieldName];

			if (correspondingLengthField.FieldInfo.FieldType == typeof(int))
			{
				correspondingLengthField.Setter(data, effectiveLength);
				return;
			}

			if (correspondingLengthField.FieldInfo.FieldType == typeof(uint))
			{
				correspondingLengthField.Setter(data, (uint) effectiveLength);
				return;
			}

			if (correspondingLengthField.FieldInfo.FieldType == typeof(short))
			{
				correspondingLengthField.Setter(data, (short) effectiveLength);
				return;
			}

			if (correspondingLengthField.FieldInfo.FieldType == typeof(ushort))
			{
				correspondingLengthField.Setter(data, (ushort) effectiveLength);
				return;
			}

			if (correspondingLengthField.FieldInfo.FieldType == typeof(byte))
			{
				correspondingLengthField.Setter(data, (byte) effectiveLength);
				return;
			}

			throw new ProtocolConverterException("CalculateStringForWriting: setting length field type " +
			                                     correspondingLengthField.FieldInfo.FieldType + " not supported yet");
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


		private void WriteFieldValueToArray(ref ByteWriter result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, T data, List<byte> stringBuffer)
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
				CalculateBufferForString(data, kvp, stringBuffer);

				result.AddRange(CollectionsMarshal.AsSpan(stringBuffer));
				return;
			}

			throw new ProtocolConverterException("WriteFieldValueToArray has no converter for " + field.FieldName);
		}
	}
}

