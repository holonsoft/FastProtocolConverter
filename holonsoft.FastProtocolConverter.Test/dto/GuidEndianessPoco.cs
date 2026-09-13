using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using System;

namespace holonsoft.FastProtocolConverter.Test.dto
{
	public class GuidLittleEndianPoco
	{
		[ProtocolField(StartPos = 0)]
		public Guid GuidField;
	}


	[ProtocolSetupArgument(UseBigEndian = true)]
	public class GuidBigEndianPoco
	{
		[ProtocolField(StartPos = 0)]
		public Guid GuidField;
	}
}
