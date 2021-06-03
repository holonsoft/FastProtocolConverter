using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace holonsoft.FastProtocolConverter.dto
{
    /// <summary>
    /// contains infos per field in POCO
    /// </summary>
    public class ConverterFieldInfo<T>
    {
        /// <summary>
        /// Field attributes to define and control behaviour of converter
        /// </summary>
        public ProtocolFieldAttribute Attribute { get; }

        /// <summary>
        /// Additional attribute data if underlying field is a string
        /// </summary>
        public ProtocolStringFieldAttribute StrAttribute { get; }

        /// <summary>
        /// Additional attribute data for fields (STRING and ENUM are not supported)
        /// </summary>
        public ProtocolFieldRangeAttribute RangeAttribute { get; }

        /// <summary>
        /// shortcut flag to indicate whether field type is string
        /// </summary>
        public bool IsString { get; }

        /// <summary>
        /// shortcut flag to indicate whether field type is string
        /// </summary>
        public bool IsEnum { get; }

				/// <summary>
				/// Shortcut flag to indicate that the byte should be treated as several bit values
				/// </summary>
				public bool IsBitValue { get; }

        /// <summary>
        /// Underlying field
        /// </summary>
        public FieldInfo FieldInfo { get; }

        public Action<T, object> Setter { get; }

        /// <summary>
        /// Field size, depends on type and will be calculated only for primitives, string and enum is set to -1
        /// </summary>
        public int ExpectedFieldSize { get; }

        /// <summary>
        /// Shortcut to Field name
        /// </summary>
        public string FieldName => FieldInfo.Name;

        public List<byte> PartialBuffer = new List<byte>();
        private readonly TypeCode _fieldTypeCode;

        public bool UseRangeCheck { get; } = false;


        public FieldRangeValue<int> RangeInt { get; }
        public FieldRangeValue<uint> RangeUInt { get; }
        public FieldRangeValue<long> RangeInt64 { get; }
        public FieldRangeValue<ulong> RangeUInt64 { get; }
        public FieldRangeValue<short> RangeInt16 { get; }
        public FieldRangeValue<ushort> RangeUInt16 { get; }
        public FieldRangeValue<decimal> RangeDecimal { get; }
        public FieldRangeValue<float> RangeSingle { get; }
        public FieldRangeValue<double> RangeDouble { get; }

        public ConverterFieldInfo(FieldInfo fieldInfo, ProtocolFieldAttribute attribute)
        {
            FieldInfo = fieldInfo;

            Setter = FastInvoke.BuildUntypedSetter<T>(fieldInfo);


            Attribute = attribute;

            IsString = FieldInfo.FieldType == typeof(string);
            IsEnum = FieldInfo.FieldType.IsEnum;
            IsBitValue = Attribute.TypeInByteArray == DestinationType.Bits;


            ExpectedFieldSize = IsString || IsEnum? -1 : Marshal.SizeOf(FieldInfo.FieldType);

            if (IsString)
            {
                var x = FieldInfo.GetCustomAttributes(false).Where(y => y.GetType() == typeof(ProtocolStringFieldAttribute)).FirstOrDefault();

                if (x != null)
                {
                    StrAttribute = (ProtocolStringFieldAttribute)x;
                }
            }


            if (!(IsString || IsEnum))
            {
                var r = FieldInfo.GetCustomAttributes(false).Where(y => y.GetType() == typeof(ProtocolFieldRangeAttribute)).FirstOrDefault();
                if (r != null)
                {
                    UseRangeCheck = true;

                    var rawRangeAttribute = (ProtocolFieldRangeAttribute)r;
                    
                    _fieldTypeCode = Type.GetTypeCode(FieldInfo.FieldType);

                    switch (_fieldTypeCode)
                    {
                        case TypeCode.Int64:
                            RangeInt64 = new FieldRangeValue<Int64>(rawRangeAttribute);
                            break;
                        case TypeCode.UInt64:
                            RangeUInt64 = new FieldRangeValue<UInt64>(rawRangeAttribute);
                            break;
                        case TypeCode.Int32:
                            RangeInt = new FieldRangeValue<int>(rawRangeAttribute);
                            break;
                        case TypeCode.UInt32:
                            RangeUInt = new FieldRangeValue<uint>(rawRangeAttribute);
                            break;
                        case TypeCode.Int16:
                            RangeInt16 = new FieldRangeValue<Int16>(rawRangeAttribute);
                            break;
                        case TypeCode.UInt16:
                            RangeUInt16 = new FieldRangeValue<UInt16>(rawRangeAttribute);
                            break;
                        case TypeCode.Decimal:
                            RangeDecimal = new FieldRangeValue<decimal>(rawRangeAttribute);
                            break;
                        case TypeCode.Single:
                            RangeSingle = new FieldRangeValue<Single>(rawRangeAttribute);
                            break;
                        case TypeCode.Double:
                            RangeDouble = new FieldRangeValue<Double>(rawRangeAttribute);
                            break;
                    }
                }
            }
        }


        public bool IsInRange(object val)
        {
            switch (_fieldTypeCode)
            {
                case TypeCode.Int64:
                    return RangeInt64.IsInRange((long)val);
                case TypeCode.UInt64:
                    return RangeUInt64.IsInRange((ulong)val);
                case TypeCode.Int32:
                    return RangeInt.IsInRange((int)val);
                case TypeCode.UInt32:
                    return RangeUInt.IsInRange((uint)val);
                case TypeCode.Int16:
                    return RangeInt16.IsInRange((Int16)val);
                case TypeCode.UInt16:
                    return RangeUInt16.IsInRange((UInt16)val);
                case TypeCode.Decimal:
                    return RangeDecimal.IsInRange((decimal)val);
                case TypeCode.Single:
                    return RangeSingle.IsInRange((Single)val);
                case TypeCode.Double:
                    return RangeDouble.IsInRange((double)val);
            }

            return false;
        }
    }
}
