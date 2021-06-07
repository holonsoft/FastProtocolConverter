using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using System;

namespace holonsoft.FastProtocolConverter.Test.dto
{
	public class SimplePocoWithGap
	{
		[ProtocolField(StartPos = 0, TypeInByteArray = DestinationType.UInt32)]
		public uint JobNumberIn;
		
		[ProtocolField(StartPos = 4, TypeInByteArray = DestinationType.Byte)]
		public bool Request;

		[ProtocolField(StartPos = 5)]
		[ProtocolBytePadding(Padding = 3)]
		public byte Padding1 = Byte.MaxValue;
		
		[ProtocolField(StartPos = 8, TypeInByteArray = DestinationType.Byte)]
		public bool Start;

		[ProtocolField(StartPos = 9)]
		[ProtocolBytePadding(Padding = 1125-10)]
		public byte Padding2 = Byte.MaxValue;

		[ProtocolField(StartPos = 1124, TypeInByteArray = DestinationType.Int32)]
		public PLCMachineMode MachineMode;
	}
}
