using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Attributes
{
    /// <summary>
    /// Up to now (full 4.7.2 / core 3.1.1.) it's not possible to have generic attributes
    /// Maybe C# 8 will implement this:
    /// https://github.com/dotnet/roslyn/blob/master/docs/Language%20Feature%20Status.md
    /// At the moment we have to use the universal representation of all datatypes ... known as string :-)
    /// The type of range values will be determined by field the attribute is used for.
    /// 
    /// Ranges are not supported for STRING and ENUM fields
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ProtocolFieldRangeAttribute : Attribute
    {
        public string MinValue { get; set; }
        public string MaxValue { get; set; }

        public string DefaultValue { get; set; }
    }
}