using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;

namespace holonsoft.FastProtocolConverter.Test.dto
{
    /// <summary>
    /// A simple protocol definition
    /// Does not contain strings, therefore we can provide a fix start position for every field
    /// </summary>
    [ProtocolSetupArgument(OffsetInByteArray = 4)]
    public sealed class DumbPoco
    {
        [ProtocolField(StartPos = 2)]
        public int IntField;

        [ProtocolField(StartPos = 0)]
        public short ShortField;

        [ProtocolField(StartPos = 6)]
        public byte ByteField;

        [ProtocolField(StartPos = 7, TypeInByteArray = DestinationType.Int32)]
        public MyImportantEnum EnumField1;

        [ProtocolField(StartPos = 11, TypeInByteArray = DestinationType.Int16)]
        public MyImportantEnum EnumField2;

        [ProtocolField(StartPos = 13, TypeInByteArray = DestinationType.Byte)]
        public MyImportantEnum EnumField3;

        [ProtocolField(StartPos = 14)]
        public float FloatField;

        [ProtocolField(StartPos = 18)]
        public double DoubleField;

        [ProtocolField(StartPos = 26)]
        public uint UIntField;

        [ProtocolField(StartPos = 30)]
        public ushort UShortField;

        [ProtocolField(IgnoreField = true)]
        public string MyUnimportantField;
    }
}
