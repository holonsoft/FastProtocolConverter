using holonsoft.FastProtocolConverter.Abstractions.Attributes;

namespace holonsoft.FastProtocolConverter.Test.dto
{
    public sealed class DumbPocoDoublePositionFields
    {
        [ProtocolField(StartPos = 0)] 
        public int Field1;

        [ProtocolField(StartPos = 0)] // this leads to an overlapping byte definition => exception
        public short Field2;

        [ProtocolField(StartPos = 7)]
        public byte Field3;

    }
}
