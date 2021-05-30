using System;
using System.Linq.Expressions;
using System.Reflection;

// See:
// https://www.sdx-ag.de/2012/05/c-performance-bei-der-befullungmapping/
// https://stackoverflow.com/questions/17660097/is-it-possible-to-speed-this-method-up/17669142#17669142
// https://stackoverflow.com/questions/6158768/c-sharp-reflection-fastest-way-to-update-a-property-value
// https://www.codeproject.com/Articles/18450/HyperDescriptor-Accelerated-dynamic-property-acces


namespace holonsoft.FastProtocolConverter
{
    public static class FastInvoke
    {
        public static Func<T, TReturn> BuildTypedGetter<T, TReturn>(PropertyInfo propertyInfo)
        {
            Func<T, TReturn> reflGet = (Func<T, TReturn>) Delegate.CreateDelegate(typeof(Func<T, TReturn>), propertyInfo.GetGetMethod());

            return reflGet;
        }

        public static Action<T, TProperty> BuildTypedSetter<T, TProperty>(PropertyInfo propertyInfo)
        {
            Action<T, TProperty> reflSet = (Action<T, TProperty>)Delegate.CreateDelegate(typeof(Action<T, TProperty>), propertyInfo.GetSetMethod());

            return reflSet;
        }


        public static Func<T, object> BuildUntypedGetter<T>(MemberInfo memberInfo)
        {
            var targetType = memberInfo.DeclaringType;
            var exInstance = Expression.Parameter(targetType, "t");

            var exMemberAccess = Expression.MakeMemberAccess(exInstance, memberInfo);       // t.PropertyName
            var exConvertToObject = Expression.Convert(exMemberAccess, typeof(object));     // Convert(t.PropertyName, typeof(object))
            var lambda = Expression.Lambda<Func<T, object>>(exConvertToObject, exInstance);

            var action = lambda.Compile();
            return action;
        }

        public static Action<T, object> BuildUntypedSetter<T>(MemberInfo memberInfo)
        {
            var targetType = memberInfo.DeclaringType;
            var exInstance = Expression.Parameter(targetType, "t");

            var exMemberAccess = Expression.MakeMemberAccess(exInstance, memberInfo);

            // t.PropertValue(Convert(p))
            var exValue = Expression.Parameter(typeof(object), "p");
            var exConvertedValue = Expression.Convert(exValue, GetUnderlyingType(memberInfo));
            var exBody = Expression.Assign(exMemberAccess, exConvertedValue);

            var lambda = Expression.Lambda<Action<T, object>>(exBody, exInstance, exValue);
            var action = lambda.Compile();
            return action;
        }

        private static Type GetUnderlyingType(this MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Event:
                    return ((EventInfo)member).EventHandlerType;
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;
                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;
                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;
                default:
                    throw new ArgumentException
                    (
                     "Input MemberInfo must be if type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"
                    );
            }
        }

    }
}
