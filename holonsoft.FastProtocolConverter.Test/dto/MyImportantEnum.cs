using System;

namespace holonsoft.FastProtocolConverter.Test.dto
{
    [Flags]
    public enum MyImportantEnum
    {
        None = 0x00,
        A = 0x01,
        B = A << 1,
        C = A << 2,
        D = A << 3,
    }
}
