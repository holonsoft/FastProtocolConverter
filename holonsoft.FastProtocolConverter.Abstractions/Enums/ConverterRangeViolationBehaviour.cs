using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Enums
{
	/// <summary>
	/// Define the behaviour when a range violation occurs 
	/// </summary>
	[Flags]
	public enum ConverterRangeViolationBehaviour
	{
	    None = 0x00,

			/// <summary>
			/// Ignore the range violation and continue
			/// </summary>
	    IgnoreAndContinue = 0x01,

			/// <summary>
			/// Set to minimum value of type and continue
			/// </summary>
	    SetToMinValue = IgnoreAndContinue << 1,

			/// <summary>
			/// Set to maximum value of type and continue
			/// </summary>
			SetToMaxValue = IgnoreAndContinue << 2,

			/// <summary>
			/// Set to default value of type and continue
			/// </summary>
			SetToDefaultValue = IgnoreAndContinue << 3,

			/// <summary>
			/// Stop processing
			/// </summary>
	    ThrowException = IgnoreAndContinue << 4,
	}
}