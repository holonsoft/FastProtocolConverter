using holonsoft.FastProtocolConverter.Abstractions.Enums;
using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Attributes
{
    /// <summary>
    /// Field attributes to define and control behaviour of converter
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ProtocolStringFieldAttribute : Attribute
    {
        /// <summary>
        /// Length field name, just needed for type 'string' to determine string length (depending on encoding of source)
        /// Please note that ASCII encoding is half of .net string (UTF16/Unicode)
        /// Hint:
        /// Length information must be provided by protocol due converting of a byte array to an object
        /// Length information will be auto calculated (and written to length field) by converter if object 
        /// is converted to byte array
        /// </summary>
        public string LengthFieldName { get; set; }

        /// <summary>
        /// Define max length of string due writing in byte array.
        /// If set to -1 complete string will be written
        /// </summary>
        public int StringMaxLengthInByteArray { get; set; } = -1;

        /// <summary>
        /// Determine whether string should have a fixed length in byte array
        /// </summary>
        public bool IsFixedLengthString => StringMaxLengthInByteArray > 0;

        /// <summary>
        /// Char to be used to fill shorter strings to max length
        /// </summary>
        public char FillupCharWhenShorter { get; set; }

        /// <summary>
        /// Encoder
        /// </summary>
        public SupportedEncoder Encoder { get; set; } = SupportedEncoder.ASCIIEncoder;
    }
}