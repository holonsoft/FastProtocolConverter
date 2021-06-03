using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;

namespace holonsoft.FastProtocolConverter.Test.dto
{
    
    public class ComplexProtocol
    {
        [ProtocolField(StartPos = 0, SequenceNo = 1)]
        public int LengthOfStr1;

        [ProtocolField(StartPos = -1, SequenceNo = 2)]
        [ProtocolStringField(LengthFieldName = "LengthOfStr1")]
        public string String1;

        //--------
        // It's allowed to change position in class as long as unique sequence number is provided
        // for string fields counts: length field *must* have a lower sequence number than string field

        [ProtocolField(StartPos = -1, SequenceNo = 4)]
        [ProtocolStringField(LengthFieldName = "LengthOfStr2")]
        public string String2;

        // Please note that the length that was read depends on encoding. If source is ASCII the length is half of .net string (UTF16/Unicode)
        [ProtocolField(StartPos = -1, SequenceNo = 3)]
        public int LengthOfStr2;

        [ProtocolField(StartPos = -1, SequenceNo = 6)]
        public short ShortField;

        [ProtocolField(StartPos = -1, SequenceNo = 5)]
        public int IntField;

        [ProtocolField(StartPos = -1, SequenceNo = 7)]
        public byte ByteField;

        [ProtocolField(StartPos = -1, SequenceNo = 8, TypeInByteArray = DestinationType.Int32)]
        public MyImportantEnum EnumField1;

        [ProtocolField(StartPos = -1, SequenceNo = 9, TypeInByteArray = DestinationType.Int16)]
        public MyImportantEnum EnumField2;

        [ProtocolField(StartPos = -1, SequenceNo = 10, TypeInByteArray = DestinationType.Byte)]
        public MyImportantEnum EnumField3;
        
        [ProtocolField(StartPos = -1, SequenceNo = 11)]
        public float FloatField;

        [ProtocolField(StartPos = -1, SequenceNo = 12)]
        public double DoubleField;

        [ProtocolField(IgnoreField = true)]
        public string MyUnimportantField;

        [ProtocolField(StartPos = -1, SequenceNo = 13)]
        public uint UIntField;

        [ProtocolField(StartPos = -1, SequenceNo = 14)]
        public ushort UShortField;

				// define only ONE field (1 bye in source byte array) to be parsed and call handler for splitting it
				[ProtocolField(StartPos = -1, SequenceNo = 15, TypeInByteArray = DestinationType.Bits)]
        public bool Bit0;

        [ProtocolField(IgnoreField = true)]
				public bool Bit2;

				[ProtocolField(IgnoreField = true)]
				public bool Bit4;
	}
}

