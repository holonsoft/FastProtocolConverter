using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Attributes
{
    /// <summary>
    /// Control global behaviour of converter 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ProtocolSetupArgument : Attribute
    {
        public int OffsetInByteArray { get; set; }

        public bool UseBigEndian { get; set; }
    }
}