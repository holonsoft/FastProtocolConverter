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
            : this(range, CultureInfo.InvariantCulture)
        {
        }

        public FieldRangeValue(ProtocolFieldRangeAttribute range, CultureInfo culture)
            : this(range.MinValue, range.MaxValue, range.DefaultValue, culture)
        {
        }

        public FieldRangeValue(string min, string max, string defaultValue)
            : this(min, max, defaultValue, CultureInfo.InvariantCulture)
        {
        }

        /// <summary>
        /// Parses the range limits with an explicitly given culture. The invariant culture is the
        /// default, a different one is only used when the protocol definition names it via
        /// <see cref="ProtocolSetupArgument.RangeCulture"/>. The culture is never taken from the
        /// environment, otherwise the same definition would mean different things per machine.
        /// </summary>
        public FieldRangeValue(string min, string max, string defaultValue, CultureInfo culture)
        {
            culture ??= CultureInfo.InvariantCulture;

            var fieldTypeCode = Type.GetTypeCode(typeof(T));

            switch (fieldTypeCode)
            {
                case TypeCode.Int64:
                    MinValue = (T) (object) Int64.Parse(min, culture);
                    MaxValue = (T) (object) Int64.Parse(max, culture);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) Int64.Parse(defaultValue, culture);
                    break;
                case TypeCode.UInt64:
                    MinValue = (T) (object) UInt64.Parse(min, culture);
                    MaxValue = (T) (object) UInt64.Parse(max, culture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) UInt64.Parse(defaultValue, culture);
                    break;
                case TypeCode.Int32:
                    MinValue = (T) (object) int.Parse(min, culture);
                    MaxValue = (T) (object) int.Parse(max, culture);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) int.Parse(defaultValue, culture);
                    break;
                case TypeCode.UInt32:
                    MinValue = (T) (object) uint.Parse(min, culture);
                    MaxValue = (T) (object) uint.Parse(max, culture);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) uint.Parse(defaultValue, culture);
                    break;
                case TypeCode.Int16:
                    MinValue = (T) (object) Int16.Parse(min, culture);
                    MaxValue = (T) (object) Int16.Parse(max, culture);
                    if (!string.IsNullOrWhiteSpace(defaultValue)) DefaultValue = (T) (object) Int16.Parse(defaultValue, culture);
                    break;
                case TypeCode.UInt16:
                    MinValue = (T) (object) UInt16.Parse(min, culture);
                    MaxValue = (T) (object) UInt16.Parse(max, culture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) UInt16.Parse(defaultValue, culture);
                    break;
                case TypeCode.SByte:
                    MinValue = (T) (object) SByte.Parse(min, culture);
                    MaxValue = (T) (object) SByte.Parse(max, culture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) SByte.Parse(defaultValue, culture);
                    break;
                case TypeCode.Byte:
                    MinValue = (T) (object) Byte.Parse(min, culture);
                    MaxValue = (T) (object) Byte.Parse(max, culture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) Byte.Parse(defaultValue, culture);
                    break;
                case TypeCode.Decimal:
                    MinValue = (T) (object) Decimal.Parse(min, culture);
                    MaxValue = (T) (object) Decimal.Parse(max, culture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) Decimal.Parse(defaultValue, culture);
                    break;
                case TypeCode.Single:
                    MinValue = (T) (object) Single.Parse(min, culture);
                    MaxValue = (T) (object) Single.Parse(max, culture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) Single.Parse(defaultValue, culture);
                    break;
                case TypeCode.Double:
                    MinValue = (T) (object) Double.Parse(min, culture);
                    MaxValue = (T) (object) Double.Parse(max, culture);
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        DefaultValue = (T) (object) Double.Parse(defaultValue, culture);
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