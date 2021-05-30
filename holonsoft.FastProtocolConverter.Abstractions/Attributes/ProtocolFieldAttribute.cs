using holonsoft.FastProtocolConverter.Abstractions.Enums;
using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Attributes
{
    /// <summary>
    /// Field attributes to define and control behaviour of converter
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ProtocolFieldAttribute : Attribute
    {
        /// <summary>
        /// Ignore field due conversion
        /// </summary>
        public bool IgnoreField { get; set; }

        /// <summary>
        /// Start position in byte stream
        /// set to -1 if variable
        /// </summary>
        public int StartPos { get; set; }

        /// <summary>
        /// Sequence number of field, used if position is variable
        /// </summary>
        public int SequenceNo { get; set; }

        /// <summary>
        /// Used if destination type in array is different, supported only for enums
        /// </summary>
        public DestinationType TypeInByteArray { get; set; }
    }
}