using System;
using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;

namespace holonsoft.FastProtocolConverter
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
                    MinValue = (T) (object) Int64.Parse(min);
                    MaxValue = (T) (object) Int64.Parse(max);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) Int64.Parse(defaultValue);
                    break;
                case TypeCode.UInt64:
                    MinValue = (T) (object) UInt64.Parse(min);
                    MaxValue = (T) (object) UInt64.Parse(max);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) UInt64.Parse(defaultValue);
                    break;
                case TypeCode.Int32:
                    MinValue = (T) (object) int.Parse(min);
                    MaxValue = (T) (object) int.Parse(max);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) int.Parse(defaultValue);
                    break;
                case TypeCode.UInt32:
                    MinValue = (T) (object) uint.Parse(min);
                    MaxValue = (T) (object) uint.Parse(max);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) uint.Parse(defaultValue);
                    break;
                case TypeCode.Int16:
                    MinValue = (T) (object) Int16.Parse(min);
                    MaxValue = (T) (object) Int16.Parse(max);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) Int16.Parse(defaultValue);
                    break;
                case TypeCode.UInt16:
                    MinValue = (T) (object) UInt16.Parse(min);
                    MaxValue = (T) (object) UInt16.Parse(max);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) UInt16.Parse(defaultValue);
                    break;
                case TypeCode.Decimal:
                    MinValue = (T) (object) Decimal.Parse(min);
                    MaxValue = (T) (object) Decimal.Parse(max);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) Decimal.Parse(defaultValue);
                    break;
                case TypeCode.Single:
                    MinValue = (T) (object) Single.Parse(min);
                    MaxValue = (T) (object) Single.Parse(max);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) Single.Parse(defaultValue);
                    break;
                case TypeCode.Double:
                    MinValue = (T) (object) Double.Parse(min);
                    MaxValue = (T) (object) Double.Parse(max);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) Double.Parse(defaultValue);
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