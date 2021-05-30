using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;

namespace holonsoft.FastProtocolConverter.Test.dto
{
    [ProtocolSetupArgument(OffsetInByteArray = 16)] // just for testing purpose
    public class ExamplePocoPayloadMsgType01
	{
        [ProtocolField(SequenceNo = 1, StartPos = 0)]
        public ulong TimeOfDetection;

        [ProtocolField(SequenceNo = 2, StartPos = -1)]
        public double BearAzimuth;

        [ProtocolField(SequenceNo = 3, StartPos = -1)]
        public double BearElevation;

        [ProtocolField(SequenceNo = 4, StartPos = -1)]
        public double BearAzimuthError;

        [ProtocolField(SequenceNo = 5, StartPos = -1)]
        public double BearElevationError;

        [ProtocolField(SequenceNo = 6, StartPos = -1)]
        public double Confidence;

        [ProtocolField(SequenceNo = 7, StartPos = -1)]
        public double LowerFrequency;

        [ProtocolField(SequenceNo = 8, StartPos = -1)]
        public double UpperFrequency;

        [ProtocolField(SequenceNo = 9, StartPos = -1)]
        public double SignalStrength;

        [ProtocolField(SequenceNo = 10, StartPos = -1)]
        public double SignalNoiseRatio;

        [ProtocolField(SequenceNo = 11, TypeInByteArray = DestinationType.Byte, StartPos = -1)]
        public ExamplePocoEnum RFSignalType;

        [ProtocolField(SequenceNo = 12, TypeInByteArray = DestinationType.Byte, StartPos = -1)]
        public ExamplePocoClassificationEnum RFSignalClassification;

        [ProtocolField(SequenceNo = 13, StartPos = -1)]
        public byte ModelNameLength;

        [ProtocolField(SequenceNo = 14, StartPos = -1)]
        public byte SourceIdLength;

        [ProtocolField(SequenceNo = 15, StartPos = -1)]
        public byte DetectionIdLength;

        [ProtocolField(SequenceNo = 16, StartPos = -1)]
        [ProtocolStringField(LengthFieldName = "ModelNameLength", Encoder = SupportedEncoder.ASCIIEncoder)]
        public string ModelName;

        [ProtocolField(SequenceNo = 17, StartPos = -1)]
        [ProtocolStringField(LengthFieldName = "SourceIdLength", Encoder = SupportedEncoder.ASCIIEncoder)]
        public string SourceId;

        [ProtocolField(SequenceNo = 18, StartPos = -1)]
        [ProtocolStringField(LengthFieldName = "DetectionIdLength", Encoder = SupportedEncoder.ASCIIEncoder)]
        public string DetectionId;
    }
}
