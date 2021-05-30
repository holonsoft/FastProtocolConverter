using holonsoft.FastProtocolConverter.Abstractions.Attributes;

namespace holonsoft.FastProtocolConverter.Test.dto
{
    public class PocoWithRanges
    {
        [ProtocolField(StartPos = 0)]
        [ProtocolFieldRange(MinValue = "-2200", MaxValue = "-1200", DefaultValue = "-2000")]
        public int IntField;

        [ProtocolField(StartPos = 4)]
        [ProtocolFieldRange(MinValue = "1100", MaxValue = "2200")]
        public uint UIntField;

        [ProtocolField(StartPos = 8)]
        [ProtocolFieldRange(MinValue = "1100", MaxValue = "2200")]
        public short ShortField;

        [ProtocolField(StartPos = 10)]
        [ProtocolFieldRange(MinValue = "1100", MaxValue = "2200")]
        public ushort UShortField;

        [ProtocolField(StartPos = 12)]
        [ProtocolFieldRange(MinValue = "1.100", MaxValue = "2.200")]
        public float FloatField;

        [ProtocolField(StartPos = 16)]
        [ProtocolFieldRange(MinValue = "3.1415", MaxValue = "6.282")]
        public double DoubleField;

    }
}
