using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using holonsoft.FluentConditions;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.dto;


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

            if (_fieldListSeqPos.Count == 0)
            {
                foreach (var kvp in _fieldListFixPos)
                {
                    WriteFieldValueToArray(result, kvp, data);
                }
            }
            else
            {
                CalculateStringForWriting(data);

                foreach (var kvp in _fieldListSeqPos)
                {
                    WriteFieldValueToArray(result, kvp, data);
                }
            }

            return result.ToArray();
        }


        private void CalculateStringForWriting(T data)
        {
            // calc length fields for strings
            foreach (var kvp in _fieldListSeqPos)
            {
                if (!kvp.Value.IsString) continue;

                var converterFieldInfo = kvp.Value;
                var strAttr = converterFieldInfo.StrAttribute;

                kvp.Value.PartialBuffer.Clear();

                byte[] fillupChar = null;

                // encode str to destination byte array
                switch (strAttr.Encoder)
                {
                    case SupportedEncoder.None:
                    case SupportedEncoder.Default:
                        kvp.Value.PartialBuffer.AddRange(Encoding.ASCII.GetBytes((string)kvp.Value.FieldInfo.GetValue(data)));
                        fillupChar = Encoding.ASCII.GetBytes(kvp.Value.StrAttribute.FillupCharWhenShorter.ToString());
                        break;
                    case SupportedEncoder.UnicodeEncoder:
                        kvp.Value.PartialBuffer.AddRange(Encoding.Unicode.GetBytes((string)kvp.Value.FieldInfo.GetValue(data)));
                        fillupChar = Encoding.Unicode.GetBytes(kvp.Value.StrAttribute.FillupCharWhenShorter.ToString());
                        break;
                }

                int effectiveLength = kvp.Value.PartialBuffer.Count;

                if (strAttr.IsFixedLengthString)
                {
                    if (effectiveLength < kvp.Value.StrAttribute.StringMaxLengthInByteArray)
                    {
                        while (effectiveLength < kvp.Value.StrAttribute.StringMaxLengthInByteArray)
                        {
                            kvp.Value.PartialBuffer.AddRange(fillupChar);
                            effectiveLength += fillupChar.Length;
                        }
                    }
                    else
                    {
                        kvp.Value.PartialBuffer.RemoveRange(kvp.Value.StrAttribute.StringMaxLengthInByteArray, kvp.Value.PartialBuffer.Count - kvp.Value.StrAttribute.StringMaxLengthInByteArray);
                        effectiveLength = kvp.Value.StrAttribute.StringMaxLengthInByteArray;
                    }
                }

                var correspondingLengthField = _fieldListByName[strAttr.LengthFieldName];

                if (correspondingLengthField.FieldInfo.FieldType == typeof(int))
                {
                    correspondingLengthField.FieldInfo.SetValue(data, effectiveLength);
                    continue;
                }

                if (correspondingLengthField.FieldInfo.FieldType == typeof(uint))
                {
                    correspondingLengthField.FieldInfo.SetValue(data, (uint)effectiveLength);
                    continue; 
                }

                if (correspondingLengthField.FieldInfo.FieldType == typeof(short))
                {
                    correspondingLengthField.FieldInfo.SetValue(data, (short)effectiveLength);
                    continue;
                }

                if (correspondingLengthField.FieldInfo.FieldType == typeof(ushort))
                {
                    correspondingLengthField.FieldInfo.SetValue(data, (ushort)effectiveLength);
                    continue;
                }

                if (correspondingLengthField.FieldInfo.FieldType == typeof(byte))
                {
                    correspondingLengthField.FieldInfo.SetValue(data, (byte)effectiveLength);
                    continue;
                }

                throw new ProtocolConverterException("CalculateStringForWriting: setting length field type " + correspondingLengthField.FieldInfo.FieldType + " not supported yet");
            }
        }

        private void WriteFieldValueToArray(List<byte> result, KeyValuePair<int, ConverterFieldInfo<T>> kvp, T data)
        {
            var fieldTypeCode = Type.GetTypeCode(kvp.Value.FieldInfo.FieldType);
            var field = kvp.Value.FieldInfo;

            var fieldValue = field.GetValue(data);

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
                        result.Add((byte)(int)fieldValue);
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
                case TypeCode.Byte:
                    var xByte = Convert.ToByte(fieldValue);
                    result.Add(xByte);
                    return;
                case TypeCode.Boolean:
                    var xBool = Convert.ToByte(fieldValue);
                    result.Add(xBool);
                    return;
            }

            if (kvp.Value.IsString)
            {
                result.AddRange(kvp.Value.PartialBuffer);
                return;
            }

            throw new ProtocolConverterException("WriteFieldValueToArray has no converter for " + kvp.Value.FieldName);
        }
    }
}

