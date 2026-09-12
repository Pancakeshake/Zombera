using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Zombera.Environment
{
    [DisallowMultipleComponent]
    public class EnviroWeatherCycleController : MonoBehaviour
    {
        [SerializeField] private bool disableZoneAutoWeather = true;
        [SerializeField] private bool startOnPlay = true;
        [SerializeField] private float minSecondsBetweenChanges = 180f;
        [SerializeField] private float maxSecondsBetweenChanges = 420f;
        [SerializeField] private bool autoFixPurpleRain = true;
        [SerializeField] private float rainFixRescanIntervalSeconds = 8f;
        [Header("Runtime Audio Safeguards")]
        [SerializeField] private bool suppressEnviroAudioOnPlay = true;
        [SerializeField] private bool keepSuppressingEnviroAudio = true;
        [SerializeField] private float audioSuppressionRescanIntervalSeconds = 4f;
        [SerializeField] private bool logEnviroAudioSuppression = true;

        private float _nextChangeTimer;
        private float _nextRainFixTimer;
        private float _nextAudioSuppressionTimer;
        private bool _enviroAudioSuppressed;
        private static Type _enviroManagerType;
        private static Type _enviroWeatherModuleType;

        private void Start()
        {
            if (!Application.isPlaying) return;

            if (suppressEnviroAudioOnPlay)
            {
                _enviroAudioSuppressed = TrySuppressEnviroAudio();
                _nextAudioSuppressionTimer = Mathf.Max(0.5f, audioSuppressionRescanIntervalSeconds);

                if (_enviroAudioSuppressed && logEnviroAudioSuppression)
                    Debug.Log("[EnviroWeatherCycleController] Suppressed Enviro runtime audio module for this play session.", this);
            }

            if (!startOnPlay) return;

            if (!TryGetEnviroWeatherModule(out var weatherModule)) return;

            if (disableZoneAutoWeather)
                SetBoolMember(weatherModule, "globalAutoWeatherChange", false);

            if (autoFixPurpleRain)
            {
                EnviroRainFixUtility.ApplySceneRainFixes();
                _nextRainFixTimer = Mathf.Max(1f, rainFixRescanIntervalSeconds);
            }

            ScheduleNextChange();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (suppressEnviroAudioOnPlay && keepSuppressingEnviroAudio)
            {
                _nextAudioSuppressionTimer -= Time.deltaTime;
                if (_nextAudioSuppressionTimer <= 0f)
                {
                    var suppressedNow = TrySuppressEnviroAudio();
                    if (!_enviroAudioSuppressed && suppressedNow && logEnviroAudioSuppression)
                        Debug.Log("[EnviroWeatherCycleController] Re-applied Enviro audio suppression after module refresh.", this);

                    _enviroAudioSuppressed |= suppressedNow;
                    _nextAudioSuppressionTimer = Mathf.Max(0.5f, audioSuppressionRescanIntervalSeconds);
                }
            }

            if (!startOnPlay) return;

            if (!TryGetEnviroWeatherModule(out var weatherModule)) return;

            _nextChangeTimer -= Time.deltaTime;
            if (_nextChangeTimer <= 0f)
            {
                ChangeToRandomWeather(weatherModule, autoFixPurpleRain);
                ScheduleNextChange();
            }

            if (!autoFixPurpleRain) return;

            _nextRainFixTimer -= Time.deltaTime;
            if (_nextRainFixTimer > 0f) return;

            EnviroRainFixUtility.ApplySceneRainFixes();
            _nextRainFixTimer = Mathf.Max(1f, rainFixRescanIntervalSeconds);
        }

        private static void ChangeToRandomWeather(object weatherModule, bool applyRainFix)
        {
            var settings = GetMemberValue(weatherModule, "Settings");
            if (settings == null) return;

            var weatherTypesObj = GetMemberValue(settings, "weatherTypes") as IList;
            if (weatherTypesObj == null || weatherTypesObj.Count == 0) return;

            var candidates = new ArrayList();
            for (var i = 0; i < weatherTypesObj.Count; i++)
            {
                var type = weatherTypesObj[i];
                if (type != null) candidates.Add(type);
            }

            if (candidates.Count == 0) return;

            var current = GetMemberValue(weatherModule, "targetWeatherType");
            if (candidates.Count == 1)
            {
                InvokeMethod(weatherModule, "ChangeWeather", candidates[0]);
                if (applyRainFix)
                    EnviroRainFixUtility.ApplySceneRainFixes();
                return;
            }

            var safety = 0;
            object next;
            do
            {
                next = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                safety++;
            } while (next == current && safety < 16);

            InvokeMethod(weatherModule, "ChangeWeather", next);

            if (applyRainFix)
                EnviroRainFixUtility.ApplySceneRainFixes();
        }

        private void ScheduleNextChange()
        {
            var min = Mathf.Max(10f, minSecondsBetweenChanges);
            var max = Mathf.Max(min, maxSecondsBetweenChanges);
            _nextChangeTimer = UnityEngine.Random.Range(min, max);
        }

        private static bool TrySuppressEnviroAudio()
        {
            if (!TryGetEnviroManager(out var manager)) return false;

            var audioModule = GetMemberValue(manager, "Audio");
            if (audioModule == null) return false;

            // Enviro's audio module recreates runtime AudioSource objects; disable it and zero module volumes.
            SetBoolMember(audioModule, "active", false);
            SetFloatMember(audioModule, "ambientVolumeModifier", 0f);
            SetFloatMember(audioModule, "weatherVolumeModifier", 0f);
            SetFloatMember(audioModule, "thunderVolumeModifier", 0f);

            var settings = GetMemberValue(audioModule, "Settings");
            if (settings != null)
            {
                SetFloatMember(settings, "ambientMasterVolume", 0f);
                SetFloatMember(settings, "weatherMasterVolume", 0f);
                SetFloatMember(settings, "thunderMasterVolume", 0f);
            }

            InvokeMethod(audioModule, "Disable");
            return true;
        }

        private static bool TryGetEnviroManager(out object manager)
        {
            manager = null;

            _enviroManagerType ??= FindType("Enviro.EnviroManager");
            if (_enviroManagerType == null) return false;

            manager = GetMemberValue(_enviroManagerType, null, "instance");
            return manager != null;
        }

        private static bool TryGetEnviroWeatherModule(out object weatherModule)
        {
            weatherModule = null;

            if (!TryGetEnviroManager(out var manager)) return false;

            weatherModule = GetMemberValue(manager, "Weather");
            if (weatherModule == null) return false;

            _enviroWeatherModuleType ??= weatherModule.GetType();
            return true;
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

        private static object GetMemberValue(object instance, string memberName)
        {
            return GetMemberValue(instance.GetType(), instance, memberName);
        }

        private static object GetMemberValue(Type type, object instance, string memberName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            var field = type.GetField(memberName, flags);
            if (field != null) return field.GetValue(instance);

            var property = type.GetProperty(memberName, flags);
            return property != null ? property.GetValue(instance) : null;
        }

        private static void SetBoolMember(object instance, string memberName, bool value)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();

            var field = type.GetField(memberName, flags);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(instance, value);
                return;
            }

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
                property.SetValue(instance, value);
        }

        private static void SetFloatMember(object instance, string memberName, float value)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();

            var field = type.GetField(memberName, flags);
            if (field != null && field.FieldType == typeof(float))
            {
                field.SetValue(instance, value);
                return;
            }

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.PropertyType == typeof(float) && property.CanWrite)
                property.SetValue(instance, value);
        }

        private static void InvokeMethod(object instance, string methodName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();
            var method = type.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
            method?.Invoke(instance, null);
        }

        private static void InvokeMethod(object instance, string methodName, object arg)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();
            var argType = arg.GetType();

            var method = type.GetMethod(methodName, flags, null, new[] { argType }, null);
            if (method != null)
            {
                method.Invoke(instance, new[] { arg });
                return;
            }

            // Fallback for overload resolution in case the runtime type comes from a base/derived lookup mismatch.
            var allMethods = type.GetMethods(flags);
            for (var i = 0; i < allMethods.Length; i++)
            {
                var parameters = allMethods[i].GetParameters();
                if (allMethods[i].Name != methodName || parameters.Length != 1) continue;
                if (!parameters[0].ParameterType.IsInstanceOfType(arg)) continue;

                allMethods[i].Invoke(instance, new[] { arg });
                return;
            }
        }
    }
}
