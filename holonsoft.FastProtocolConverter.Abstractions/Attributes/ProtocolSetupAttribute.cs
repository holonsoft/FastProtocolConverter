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
    }
}