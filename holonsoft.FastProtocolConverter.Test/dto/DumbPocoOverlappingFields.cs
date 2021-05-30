using holonsoft.FastProtocolConverter.Abstractions.Attributes;

namespace holonsoft.FastProtocolConverter.Test.dto
{
    public sealed class DumbPocoOverlappingFields
    {
        [ProtocolField(StartPos = 1)] // this leads to an overlapping byte definition => exception
        public int Field1;

        [ProtocolField(StartPos = 0)]
        public short Field2;

        [ProtocolField(StartPos = 7)]
        public byte Field3;
    }
}
