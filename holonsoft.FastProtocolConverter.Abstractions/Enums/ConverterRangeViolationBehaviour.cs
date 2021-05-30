using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Enums
{
    [Flags]
    public enum ConverterRangeViolationBehaviour
    {
        None = 0x00,
        IgnoreAndContinue = 0x01,

        SetToMinValue = IgnoreAndContinue << 1,
        SetToMaxValue = IgnoreAndContinue << 2,
        SetToDefaultValue = IgnoreAndContinue << 3,

        ThrowException = IgnoreAndContinue << 4,
    }
}