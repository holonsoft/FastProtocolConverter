using holonsoft.FastProtocolConverter.Abstractions.Attributes;

namespace holonsoft.FastProtocolConverter.Test.dto
{
    /// <summary>
    /// Header of each message to determine the payload
    /// </summary>
    public class ExamplePocoHeader
    {
        [ProtocolField(StartPos = 0)]
        public ushort MsgTypeId;        // 2 bytes

        [ProtocolField(StartPos = 2)]
        public byte MsgVersionMajor;    // 1 byte

        [ProtocolField(StartPos = 3)]
        public byte MsgVersionMinor;    // 1 byte

        [ProtocolField(StartPos = 4)]
        public uint PayloadLength;      // 4 byte

        [ProtocolField(StartPos = 8)]
        public ulong MsgTimestamp;      // 8 byte
                                        //==========
                                        // 16 bytes
    }
}
