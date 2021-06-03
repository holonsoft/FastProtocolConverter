using holonsoft.FastProtocolConverter.Abstractions.Enums;
using System.Reflection;

namespace holonsoft.FastProtocolConverter.Abstractions.Delegates
{
	/// <summary>
	/// Defines a delegate for handling range check violations
	/// hint: you can define ranges via attribute for a field in a POCO	
	/// </summary>
	public delegate void OnRangeViolationDelegate(FieldInfo fieldInfo, out ConverterRangeViolationBehaviour converterRangeViolationBehaviour);
}