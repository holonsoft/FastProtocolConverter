using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;

namespace holonsoft.FastProtocolConverter.Test.dto
{
    public class ComplexProtocolFixedStringLength
    {
        [ProtocolField(StartPos = 0, SequenceNo = 1)]
        public int LengthOfStr1;

        [ProtocolField(StartPos = -1, SequenceNo = 2)]
        public int LengthOfStr2;

        // will be filled to 10 chars (ascii)
        [ProtocolField(StartPos = -1, SequenceNo = 3)]
        [ProtocolStringField(LengthFieldName = "LengthOfStr1", FillupCharWhenShorter = 'Z', StringMaxLengthInByteArray = 10, Encoder = SupportedEncoder.ASCIIEncoder)]
        public string StrField1;

        // will be shortened to 10 chars (ascii)
        [ProtocolField(StartPos = -1, SequenceNo = 4)]
        [ProtocolStringField(LengthFieldName = "LengthOfStr2", StringMaxLengthInByteArray = 10, Encoder = SupportedEncoder.ASCIIEncoder)]
        public string StrField2;
    }
}
