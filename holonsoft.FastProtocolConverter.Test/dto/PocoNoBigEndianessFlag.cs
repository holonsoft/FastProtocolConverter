using holonsoft.FastProtocolConverter.Abstractions.Attributes;

namespace holonsoft.FastProtocolConverter.Test.dto
{
    [ProtocolSetupArgument(UseBigEndian = false)] 
    public class PocoNoBigEndianessFlag
    {
        [ProtocolField(StartPos = 0)]
        public int IntField;

        [ProtocolField(StartPos = 4)]
        public uint UIntField;

        [ProtocolField(StartPos = 8)]
        public short ShortField;

        [ProtocolField(StartPos = 10)]
        public ushort UShortField;

        [ProtocolField(StartPos = 12)]
        public float FloatField;

        [ProtocolField(StartPos = 16)]
        public double DoubleField;

        [ProtocolField(StartPos = 24)]
        public long LongField;

        [ProtocolField(StartPos = 32)]
        public ulong ULongField;
    }
}
