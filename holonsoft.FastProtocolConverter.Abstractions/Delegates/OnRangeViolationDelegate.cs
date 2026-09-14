using holonsoft.FastProtocolConverter.Abstractions.Enums;
using System.Reflection;

namespace holonsoft.FastProtocolConverter.Abstractions.Delegates
{
	/// <summary>
	/// Defines a delegate for handling range check violations
	/// hint: you can define ranges via attribute for a field in a POCO	
	/// </summary>
	/// <remarks>
	/// The first parameter was a FieldInfo before 4.0. It is a MemberInfo now, because a protocol
	/// member can be a property as well as a field. A handler that only reads the name needs no
	/// change beyond the parameter type. One that reads the type has to ask the member for it,
	/// FieldInfo.FieldType resp. PropertyInfo.PropertyType, since MemberInfo carries neither.
	/// </remarks>
	public delegate void OnRangeViolationDelegate(MemberInfo memberInfo, out ConverterRangeViolationBehaviour converterRangeViolationBehaviour);
}