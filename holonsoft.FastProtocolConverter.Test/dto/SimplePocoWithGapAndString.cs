using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;

namespace holonsoft.FastProtocolConverter.Test.dto
{
	public class SimplePocoWithGapAndString
	{
		[ProtocolField(StartPos = 0, TypeInByteArray = DestinationType.UInt32)]
		public uint JobId;
		

		[ProtocolField(StartPos = 4, TypeInByteArray = DestinationType.Byte)]
		public bool IsRequest;
		
		[ProtocolField(StartPos = 5, TypeInByteArray = DestinationType.Byte)]
		public bool IsStart;
		
		[ProtocolField(StartPos = 6)]
		[ProtocolBytePadding(Padding = 10)]
		public byte Padding1 = 0;

		[ProtocolField(StartPos = 16, TypeInByteArray = DestinationType.None)]
		[ProtocolStringField(FillupCharWhenShorter = '\0', StringMaxLengthInByteArray = 256, Encoder = SupportedEncoder.ASCIIEncoder)]
		public string SomeImportantCode;
	}
}