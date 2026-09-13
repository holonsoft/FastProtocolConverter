using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using System;
using System.Globalization;

namespace holonsoft.FastProtocolConverter.dto
{
    public class FieldRangeValue<T>
        where T : IComparable<T>
    {
        public FieldRangeValue(T min, T max)
            : this(min, max, default)
        {
        }

        public FieldRangeValue(T min, T max, T defaultValue)
        {
            MinValue = min;
            MaxValue = max;
            DefaultValue = defaultValue;
        }

        public FieldRangeValue(ProtocolFieldRangeAttribute range)
            : this(range.MinValue, range.MaxValue, range.DefaultValue)
        {
        }

        public FieldRangeValue(string min, string max, string defaultValue)
        {
            var fieldTypeCode = Type.GetTypeCode(typeof(T));

            switch (fieldTypeCode)
            {
                case TypeCode.Int64:
                    MinValue = (T) (object) Int64.Parse(min, CultureInfo.InvariantCulture);
                    MaxValue = (T) (object) Int64.Parse(max, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) Int64.Parse(defaultValue, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.UInt64:
                    MinValue = (T) (object) UInt64.Parse(min, CultureInfo.InvariantCulture);
                    MaxValue = (T) (object) UInt64.Parse(max, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) UInt64.Parse(defaultValue, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.Int32:
                    MinValue = (T) (object) int.Parse(min, CultureInfo.InvariantCulture);
                    MaxValue = (T) (object) int.Parse(max, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) int.Parse(defaultValue, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.UInt32:
                    MinValue = (T) (object) uint.Parse(min, CultureInfo.InvariantCulture);
                    MaxValue = (T) (object) uint.Parse(max, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) uint.Parse(defaultValue, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.Int16:
                    MinValue = (T) (object) Int16.Parse(min, CultureInfo.InvariantCulture);
                    MaxValue = (T) (object) Int16.Parse(max, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) Int16.Parse(defaultValue, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.UInt16:
                    MinValue = (T) (object) UInt16.Parse(min, CultureInfo.InvariantCulture);
                    MaxValue = (T) (object) UInt16.Parse(max, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) UInt16.Parse(defaultValue, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.SByte:
                    MinValue = (T) (object) SByte.Parse(min, CultureInfo.InvariantCulture);
                    MaxValue = (T) (object) SByte.Parse(max, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) SByte.Parse(defaultValue, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.Byte:
                    MinValue = (T) (object) Byte.Parse(min, CultureInfo.InvariantCulture);
                    MaxValue = (T) (object) Byte.Parse(max, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) Byte.Parse(defaultValue, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.Decimal:
                    MinValue = (T) (object) Decimal.Parse(min, CultureInfo.InvariantCulture);
                    MaxValue = (T) (object) Decimal.Parse(max, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) Decimal.Parse(defaultValue, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.Single:
                    MinValue = (T) (object) Single.Parse(min, CultureInfo.InvariantCulture);
                    MaxValue = (T) (object) Single.Parse(max, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) Single.Parse(defaultValue, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.Double:
                    MinValue = (T) (object) Double.Parse(min, CultureInfo.InvariantCulture);
                    MaxValue = (T) (object) Double.Parse(max, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) Double.Parse(defaultValue, CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new ProtocolConverterException("No range support for type " + typeof(T).Name);
            }
        }

        public T MinValue { get; set; }
        public T MaxValue { get; set; }
        public T DefaultValue { get; set; }


        public bool IsInRange(T val)
        {
            return (MinValue.CompareTo(val) <= 0) && (MaxValue.CompareTo(val) >= 0);
        }


        public bool IsDefaultValue(T val)
        {
            return DefaultValue.CompareTo(val) == 0;
        }
    }
}