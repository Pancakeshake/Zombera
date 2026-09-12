#region

using System;
using System.Reflection;
using UnityEngine;

#endregion

namespace Zombera.Environment
{
    internal sealed class DayNightEnviroBridge
    {
        private static Type _cachedEnviroManagerType;
        private static bool _attemptedEnviroTypeResolve;

        private bool _enviroSimulationDisabled;

        public bool DetectEnviro()
        {
            var enviroManagerType = ResolveEnviroManagerType();
            if (enviroManagerType == null) return false;

            var instanceField = enviroManagerType.GetField("instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var manager = instanceField?.GetValue(null) as MonoBehaviour;
            if (manager == null || !manager.isActiveAndEnabled) return false;

            var configuration = GetMemberValue(enviroManagerType, manager, "configuration") as UnityEngine.Object;
            return configuration != null;
        }

        public bool SyncTime(float currentHour, int dayNumber, bool forceImmediateUpdate)
        {
            if (!TryGetEnviroTimeModule(out var timeModule)) return false;

            // Enviro only updates sky/lighting if these toggles are enabled.
            TryEnableEnviroSkyAndLightingUpdates();

            if (!_enviroSimulationDisabled)
            {
                var settings = GetMemberValue(timeModule, "Settings");
                if (settings != null) SetMemberValue(settings, "simulate", false);
                _enviroSimulationDisabled = true;
            }

            var applied = TrySetEnviroTimeOfDay(timeModule, currentHour);
            if (!applied)
                ApplyEnviroTimeBySettingDateTime(timeModule, currentHour, dayNumber);

            InvokeMethod(timeModule, "UpdateModule");

            if (forceImmediateUpdate && TryGetEnviroManager(out var manager))
                InvokeMethod(manager, "UpdateModules");

            return true;
        }

        public bool TryReadEnviroTime(out float timeOfDay)
        {
            timeOfDay = 0f;
            if (!TryGetEnviroTimeModule(out var timeModule)) return false;

            if (TryInvokeMethodWithResult(timeModule, "GetTimeOfDay", out var directResult) &&
                TryConvertToFloat(directResult, out var directTod))
            {
                timeOfDay = Mathf.Repeat(directTod, 24f);
                return true;
            }

            var settings = GetMemberValue(timeModule, "Settings");
            if (settings == null) return false;

            var todObj = GetMemberValue(settings, "timeOfDay");
            if (todObj is not float tod) return false;

            timeOfDay = Mathf.Repeat(tod, 24f);
            return true;
        }

        private static bool TrySetEnviroTimeOfDay(object timeModule, float hourFloat)
        {
            if (timeModule == null) return false;

            var wrapped = Mathf.Repeat(hourFloat, 24f);
            if (TryInvokeMethod(timeModule, "SetTimeOfDay", wrapped))
                return true;

            return false;
        }

        private static void ApplyEnviroTimeBySettingDateTime(object timeModule, float hourFloat, int dayNumber)
        {
            if (timeModule == null) return;

            var wrappedHour = Mathf.Repeat(hourFloat, 24f);
            var hour = Mathf.FloorToInt(wrappedHour);
            var minute = Mathf.FloorToInt((wrappedHour - hour) * 60f);
            var second = 0;

            var settings = GetMemberValue(timeModule, "Settings");
            if (settings == null) return;

            // Build date from configured Enviro start date plus elapsed game days.
            var baseDay = GetIntMemberValue(settings, "daySerial", 1);
            var baseMonth = GetIntMemberValue(settings, "monthSerial", 1);
            var baseYear = GetIntMemberValue(settings, "yearSerial", 1);
            BuildCalendarDate(baseDay, baseMonth, baseYear, dayNumber, out var day, out var month, out var year);

            InvokeMethod(timeModule, "SetDateTime", second, minute, hour, day, month, year);
        }

        private static void BuildCalendarDate(int baseDay, int baseMonth, int baseYear, int elapsedDayNumber, out int day,
            out int month, out int year)
        {
            var normalizedDay = Mathf.Max(1, baseDay);
            var normalizedMonth = Mathf.Clamp(baseMonth, 1, 12);
            var normalizedYear = Mathf.Max(1, baseYear);
            var extraDays = Mathf.Max(0, elapsedDayNumber - 1);

            while (extraDays > 0)
            {
                var daysInMonth = DateTime.DaysInMonth(Mathf.Clamp(normalizedYear, 1, 9999), normalizedMonth);
                var remainingInMonth = daysInMonth - normalizedDay;

                if (extraDays <= remainingInMonth)
                {
                    normalizedDay += extraDays;
                    extraDays = 0;
                    continue;
                }

                extraDays -= remainingInMonth + 1;
                normalizedDay = 1;
                normalizedMonth++;
                if (normalizedMonth <= 12) continue;

                normalizedMonth = 1;
                normalizedYear++;
            }

            day = normalizedDay;
            month = normalizedMonth;
            year = normalizedYear;
        }

        private static int GetIntMemberValue(object instance, string memberName, int fallback)
        {
            if (instance == null) return fallback;

            var raw = GetMemberValue(instance, memberName);
            if (raw is int i) return i;

            try
            {
                return raw != null ? Convert.ToInt32(raw) : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static void TryEnableEnviroSkyAndLightingUpdates()
        {
            var enviroManagerType = ResolveEnviroManagerType();
            if (enviroManagerType == null) return;

            var instanceField = enviroManagerType.GetField("instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var manager = instanceField?.GetValue(null) as MonoBehaviour;
            if (manager == null) return;

            SetMemberValue(manager, "updateSkyAndLighting", true);
            SetMemberValue(manager, "updateSkyAndLightingHDRP", true);
        }

        private static bool TryConvertToFloat(object value, out float result)
        {
            result = 0f;
            if (value == null) return false;

            if (value is float f)
            {
                result = f;
                return true;
            }

            if (value is int i)
            {
                result = i;
                return true;
            }

            if (value is double d)
            {
                result = (float)d;
                return true;
            }

            try
            {
                result = Convert.ToSingle(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetEnviroManager(out object manager)
        {
            manager = null;
            var enviroManagerType = ResolveEnviroManagerType();
            if (enviroManagerType == null) return false;

            var instanceField = enviroManagerType.GetField("instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            manager = instanceField?.GetValue(null);
            return manager != null;
        }

        private static bool TryGetEnviroTimeModule(out object timeModule)
        {
            timeModule = null;
            var enviroManagerType = ResolveEnviroManagerType();
            if (enviroManagerType == null) return false;

            var instanceField = enviroManagerType.GetField("instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var manager = instanceField?.GetValue(null) as MonoBehaviour;
            if (manager == null || !manager.isActiveAndEnabled) return false;

            var configuration = GetMemberValue(enviroManagerType, manager, "configuration") as UnityEngine.Object;
            if (configuration == null) return false;

            timeModule = GetMemberValue(enviroManagerType, manager, "Time");
            return timeModule != null;
        }

        private static Type ResolveEnviroManagerType()
        {
            if (_cachedEnviroManagerType != null) return _cachedEnviroManagerType;
            if (_attemptedEnviroTypeResolve) return null;

            _attemptedEnviroTypeResolve = true;
            _cachedEnviroManagerType = FindType("Enviro.EnviroManager");
            return _cachedEnviroManagerType;
        }

        private static Type FindType(string fullName)
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

        private static object GetMemberValue(Type type, object instance, string memberName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            var field = type.GetField(memberName, flags);
            if (field != null) return field.GetValue(instance);

            var property = type.GetProperty(memberName, flags);
            return property != null ? property.GetValue(instance) : null;
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            return instance == null ? null : GetMemberValue(instance.GetType(), instance, memberName);
        }

        private static void SetMemberValue(object instance, string memberName, object value)
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
            if (property != null && property.CanWrite)
                property.SetValue(instance, value);
        }

        private static void InvokeMethod(object instance, string methodName, params object[] args)
        {
            if (instance == null) return;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var methods = instance.GetType().GetMethods(flags);

            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != methodName) continue;

                var parameters = method.GetParameters();
                if (!AreParametersCompatible(parameters, args)) continue;

                method.Invoke(instance, args);
                return;
            }
        }

        private static bool TryInvokeMethod(object instance, string methodName, params object[] args)
        {
            if (instance == null) return false;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var methods = instance.GetType().GetMethods(flags);
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != methodName) continue;

                var parameters = method.GetParameters();
                if (!AreParametersCompatible(parameters, args)) continue;

                method.Invoke(instance, args);
                return true;
            }

            return false;
        }

        private static bool TryInvokeMethodWithResult(object instance, string methodName, out object result,
            params object[] args)
        {
            result = null;
            if (instance == null) return false;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var methods = instance.GetType().GetMethods(flags);
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != methodName) continue;

                var parameters = method.GetParameters();
                if (!AreParametersCompatible(parameters, args)) continue;

                result = method.Invoke(instance, args);
                return true;
            }

            return false;
        }

        private static bool AreParametersCompatible(ParameterInfo[] parameters, object[] args)
        {
            if (parameters.Length != args.Length) return false;

            for (var p = 0; p < parameters.Length; p++)
            {
                var arg = args[p];
                if (arg == null) continue;

                if (!parameters[p].ParameterType.IsInstanceOfType(arg))
                    return false;
            }

            return true;
        }
    }
}
