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


namespace holonsoft.FastProtocolConverter
{
	public partial class ProtocolConverter<T>
		where T : class, new()
	{
		private byte[] ConvertToByteArray(T data)
		{
			IsPrepared.Requires("Prepare()").IsTrue();
			data.Requires(nameof(data)).IsNotNull();

			var result = new List<byte>();

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
			var converterFieldInfo = kvp.Value;
			var strAttr = converterFieldInfo.StrAttribute;

			stringBuffer.Clear();

			byte[] fillupChar = null;

			// encode str to destination byte array
			switch (strAttr.Encoder)
			{
				case SupportedEncoder.None:
				case SupportedEncoder.Default:
					stringBuffer.AddRange(Encoding.ASCII.GetBytes((string) kvp.Value.Getter(data)));
					fillupChar = Encoding.ASCII.GetBytes(kvp.Value.StrAttribute.FillupCharWhenShorter.ToString());
					break;
				case SupportedEncoder.UnicodeEncoder:
					stringBuffer.AddRange(Encoding.Unicode.GetBytes((string) kvp.Value.Getter(data)));
					fillupChar = Encoding.Unicode.GetBytes(kvp.Value.StrAttribute.FillupCharWhenShorter.ToString());
					break;
			}

			int effectiveLength = stringBuffer.Count;

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


		private void WriteFieldValueToArray(List<byte> result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, T data, List<byte> stringBuffer)
		{
			var fieldTypeCode = kvp.Value.FieldTypeCode;

			// compiled accessor, this used to be FieldInfo.GetValue per field per message
			var fieldValue = kvp.Value.Getter(data);

			if (kvp.Value.IsEnum)
			{
				switch (kvp.Value.Attribute.TypeInByteArray)
				{
					case DestinationType.None:
					case DestinationType.Default:
					case DestinationType.Int32:
						result.AddRange(UseBigEndian
							? BitConverter.GetBytes((int) fieldValue).Reverse()
							: BitConverter.GetBytes((int) fieldValue));
						break;
					case DestinationType.Int16:

						result.AddRange(UseBigEndian
							? BitConverter.GetBytes((short) (int) fieldValue).Reverse()
							: BitConverter.GetBytes((short) (int) fieldValue));
						break;
					case DestinationType.Byte:
						result.Add((byte) (int) fieldValue);
						break;
				}

				return;
			}

			if (kvp.Value.IsBitValue)
			{
				byte consolidatedBits = 0;
				OnConsolidateBitValues?.Invoke(data, out consolidatedBits);

				result.Add(consolidatedBits);
				return;
			}

			if (kvp.Value.IsGuid)
			{
				var subArray = ((Guid) fieldValue).ToByteArray();

				// the reader reverses all 16 bytes when big endian is set, so the writer has to do
				// the same, otherwise a Guid cannot be read back by this very converter.
				// Note that this order is a full reversal of Guid.ToByteArray(), it is NOT RFC 4122.
				if (UseBigEndian) Array.Reverse(subArray);

				result.AddRange(subArray);
				return;
			}

			switch (fieldTypeCode)
			{
				case TypeCode.Int32:
					result.AddRange(UseBigEndian
						? BitConverter.GetBytes((int) fieldValue).Reverse()
						: BitConverter.GetBytes((int) fieldValue));
					return;
				case TypeCode.UInt32:
					result.AddRange(UseBigEndian
						? BitConverter.GetBytes((uint) fieldValue).Reverse()
						: BitConverter.GetBytes((uint) fieldValue));
					return;
				case TypeCode.Int16:
					result.AddRange(UseBigEndian
						? BitConverter.GetBytes((short) fieldValue).Reverse()
						: BitConverter.GetBytes((short) fieldValue));
					return;
				case TypeCode.UInt16:
					result.AddRange(UseBigEndian
						? BitConverter.GetBytes((ushort) fieldValue).Reverse()
						: BitConverter.GetBytes((ushort) fieldValue));
					return;
				case TypeCode.Int64:
					result.AddRange(UseBigEndian
						? BitConverter.GetBytes((long) fieldValue).Reverse()
						: BitConverter.GetBytes((long) fieldValue));
					return;
				case TypeCode.UInt64:
					result.AddRange(UseBigEndian
						? BitConverter.GetBytes((ulong) fieldValue).Reverse()
						: BitConverter.GetBytes((ulong) fieldValue));
					return;
				case TypeCode.Single:
					result.AddRange(UseBigEndian
						? BitConverter.GetBytes((float) fieldValue).Reverse()
						: BitConverter.GetBytes((float) fieldValue));
					return;
				case TypeCode.Double:
					result.AddRange(UseBigEndian
						? BitConverter.GetBytes((double) fieldValue).Reverse()
						: BitConverter.GetBytes((double) fieldValue));
					return;
				case TypeCode.SByte:
					// a single byte has no byte order, so UseBigEndian is irrelevant here
					result.Add(unchecked((byte) (sbyte) fieldValue));
					return;
				case TypeCode.Decimal:
					WriteDecimalToArray(result, (decimal) fieldValue);
					return;
				case TypeCode.Byte:
					var xByte = Convert.ToByte(fieldValue);

					if (kvp.Value.IsPaddingByte)
					{
						for (var i = 0; i < kvp.Value.BytePaddingAttribute.Padding; i++)
						{
							result.Add(xByte);
						}
					}
					else
					{
						result.Add(xByte);
					}
					
					return;
				case TypeCode.Boolean:
					var xBool = Convert.ToByte(fieldValue);
					result.Add(xBool);
					return;
				case TypeCode.DateTime:
					var dtf = (DateTime) fieldValue;
					dtf = kvp.Value.DateTimeAttribute.DateTimeKind == DateTimeKind.Utc ? dtf.ToUniversalTime() : dtf.ToLocalTime();
					var uts = dtf.ToUnixTimeSeconds();

					if (kvp.Value.DateTimeAttribute.DateTimeByteFormat == DateTimeByteFormat.UnixTimeStamp32Bit)
					{
						result.AddRange(UseBigEndian
							? BitConverter.GetBytes((int) uts).Reverse()
							: BitConverter.GetBytes((int) uts));
						return;
					}
					else
					{
						result.AddRange(UseBigEndian
							? BitConverter.GetBytes(uts).Reverse()
							: BitConverter.GetBytes(uts));
					}
					break;
			}

			if (kvp.Value.IsString)
			{
				CalculateBufferForString(data, kvp, stringBuffer);

				result.AddRange(stringBuffer);
				return;
			}

			throw new ProtocolConverterException("WriteFieldValueToArray has no converter for " + kvp.Value.FieldName);
		}
	}
}

