using holonsoft.FastProtocolConverter.Abstractions.Enums;
using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Attributes
{
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class ProtocolDateTimeFieldAttribute: Attribute
	{
		public DateTimeKind DateTimeKind { get; set; }
		public DateTimeByteFormat DateTimeByteFormat { get; set; }
	}
}