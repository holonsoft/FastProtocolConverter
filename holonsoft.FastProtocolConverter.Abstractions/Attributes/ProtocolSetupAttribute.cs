using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Attributes
{
    /// <summary>
    /// Control global behaviour of converter 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ProtocolSetupArgument : Attribute
    {
        /// <summary>
        /// Number of bytes the converter skips at the beginning of the byte array.
        /// <para>
        /// This applies to READING ONLY. The skipped bytes belong to a frame header that this POCO
        /// does not describe, and only the calling application knows their content, so
        /// ConvertToByteArray writes the fields of the POCO and nothing else. Its output is therefore
        /// shorter than the protocol by exactly this offset and cannot be passed back into
        /// ConvertFromByteArray without prepending the header yourself.
        /// </para>
        /// <para>
        /// If you instead want reserved bytes that are written as well as read, model them as a field
        /// with <see cref="ProtocolBytePaddingAttribute"/>. That mechanism is symmetric.
        /// </para>
        /// </summary>
        public int OffsetInByteArray { get; set; }

        /// <summary>
        /// Write and read every multi byte value in big endian (network) byte order.
        /// Single byte types are unaffected.
        /// </summary>
        public bool UseBigEndian { get; set; }

        /// <summary>
        /// Culture used to parse the string values of <see cref="ProtocolFieldRangeAttribute"/>,
        /// for example "de-DE" if you prefer to write range limits as "1,5" instead of "1.5".
        /// <para>
        /// Leave it unset to use the invariant culture, which is the default and the recommended
        /// setting. The point of naming the culture here is that it is part of the protocol
        /// definition and therefore identical on every machine. Never rely on the culture of the
        /// operating system for this: a limit of "1.100" means one point one under the invariant
        /// culture but one thousand one hundred under a German one, and the range check would
        /// silently differ per machine.
        /// </para>
        /// </summary>
        public string RangeCulture { get; set; }
    }
}