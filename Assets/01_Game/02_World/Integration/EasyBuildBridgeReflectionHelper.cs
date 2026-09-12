using System;
using System.Reflection;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeReflectionHelper
    {
        internal static Type FindType(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type != null) return type;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName);
                if (type != null) return type;
            }

            return null;
        }

        internal static bool? GetBoolMemberValue(object instance, string memberName)
        {
            if (instance == null) return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();

            var field = type.GetField(memberName, flags);
            if (field != null && field.GetValue(instance) is bool fieldValue)
                return fieldValue;

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.GetValue(instance) is bool propertyValue)
                return propertyValue;

            return null;
        }

        internal static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null) return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();

            var field = type.GetField(memberName, flags);
            if (field != null) return field.GetValue(instance);

            var property = type.GetProperty(memberName, flags);
            return property?.GetValue(instance);
        }

        internal static void SetMemberValue(object instance, string memberName, object value)
        {
            if (instance == null) return;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();

            var field = type.GetField(memberName, flags);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }

            var property = type.GetProperty(memberName, flags);
            if (property == null || !property.CanWrite) return;

            property.SetValue(instance, value);
        }

        internal static object GetStaticMemberValue(Type type, string memberName)
        {
            if (type == null) return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var field = type.GetField(memberName, flags);
            if (field != null) return field.GetValue(null);

            var property = type.GetProperty(memberName, flags);
            return property?.GetValue(null);
        }

        internal static bool TryInvokeMethodExact(object instance, string methodName, params object[] arguments)
        {
            if (instance == null || string.IsNullOrWhiteSpace(methodName)) return false;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();
            var argumentTypes = new Type[arguments.Length];
            for (var i = 0; i < arguments.Length; i++) argumentTypes[i] = arguments[i]?.GetType();

            var method = type.GetMethod(methodName, flags, null, argumentTypes, null);
            if (method == null) return false;

            try
            {
                method.Invoke(instance, arguments);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void InvokeMethod(object instance, string methodName, params object[] arguments)
        {
            if (instance == null) return;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();
            var argumentTypes = new Type[arguments.Length];
            for (var i = 0; i < arguments.Length; i++) argumentTypes[i] = arguments[i]?.GetType();

            var method = type.GetMethod(methodName, flags, null, argumentTypes, null)
                         ?? type.GetMethod(methodName, flags);
            method?.Invoke(instance, arguments);
        }

        internal static object InvokeMethodWithReturn(object instance, string methodName, params object[] arguments)
        {
            if (instance == null) return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();
            var argumentTypes = new Type[arguments.Length];
            for (var i = 0; i < arguments.Length; i++) argumentTypes[i] = arguments[i]?.GetType();

            var method = type.GetMethod(methodName, flags, null, argumentTypes, null)
                         ?? type.GetMethod(methodName, flags);
            return method?.Invoke(instance, arguments);
        }
    }
}
