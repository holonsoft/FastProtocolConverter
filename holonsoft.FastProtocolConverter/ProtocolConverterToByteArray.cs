using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using holonsoft.FluentConditions;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.dto;
using holonsoft.FluentDateTime.DateTime;
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

			// sized up front, the backing array used to double its way up from nothing
			var result = new List<byte>(_writeSizeEstimate);

			// Scratch buffer for string encoding. It is local to this call on purpose: it used to be
			// a List<byte> on the shared ConverterFieldInfo, so two threads serialising different
			// values through one prepared converter produced mixed up or truncated output.
			// A protocol without strings allocates nothing here.
			var stringBuffer = _hasStringFields ? new List<byte>(_stringBufferCapacity) : null;

			if (_fieldListSeqPos.Count == 0)
			{
				foreach (var kvp in _fieldListFixPos)
				{
					WriteFieldValueToArray(result, kvp, data, stringBuffer);
				}
			}
			else
			{
				CalculateStringForWriting(data, stringBuffer);

				foreach (var kvp in _fieldListSeqPos)
				{
					WriteFieldValueToArray(result, kvp, data, stringBuffer);
				}
			}

			return result.ToArray();
		}


		private void CalculateStringForWriting(T data, List<byte> stringBuffer)
		{
			// calc length fields for strings
			foreach (var kvp in _fieldListSeqPos)
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
				if (effectiveLength < kvp.Value.StrAttribute.StringMaxLengthInByteArray)
				{
					while (effectiveLength < kvp.Value.StrAttribute.StringMaxLengthInByteArray)
					{
						stringBuffer.AddRange(fillupChar);
						effectiveLength += fillupChar.Length;
					}
				}
				else
				{
					stringBuffer.RemoveRange(kvp.Value.StrAttribute.StringMaxLengthInByteArray,
						stringBuffer.Count - kvp.Value.StrAttribute.StringMaxLengthInByteArray);
					effectiveLength = kvp.Value.StrAttribute.StringMaxLengthInByteArray;
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
		private void WriteDecimalToArray(List<byte> result, decimal value)
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
		private static void Append16(List<byte> target, ushort value, bool bigEndian)
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
		private static void Append32(List<byte> target, uint value, bool bigEndian)
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
		private static void Append64(List<byte> target, ulong value, bool bigEndian)
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


		private void WriteFieldValueToArray(List<byte> result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, T data, List<byte> stringBuffer)
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
						Append32(result, unchecked((uint) enumValue), UseBigEndian);
						break;
					case DestinationType.Int16:
						Append16(result, unchecked((ushort) (short) enumValue), UseBigEndian);
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
				var subArray = field.Get<Guid>(data).ToByteArray();

				// the reader reverses all 16 bytes when big endian is set, so the writer has to do
				// the same, otherwise a Guid cannot be read back by this very converter.
				// Note that this order is a full reversal of Guid.ToByteArray(), it is NOT RFC 4122.
				if (UseBigEndian) Array.Reverse(subArray);

				result.AddRange(subArray);
				return;
			}

			switch (field.FieldTypeCode)
			{
				case TypeCode.Int32:
				{
					Append32(result, unchecked((uint) field.Get<int>(data)), UseBigEndian);
					return;
				}
				case TypeCode.UInt32:
				{
					Append32(result, field.Get<uint>(data), UseBigEndian);
					return;
				}
				case TypeCode.Int16:
				{
					Append16(result, unchecked((ushort) field.Get<short>(data)), UseBigEndian);
					return;
				}
				case TypeCode.UInt16:
				{
					Append16(result, field.Get<ushort>(data), UseBigEndian);
					return;
				}
				case TypeCode.Int64:
				{
					Append64(result, unchecked((ulong) field.Get<long>(data)), UseBigEndian);
					return;
				}
				case TypeCode.UInt64:
				{
					Append64(result, field.Get<ulong>(data), UseBigEndian);
					return;
				}
				case TypeCode.Single:
				{
					Append32(result, unchecked((uint) BitConverter.SingleToInt32Bits(field.Get<float>(data))), UseBigEndian);
					return;
				}
				case TypeCode.Double:
				{
					Append64(result, unchecked((ulong) BitConverter.DoubleToInt64Bits(field.Get<double>(data))), UseBigEndian);
					return;
				}
				case TypeCode.SByte:
					// a single byte has no byte order, so UseBigEndian is irrelevant here
					result.Add(unchecked((byte) field.Get<sbyte>(data)));
					return;
				case TypeCode.Decimal:
					WriteDecimalToArray(result, field.Get<decimal>(data));
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
					var uts = dtf.ToUnixTimeSeconds();

					if (field.DateTimeAttribute.DateTimeByteFormat == DateTimeByteFormat.UnixTimeStamp32Bit)
					{
						Append32(result, unchecked((uint) (int) uts), UseBigEndian);
						return;
					}

					Append64(result, unchecked((ulong) uts), UseBigEndian);
					return;
				}
			}

			if (field.IsString)
			{
				CalculateBufferForString(data, kvp, stringBuffer);

				result.AddRange(stringBuffer);
				return;
			}

			throw new ProtocolConverterException("WriteFieldValueToArray has no converter for " + field.FieldName);
		}
	}
}

