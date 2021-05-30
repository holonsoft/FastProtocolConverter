using holonsoft.FastProtocolConverter.Abstractions.Enums;
using System.Reflection;

namespace holonsoft.FastProtocolConverter.Abstractions.Delegates
{
    public delegate void OnRangeViolationDelegate(FieldInfo FieldInfo, out ConverterRangeViolationBehaviour converterRangeViolationBehaviour);
}