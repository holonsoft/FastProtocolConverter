using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using System;
using System.Net;

namespace holonsoft.FastProtocolConverter.Test.dto
{
	public class AdvancedTypesPoco
	{
		[ProtocolField(StartPos = 0)]
		[ProtocolDateTimeField(DateTimeKind = DateTimeKind.Utc, DateTimeByteFormat = DateTimeByteFormat.UnixTimeStamp32Bit)]
		public DateTime DateTimeField;

		[ProtocolField(StartPos = 4)]
		public Guid GuidField;

		[ProtocolField(StartPos = 20)] 
		[ProtocolBytePadding(Padding=16)]
		public byte Padding = (byte)'#';

		[ProtocolField(StartPos = 36)]
		public byte SomeValue = 255;
	}
}