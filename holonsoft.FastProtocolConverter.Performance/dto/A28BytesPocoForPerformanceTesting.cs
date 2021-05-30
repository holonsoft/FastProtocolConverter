// unset

using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using System;

namespace holonsoft.FastProtocolConverter.Performance.dto
{
	public class A28BytesPocoForPerformanceTesting
	{
		[ProtocolField(StartPos = 0)]
		public UInt64 UInt64Field;      // 8 bytes

		[ProtocolField(StartPos = 8)]
		public UInt32 UInt32Field;      // 4 bytes

		[ProtocolField(StartPos = 12)]
		public float FloatField1;       // 4 bytes

		[ProtocolField(StartPos = 16)]
		public float FloatField2;       // 4 bytes

		[ProtocolField(StartPos = 20)]
		public double DoubleField;       // 8 bytes
		//===========================================
		//                                28 bytes
	}
}