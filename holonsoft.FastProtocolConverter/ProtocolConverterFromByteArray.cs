using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.dto;
using holonsoft.FluentConditions;
using holonsoft.FluentDateTime.DateTime;
using System.Linq;

namespace holonsoft.FastProtocolConverter
{
	public partial class ProtocolConverter<T>
		where T : class, new()
	{

		/// <summary>
		/// A byte array that cannot possibly hold the protocol is a protocol condition, not a
		/// programmer error, so it is reported as ProtocolConverterException in every code path.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void EnsureMinimumLength(byte[] data)
		{
			data.Requires(nameof(data)).IsNotNull();

			if (data.Length < _totalMinLength)
			{
				var msg = $"Too less data in byte stream, {_totalMinLength} bytes are needed for {typeof(T).Name} but only {data.Length} were provided";

				// the most common way to land exactly 'offset' bytes short is to feed the output of
				// ConvertToByteArray back in, which does not write the skipped header bytes
				if ((_globalOffsetInByteArray > 0) && (data.Length + _globalOffsetInByteArray >= _totalMinLength))
				{
					msg += $". {typeof(T).Name} declares OffsetInByteArray = {_globalOffsetInByteArray}, and that offset applies to reading only."
						+ " ConvertToByteArray does not write those leading bytes, so its result has to be prefixed with the frame header before it can be read back."
						+ " Use ProtocolBytePadding on a field instead if you want reserved bytes that are written as well.";
				}

				throw new ProtocolConverterException(msg);
			}
		}


		/// <summary>
		/// Guards a single field read. Without it a truncated frame surfaced as a raw
		/// ArgumentException from Array.Copy or BitConverter somewhere deep inside the converter.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void EnsureRoomForField(byte[] data, int position, int requiredBytes, ConverterFieldInfo<T> field)
		{
			if ((position < 0) || (requiredBytes < 0) || (position + requiredBytes > data.Length))
			{
				throw new ProtocolConverterException(
					$"Too less data in byte stream, field '{field.FieldName}' needs {requiredBytes} bytes at position {position} but the array holds only {data.Length}");
			}
		}


		private T ConvertFromByteArray(byte[] data)
		{
			IsPrepared.Requires("Prepare()").IsTrue();

			EnsureMinimumLength(data);

			var result = new T();

			// just a simple protocol in terms of fixed length fields
			if (_fieldListSeqPos.Count == 0)
			{
				foreach (var kvp in _fieldListFixPos)
				{
					SetFieldValue(result, kvp, -1, data);
				}
			}
			else
			{
				ResolveComplexProtocol(result, data);
			}

			return result;
		}


		private void ConvertFromByteArray(byte[] data, T instance)
		{
			IsPrepared.Requires("Prepare()").IsTrue();
			instance.Requires(nameof(instance)).IsNotNull();

			EnsureMinimumLength(data);

			var result = instance;

			// just a simple protocol in terms of fixed length fields
			if (_fieldListSeqPos.Count == 0)
			{
				foreach (var kvp in _fieldListFixPos)
				{
					SetFieldValue(result, kvp, -1, data);
				}
			}
			else
			{
				ResolveComplexProtocol(result, data);
			}
		}




		private void ResolveComplexProtocol(T result, byte[] data)
		{
			var lengthOfData = data.Length;

			var actualPosition = _globalOffsetInByteArray;
			foreach (var kvp in _fieldListSeqPos)
			{
				if (actualPosition > lengthOfData)
				{
					throw new ProtocolConverterException(
						$"Too less data in byte stream, position {actualPosition} is behind the end of the {lengthOfData} byte array");
				}


				if (kvp.Value.IsString)
				{
					var length = kvp.Value.StrAttribute.IsFixedLengthString? kvp.Value.StrAttribute.StringMaxLengthInByteArray : ReadLengthFieldValue(result, kvp.Value);

					// the length of a variable string comes out of the frame itself and can be a lie
					EnsureRoomForField(data, actualPosition, length, kvp.Value);

					string dataStr;

					switch (kvp.Value.StrAttribute.Encoder)
					{
						case SupportedEncoder.UnicodeEncoder:
							dataStr = FromByteArrayToStringConverter(data, actualPosition, length, Encoding.Unicode);
							break;
						case SupportedEncoder.None:
						case SupportedEncoder.ASCIIEncoder: // is default
						default:
							dataStr = FromByteArrayToStringConverter(data, actualPosition, length, Encoding.ASCII);
							break;
					}

					kvp.Value.FieldInfo.SetValue(result, dataStr);
					actualPosition += length;

					continue;
				}

				actualPosition += SetFieldValue(result, kvp, actualPosition, data);

			}
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private string FromByteArrayToStringConverter(byte[] data, int actualPosition, int length, Encoding encoding)
		{
			var strArray = new byte[length];

			// see https://gist.github.com/fraguada/301fe31b939e6889514969cb1bbec37c
			//Array.Copy: 360ms
			//Linq: 31335ms
			//Buffer.BlockCopy: 84ms
			//ArraySegment: 600m
			Buffer.BlockCopy(data, actualPosition, strArray, 0, length);

			return encoding.GetString(strArray);
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int ReadLengthFieldValue(T result, ConverterFieldInfo<T> inspectedField)
		{
			var lengthFieldInfo = _fieldListByName[inspectedField.StrAttribute.LengthFieldName];

			return (int) Convert.ToInt32(lengthFieldInfo.FieldInfo.GetValue(result));
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldValue(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, int position, byte[] data)
		{
			var pos = position == -1 ? kvp.Key + _globalOffsetInByteArray : position;

			// central bounds guard, so no read can run past the end of the array below
			var effectiveSize = kvp.Value.EffectiveFieldSize;

			if (effectiveSize > 0) EnsureRoomForField(data, pos, effectiveSize, kvp.Value);

			if (kvp.Value.IsBitValue)
			{
				OnSplitBitValues?.Invoke(data[pos], result);
				return 1;
			}


			if (kvp.Value.IsGuid)
			{
				return SetFieldHandleGuidValues(result, kvp, data, pos);
			}

			if (kvp.Value.IsString)
			{
				string dataStr;

				switch (kvp.Value.StrAttribute.Encoder)
				{
					case SupportedEncoder.UnicodeEncoder:
						dataStr = FromByteArrayToStringConverter(data, pos, kvp.Value.StrAttribute.StringMaxLengthInByteArray, Encoding.Unicode);
						break;
					case SupportedEncoder.None:
					case SupportedEncoder.ASCIIEncoder: // is default
					default:
						dataStr = FromByteArrayToStringConverter(data, pos, kvp.Value.StrAttribute.StringMaxLengthInByteArray, Encoding.ASCII);
						break;
				}

				if (kvp.Value.StrAttribute.IsFixedLengthString)
				{
					var i = dataStr.Length - 1;

					while ((i >= 0) && (dataStr[i] == kvp.Value.StrAttribute.FillupCharWhenShorter))
					{
						i--;
					}

					if (i > -1)
					{
						dataStr = dataStr.Substring(0, i + 1);
					}

				}

				kvp.Value.FieldInfo.SetValue(result, dataStr);
				return kvp.Value.StrAttribute.StringMaxLengthInByteArray;
			}


			var fieldTypeCode = Type.GetTypeCode(kvp.Value.FieldInfo.FieldType);

			switch (fieldTypeCode)
			{
				case TypeCode.Int32:
					return SetFieldHandleIntValues(result, kvp, data, pos);

				case TypeCode.UInt32:
					return SetFieldHandleUIntValues(result, kvp, data, pos);

				case TypeCode.Int16:
					return SetFieldHandleShortValues(result, kvp, data, pos);

				case TypeCode.UInt16:
					return SetFieldHandleUShortValues(result, kvp, data, pos);

				case TypeCode.Int64:
					return SetFieldHandleInt64Values(result, kvp, data, pos);

				case TypeCode.UInt64:
					return SetFieldHandleUInt64Values(result, kvp, data, pos);

				case TypeCode.Byte:
					kvp.Value.Setter(result, data[pos]);
					return kvp.Value.IsPaddingByte ? kvp.Value.BytePaddingAttribute.Padding : 1;

				case TypeCode.SByte:
					return SetFieldHandleSByteValues(result, kvp, data, pos);

				case TypeCode.Decimal:
					return SetFieldHandleDecimalValues(result, kvp, data, pos);

				case TypeCode.Single:
					return SetFieldHandleFloatValues(result, kvp, data, pos);

				case TypeCode.Double:
					return SetFieldHandleDoubleValues(result, kvp, data, pos);

				case TypeCode.Boolean:
					//fieldInfo.SetValue(result, data[pos] == 1 ? true : false);
					kvp.Value.Setter(result, data[pos] == 1 ? true : false);
					return 1;
				case TypeCode.DateTime:
					return SetFieldHandleDateTimeValues(result, kvp, data, pos);
			}

			throw new NotImplementedException("Datatype " + kvp.Value.FieldInfo.FieldType + " has no matching converter");
		}

		

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleSByteValues(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			// a single byte has no byte order, so UseBigEndian is irrelevant here
			var sByteVal = unchecked((sbyte) data[pos]);

			if (kvp.Value.UseRangeCheck && !kvp.Value.IsInRange(sByteVal))
			{
				var behaviourRangeViolation = ConverterRangeViolationBehaviour.None;

				OnRangeViolation?.Invoke(kvp.Value.FieldInfo, out behaviourRangeViolation);
				switch (behaviourRangeViolation)
				{
					case ConverterRangeViolationBehaviour.None:
					case ConverterRangeViolationBehaviour.IgnoreAndContinue:
						kvp.Value.Setter(result, sByteVal);
						break;
					case ConverterRangeViolationBehaviour.SetToMinValue:
						kvp.Value.Setter(result, kvp.Value.RangeSByte.MinValue);
						break;
					case ConverterRangeViolationBehaviour.SetToMaxValue:
						kvp.Value.Setter(result, kvp.Value.RangeSByte.MaxValue);
						break;
					case ConverterRangeViolationBehaviour.SetToDefaultValue:
						kvp.Value.Setter(result, kvp.Value.RangeSByte.DefaultValue);
						break;
					case ConverterRangeViolationBehaviour.ThrowException:
						throw new ProtocolConverterException("Field value out of range " + kvp.Value.FieldInfo.Name);
				}
			}
			else
			{
				kvp.Value.Setter(result, sByteVal);
			}

			return 1;
		}


		/// <summary>
		/// A decimal is stored as its four component integers (low, mid, high, flags) in exactly that
		/// order. UseBigEndian swaps the bytes inside every component, the order of the components
		/// themselves never changes. See <see cref="decimal.GetBits(decimal, Span{int})"/>.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleDecimalValues(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			Span<int> bits = stackalloc int[4];

			for (var i = 0; i < 4; i++)
			{
				var offset = pos + (i * 4);

				bits[i] = UseBigEndian
					? (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]
					: data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
			}

			decimal decimalVal;

			try
			{
				decimalVal = new decimal(bits);
			}
			catch (ArgumentException ex)
			{
				throw new ProtocolConverterException(
					"Invalid decimal representation in byte stream for field " + kvp.Value.FieldInfo.Name, ex);
			}

			if (kvp.Value.UseRangeCheck && !kvp.Value.IsInRange(decimalVal))
			{
				var behaviourRangeViolation = ConverterRangeViolationBehaviour.None;

				OnRangeViolation?.Invoke(kvp.Value.FieldInfo, out behaviourRangeViolation);
				switch (behaviourRangeViolation)
				{
					case ConverterRangeViolationBehaviour.None:
					case ConverterRangeViolationBehaviour.IgnoreAndContinue:
						kvp.Value.Setter(result, decimalVal);
						break;
					case ConverterRangeViolationBehaviour.SetToMinValue:
						kvp.Value.Setter(result, kvp.Value.RangeDecimal.MinValue);
						break;
					case ConverterRangeViolationBehaviour.SetToMaxValue:
						kvp.Value.Setter(result, kvp.Value.RangeDecimal.MaxValue);
						break;
					case ConverterRangeViolationBehaviour.SetToDefaultValue:
						kvp.Value.Setter(result, kvp.Value.RangeDecimal.DefaultValue);
						break;
					case ConverterRangeViolationBehaviour.ThrowException:
						throw new ProtocolConverterException("Field value out of range " + kvp.Value.FieldInfo.Name);
				}
			}
			else
			{
				kvp.Value.Setter(result, decimalVal);
			}

			return 16;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleDoubleValues(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			double doubleVal;

			if (UseBigEndian)
			{
				var buffer = new byte[8]
				{
					data[pos + 7], data[pos + 6], data[pos + 5], data[pos + 4], data[pos + 3], data[pos + 2], data[pos + 1],
					data[pos + 0],
				};

				doubleVal = BitConverter.ToDouble(buffer, 0);
			}
			else
			{
				doubleVal = BitConverter.ToDouble(data, pos);
			}


			if (kvp.Value.UseRangeCheck && !kvp.Value.IsInRange(doubleVal))
			{
				var behaviourRangeViolation = ConverterRangeViolationBehaviour.None;

				OnRangeViolation?.Invoke(kvp.Value.FieldInfo, out behaviourRangeViolation);
				switch (behaviourRangeViolation)
				{
					case ConverterRangeViolationBehaviour.None:
					case ConverterRangeViolationBehaviour.IgnoreAndContinue:
						kvp.Value.Setter(result, doubleVal);
						break;
					case ConverterRangeViolationBehaviour.SetToMinValue:
						kvp.Value.Setter(result, kvp.Value.RangeDouble.MinValue);
						break;
					case ConverterRangeViolationBehaviour.SetToMaxValue:
						kvp.Value.Setter(result, kvp.Value.RangeDouble.MaxValue);
						break;
					case ConverterRangeViolationBehaviour.SetToDefaultValue:
						kvp.Value.Setter(result, kvp.Value.RangeDouble.DefaultValue);
						break;
					case ConverterRangeViolationBehaviour.ThrowException:
						throw new ProtocolConverterException("Field value out of range " + kvp.Value.FieldInfo.Name);
				}
			}
			else
			{
				kvp.Value.Setter(result, doubleVal);
			}

			return 8;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleFloatValues(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			float floatVal;

			if (UseBigEndian)
			{
				var buffer = new byte[4] {data[pos + 3], data[pos + 2], data[pos + 1], data[pos + 0],};

				floatVal = BitConverter.ToSingle(buffer, 0);
			}
			else
			{
				floatVal = BitConverter.ToSingle(data, pos);
			}


			if (kvp.Value.UseRangeCheck && !kvp.Value.IsInRange(floatVal))
			{
				var behaviourRangeViolation = ConverterRangeViolationBehaviour.None;

				OnRangeViolation?.Invoke(kvp.Value.FieldInfo, out behaviourRangeViolation);
				switch (behaviourRangeViolation)
				{
					case ConverterRangeViolationBehaviour.None:
					case ConverterRangeViolationBehaviour.IgnoreAndContinue:
						kvp.Value.Setter(result, floatVal);
						break;
					case ConverterRangeViolationBehaviour.SetToMinValue:
						kvp.Value.Setter(result, kvp.Value.RangeSingle.MinValue);
						break;
					case ConverterRangeViolationBehaviour.SetToMaxValue:
						kvp.Value.Setter(result, kvp.Value.RangeSingle.MaxValue);
						break;
					case ConverterRangeViolationBehaviour.SetToDefaultValue:
						kvp.Value.Setter(result, kvp.Value.RangeSingle.DefaultValue);
						break;
					case ConverterRangeViolationBehaviour.ThrowException:
						throw new ProtocolConverterException("Field value out of range " + kvp.Value.FieldInfo.Name);
				}
			}
			else
			{
				kvp.Value.Setter(result, floatVal);
			}

			return 4;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleUInt64Values(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			ulong ulongVal;

			if (UseBigEndian)
			{
				var buffer = new byte[8]
				{
					data[pos + 7], data[pos + 6], data[pos + 5], data[pos + 4], data[pos + 3], data[pos + 2], data[pos + 1],
					data[pos + 0],
				};

				ulongVal = BitConverter.ToUInt64(buffer, 0);
			}
			else
			{
				ulongVal = BitConverter.ToUInt64(data, pos);
			}


			if (kvp.Value.UseRangeCheck && !kvp.Value.IsInRange(ulongVal))
			{
				var behaviourRangeViolation = ConverterRangeViolationBehaviour.None;

				OnRangeViolation?.Invoke(kvp.Value.FieldInfo, out behaviourRangeViolation);
				switch (behaviourRangeViolation)
				{
					case ConverterRangeViolationBehaviour.None:
					case ConverterRangeViolationBehaviour.IgnoreAndContinue:
						kvp.Value.Setter(result, ulongVal);
						break;
					case ConverterRangeViolationBehaviour.SetToMinValue:
						kvp.Value.Setter(result, kvp.Value.RangeUInt64.MinValue);
						break;
					case ConverterRangeViolationBehaviour.SetToMaxValue:
						kvp.Value.Setter(result, kvp.Value.RangeUInt64.MaxValue);
						break;
					case ConverterRangeViolationBehaviour.SetToDefaultValue:
						kvp.Value.Setter(result, kvp.Value.RangeUInt64.DefaultValue);
						break;
					case ConverterRangeViolationBehaviour.ThrowException:
						throw new ProtocolConverterException("Field value out of range " + kvp.Value.FieldInfo.Name);
				}
			}
			else
			{
				kvp.Value.Setter(result, ulongVal);
			}

			return 8;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleInt64Values(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			long longVal;

			if (UseBigEndian)
			{
				var buffer = new byte[8]
				{
					data[pos + 7], data[pos + 6], data[pos + 5], data[pos + 4], data[pos + 3], data[pos + 2], data[pos + 1],
					data[pos + 0],
				};

				longVal = BitConverter.ToInt64(buffer, 0);
			}
			else
			{
				longVal = BitConverter.ToInt64(data, pos);
			}


			if (kvp.Value.UseRangeCheck && !kvp.Value.IsInRange(longVal))
			{
				var behaviourRangeViolation = ConverterRangeViolationBehaviour.None;

				OnRangeViolation?.Invoke(kvp.Value.FieldInfo, out behaviourRangeViolation);
				switch (behaviourRangeViolation)
				{
					case ConverterRangeViolationBehaviour.None:
					case ConverterRangeViolationBehaviour.IgnoreAndContinue:
						kvp.Value.Setter(result, longVal);
						break;
					case ConverterRangeViolationBehaviour.SetToMinValue:
						kvp.Value.Setter(result, kvp.Value.RangeInt64.MinValue);
						break;
					case ConverterRangeViolationBehaviour.SetToMaxValue:
						kvp.Value.Setter(result, kvp.Value.RangeInt64.MaxValue);
						break;
					case ConverterRangeViolationBehaviour.SetToDefaultValue:
						kvp.Value.Setter(result, kvp.Value.RangeInt64.DefaultValue);
						break;
					case ConverterRangeViolationBehaviour.ThrowException:
						throw new ProtocolConverterException("Field value out of range " + kvp.Value.FieldInfo.Name);
				}
			}
			else
			{
				//field.SetValue(result, longVal);
				kvp.Value.Setter(result, longVal);
			}

			return 8;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleUShortValues(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			ushort uint16Val;

			if (UseBigEndian)
			{
				var buffer = new byte[2] {data[pos + 1], data[pos + 0],};

				uint16Val = BitConverter.ToUInt16(buffer, 0);
			}
			else
			{
				uint16Val = BitConverter.ToUInt16(data, pos);
			}


			if (kvp.Value.UseRangeCheck && !kvp.Value.IsInRange(uint16Val))
			{
				var behaviourRangeViolation = ConverterRangeViolationBehaviour.None;

				OnRangeViolation?.Invoke(kvp.Value.FieldInfo, out behaviourRangeViolation);
				switch (behaviourRangeViolation)
				{
					case ConverterRangeViolationBehaviour.None:
					case ConverterRangeViolationBehaviour.IgnoreAndContinue:
						kvp.Value.Setter(result, uint16Val);
						break;
					case ConverterRangeViolationBehaviour.SetToMinValue:
						kvp.Value.Setter(result, kvp.Value.RangeUInt16.MinValue);
						break;
					case ConverterRangeViolationBehaviour.SetToMaxValue:
						kvp.Value.Setter(result, kvp.Value.RangeUInt16.MaxValue);
						break;
					case ConverterRangeViolationBehaviour.SetToDefaultValue:
						kvp.Value.Setter(result, kvp.Value.RangeUInt16.DefaultValue);
						break;
					case ConverterRangeViolationBehaviour.ThrowException:
						throw new ProtocolConverterException("Field value out of range " + kvp.Value.FieldInfo.Name);
				}
			}
			else
			{
				//field.SetValue(result, uintVal);
				kvp.Value.Setter(result, uint16Val);
			}

			return 2;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleShortValues(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			short shortVal;

			if (UseBigEndian)
			{
				var buffer = new byte[2] {data[pos + 1], data[pos + 0],};

				shortVal = BitConverter.ToInt16(buffer, 0);
			}
			else
			{
				shortVal = BitConverter.ToInt16(data, pos);
			}


			if (kvp.Value.UseRangeCheck && !kvp.Value.IsInRange(shortVal))
			{
				var behaviourRangeViolation = ConverterRangeViolationBehaviour.None;

				OnRangeViolation?.Invoke(kvp.Value.FieldInfo, out behaviourRangeViolation);
				switch (behaviourRangeViolation)
				{
					case ConverterRangeViolationBehaviour.None:
					case ConverterRangeViolationBehaviour.IgnoreAndContinue:
						kvp.Value.Setter(result, shortVal);
						break;
					case ConverterRangeViolationBehaviour.SetToMinValue:
						kvp.Value.Setter(result, kvp.Value.RangeInt16.MinValue);
						break;
					case ConverterRangeViolationBehaviour.SetToMaxValue:
						kvp.Value.Setter(result, kvp.Value.RangeInt16.MaxValue);
						break;
					case ConverterRangeViolationBehaviour.SetToDefaultValue:
						kvp.Value.Setter(result, kvp.Value.RangeInt16.DefaultValue);
						break;
					case ConverterRangeViolationBehaviour.ThrowException:
						throw new ProtocolConverterException("Field value out of range " + kvp.Value.FieldInfo.Name);
				}
			}
			else
			{
				//field.SetValue(result, uintVal);
				kvp.Value.Setter(result, shortVal);
			}

			return 2;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleUIntValues(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			uint uintVal;

			if (UseBigEndian)
			{
				var buffer = new byte[4] {data[pos + 3], data[pos + 2], data[pos + 1], data[pos + 0],};

				uintVal = BitConverter.ToUInt32(buffer, 0);
			}
			else
			{
				uintVal = BitConverter.ToUInt32(data, pos);
			}


			if (kvp.Value.UseRangeCheck && !kvp.Value.IsInRange(uintVal))
			{
				var behaviourRangeViolation = ConverterRangeViolationBehaviour.None;

				OnRangeViolation?.Invoke(kvp.Value.FieldInfo, out behaviourRangeViolation);
				switch (behaviourRangeViolation)
				{
					case ConverterRangeViolationBehaviour.None:
					case ConverterRangeViolationBehaviour.IgnoreAndContinue:
						kvp.Value.Setter(result, uintVal);
						break;
					case ConverterRangeViolationBehaviour.SetToMinValue:
						kvp.Value.Setter(result, kvp.Value.RangeUInt.MinValue);
						break;
					case ConverterRangeViolationBehaviour.SetToMaxValue:
						kvp.Value.Setter(result, kvp.Value.RangeUInt.MaxValue);
						break;
					case ConverterRangeViolationBehaviour.SetToDefaultValue:
						kvp.Value.Setter(result, kvp.Value.RangeUInt.DefaultValue);
						break;
					case ConverterRangeViolationBehaviour.ThrowException:
						throw new ProtocolConverterException("Field value out of range " + kvp.Value.FieldInfo.Name);
				}
			}
			else
			{
				//field.SetValue(result, uintVal);
				kvp.Value.Setter(result, uintVal);
			}

			return 4;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleIntValues(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			if (kvp.Value.IsEnum)
			{
				int returnVal;

				switch (kvp.Value.Attribute.TypeInByteArray)
				{
					case DestinationType.Byte:
						_converterHelper[0] = data[pos];
						_converterHelper[1] = 0;
						_converterHelper[2] = 0;
						_converterHelper[3] = 0;
						returnVal = 1;
						break;
					case DestinationType.Int16:
						if (UseBigEndian)
						{
							_converterHelper[0] = data[pos + 1];
							_converterHelper[1] = data[pos];
							_converterHelper[2] = 0;
							_converterHelper[3] = 0;
						}
						else
						{
							_converterHelper[0] = data[pos];
							_converterHelper[1] = data[pos + 1];
							_converterHelper[2] = 0;
							_converterHelper[3] = 0;
						}

						returnVal = 2;
						break;
					case DestinationType.Int32:
					case DestinationType.Default:
						if (UseBigEndian)
						{
							_converterHelper[3] = data[pos];
							_converterHelper[2] = data[pos + 1];
							_converterHelper[1] = data[pos + 2];
							_converterHelper[0] = data[pos + 3];
						}
						else
						{
							_converterHelper[0] = data[pos];
							_converterHelper[1] = data[pos + 1];
							_converterHelper[2] = data[pos + 2];
							_converterHelper[3] = data[pos + 3];
						}

						returnVal = 4;
						break;
					default:
						throw new ProtocolConverterException("Conversion for enum " + kvp.Value.FieldName + " not supported");
				}

				var x = (BitConverter.ToInt32(_converterHelper, 0)).ToString(CultureInfo.InvariantCulture);
				var value = Enum.Parse(kvp.Value.FieldInfo.FieldType, x);


				//field.SetValue(result, value);
				kvp.Value.Setter(result, value);
				return returnVal;
			}

			int intVal;

			if (UseBigEndian)
			{
				var buffer = new byte[4] {data[pos + 3], data[pos + 2], data[pos + 1], data[pos + 0],};

				intVal = BitConverter.ToInt32(buffer, 0);
			}
			else
			{
				intVal = BitConverter.ToInt32(data, pos);
			}

			if (kvp.Value.UseRangeCheck && !kvp.Value.IsInRange(intVal))
			{
				var behaviourRangeViolation = ConverterRangeViolationBehaviour.None;

				OnRangeViolation?.Invoke(kvp.Value.FieldInfo, out behaviourRangeViolation);
				switch (behaviourRangeViolation)
				{
					case ConverterRangeViolationBehaviour.None:
					case ConverterRangeViolationBehaviour.IgnoreAndContinue:
						kvp.Value.Setter(result, intVal);
						break;
					case ConverterRangeViolationBehaviour.SetToMinValue:
						kvp.Value.Setter(result, kvp.Value.RangeInt.MinValue);
						break;
					case ConverterRangeViolationBehaviour.SetToMaxValue:
						kvp.Value.Setter(result, kvp.Value.RangeInt.MaxValue);
						break;
					case ConverterRangeViolationBehaviour.SetToDefaultValue:
						kvp.Value.Setter(result, kvp.Value.RangeInt.DefaultValue);
						break;
					case ConverterRangeViolationBehaviour.ThrowException:
						throw new ProtocolConverterException("Field value out of range " + kvp.Value.FieldInfo.Name);
				}
			}
			else
			{
				//field.SetValue(result, intVal);
				kvp.Value.Setter(result, intVal);
			}

			return 4;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleGuidValues(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			var buffer = new byte[16];
			Array.Copy(data, pos, buffer, 0, 16);

			Guid value = UseBigEndian ? new Guid(buffer.Reverse().ToArray()) : new Guid(buffer);

			kvp.Value.Setter(result, value);

			return 16;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SetFieldHandleDateTimeValues(T result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, byte[] data, int pos)
		{
			DateTime dtValue;

			switch (kvp.Value.DateTimeAttribute.DateTimeByteFormat)
			{
				case DateTimeByteFormat.UnixTimeStamp32Bit:
					long intVal;

					if (UseBigEndian)
					{
						var buffer = new byte[4]
						{
							data[pos + 3], data[pos + 2], data[pos + 1], data[pos + 0],
						};

						intVal = BitConverter.ToInt32(buffer, 0);
					}
					else
					{
						intVal = BitConverter.ToInt32(data, pos);
					}

					dtValue = DateTimeExtensions.UnixEpoch.AddSeconds(intVal);
					kvp.Value.Setter(result, dtValue);
					return 4;
				case DateTimeByteFormat.UnixTimeStamp64Bit:
					long longVal;

					if (UseBigEndian)
					{
						var buffer = new byte[8]
						{
							data[pos + 7], data[pos + 6], data[pos + 5], data[pos + 4], data[pos + 3], data[pos + 2], data[pos + 1],
							data[pos + 0],
						};

						longVal = BitConverter.ToInt64(buffer, 0);
					}
					else
					{
						longVal = BitConverter.ToInt64(data, pos);
					}

					dtValue = DateTimeExtensions.UnixEpoch.AddSeconds(longVal);
					kvp.Value.Setter(result, dtValue);
					return 8;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

	}
}

