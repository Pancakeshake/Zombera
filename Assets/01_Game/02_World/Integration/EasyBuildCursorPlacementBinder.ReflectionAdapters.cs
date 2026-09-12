#region

using System;

#endregion

namespace Zombera.BuildingSystem
{
    public sealed partial class EasyBuildCursorPlacementBinder
    {
        private static Type FindType(string fullName)
        {
            return EasyBuildBridgeReflectionHelper.FindType(fullName);
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            return EasyBuildBridgeReflectionHelper.GetMemberValue(instance, memberName);
        }

        private static object GetStaticMemberValue(Type type, string memberName)
        {
            return EasyBuildBridgeReflectionHelper.GetStaticMemberValue(type, memberName);
        }

        private static void SetMemberValue(object instance, string memberName, object value)
        {
            EasyBuildBridgeReflectionHelper.SetMemberValue(instance, memberName, value);
        }

        private static void InvokeMethod(object instance, string methodName, params object[] arguments)
        {
            EasyBuildBridgeReflectionHelper.InvokeMethod(instance, methodName, arguments);
        }

        private static object InvokeMethodWithReturn(object instance, string methodName, params object[] arguments)
        {
            return EasyBuildBridgeReflectionHelper.InvokeMethodWithReturn(instance, methodName, arguments);
        }

        private static int TryReadIntMember(object instance, params string[] memberNames)
        {
            if (instance == null || memberNames == null || memberNames.Length == 0) return -1;

            for (var i = 0; i < memberNames.Length; i++)
            {
                var value = GetMemberValue(instance, memberNames[i]);
                if (value is int index && index >= 0)
                    return index;
            }

            return -1;
        }

        private static bool TryReadBoolMember(object instance, params string[] memberNames)
        {
            if (instance == null || memberNames == null || memberNames.Length == 0) return false;

            for (var i = 0; i < memberNames.Length; i++)
            {
                var value = GetMemberValue(instance, memberNames[i]);
                if (value is bool b && b)
                    return true;
            }

            return false;
        }
    }
}
