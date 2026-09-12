using System;
using System.Reflection;
using UnityEngine;
using Zombera.Core;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Enviro
{
    /// <summary>
    /// Enviro sky/weather backend via reflection so World.City never hard-references Enviro.
    /// </summary>
    [AddComponentMenu("Zombera/World/Enviro World Environment Backend")]
    [DisallowMultipleComponent]
    public sealed partial class EnviroWorldEnvironmentBackend : MonoBehaviour,
        IWorldEnvironmentBackend,
        IWorldEnvironmentProfileBinder,
        IWorldWeatherConsumer
    {
        private const string SharedEnviroPrefabPath =
            "Assets/02_Shared/Prefabs/Systems/Environment/Enviro 3.prefab";
        private const string ThirdPartyEnviroPrefabPath =
            "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Enviro 3.prefab";
        private const string DefaultEnviroConfigPath =
            "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Profiles/Configurations/Default Enviro Configuration 3_3_2.asset";

        [SerializeField] private WorldEnvironmentProfile _profile;
        [SerializeField] private GameObject _enviroPrefab;
        [SerializeField] private Transform _root;
        [SerializeField] private bool _logMissingOnce = true;

        private bool _loggedMissing;
        private GameObject _enviroInstance;
        private WorldEnvironmentState _state = new();

        public void BindProfile(WorldEnvironmentProfile environment) =>
            _profile = environment;

        public bool Validate(WorldEnvironmentProfile profile, out string error)
        {
            error = null;
            if (profile == null)
            {
                error = "WorldEnvironmentProfile is required.";
                return false;
            }

            if (!EnsureEnviroManager(out _))
            {
                error = "Enviro manager not found and could not be provisioned.";
                return false;
            }

            EnsureUrpSupportForProfile(profile);

            if (profile.RequireUrpRenderFeature && !TryValidateUrpRenderFeature(out var urpError))
            {
                error = urpError;
                return false;
            }

            return true;
        }

        public void Configure(WorldEnvironmentContext context)
        {
            if (context?.Profile != null)
                _profile = context.Profile;

            if (!EnsureEnviroManager(out var manager))
            {
                LogMissingOnce("Configure");
                return;
            }

            EnsureUrpSupportForProfile(_profile);
            ApplyProfileRefs(manager, _profile);
            ApplyAtmosphereFromProfile(manager, _profile);

            if (!string.IsNullOrEmpty(_state.ActiveWeatherId))
                TrySetWeather(manager, _state.ActiveWeatherId);
            else if (_profile != null)
                TrySetWeather(manager, _profile.StartingWeatherId);

            if (_profile != null)
            {
                _state.TimeOfDayHours = _profile.TimeOfDayHours;
                _state.Season = _profile.Season;
            }

            ApplySunAuthority(manager);
            MarkEnviroSceneDirty(manager);
            Debug.Log($"[EnviroWorldEnvironmentBackend] Configured: {DescribeConfiguredSetup()}", this);
        }

        private static void MarkEnviroSceneDirty(Component manager)
        {
#if UNITY_EDITOR
            if (manager == null) return;
            UnityEditor.EditorUtility.SetDirty(manager.gameObject);
            if (manager.gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
#endif
        }

        public void ApplyWeather(in WorldWeatherSnapshot weather)
        {
            _state.ActiveWeatherId = weather.WeatherId;
            _state.Transition01 = weather.Transition01;
            _state.WindStrength01 = weather.WindStrength01;
            _state.Precipitation01 = weather.Precipitation01;
            _state.TemperatureCelsius = weather.TemperatureCelsius;

            if (!EnsureEnviroManager(out var manager)) return;
            TrySetWeather(manager, weather.WeatherId);
        }

        public void Restore(WorldEnvironmentState state)
        {
            _state = state ?? new WorldEnvironmentState();
            if (!EnsureEnviroManager(out var manager))
            {
                LogMissingOnce("Restore");
                return;
            }

            TrySetWeather(manager, _state.ActiveWeatherId);
            if (_profile != null)
            {
                ApplyTimeAndSeasonFromProfile(manager, _profile);
                ApplyQualityFromProfile(manager, _profile);
            }
            else
            {
                var time = GetMemberValue(manager, "Time");
                TryInvoke(time, "SetTimeOfDay", _state.TimeOfDayHours);
                var environment = GetMemberValue(manager, "Environment");
                TryChangeSeason(environment, _state.Season);
            }

            ApplySunAuthority(manager);
        }

        public WorldEnvironmentState Capture()
        {
            return new WorldEnvironmentState
            {
                ActiveWeatherId = _state.ActiveWeatherId,
                Transition01 = _state.Transition01,
                HoursUntilNextChange = _state.HoursUntilNextChange,
                WindStrength01 = _state.WindStrength01,
                Precipitation01 = _state.Precipitation01,
                TemperatureCelsius = _state.TemperatureCelsius,
                TimeOfDayHours = _state.TimeOfDayHours,
                Season = _state.Season
            };
        }

        public void TearDown()
        {
            DestroyInstance(ref _enviroInstance);
            if (_root == null) return;
            var rootGo = _root.gameObject;
            _root = null;
            DestroyInstance(ref rootGo);
        }

        private void ApplyProfileRefs(Component manager, WorldEnvironmentProfile profile)
        {
            if (manager == null) return;

            var config = profile?.EnviroConfig;
            if (config == null)
                config = LoadDefaultConfig();

            TrySetMember(manager, "configuration", config);
            if (profile?.EnviroZone != null)
            {
                TrySetMember(manager, "defaultZone", profile.EnviroZone);
                TrySetMember(manager, "currentZone", profile.EnviroZone);
            }

            if (profile?.EnviroWeatherPresetLibrary != null)
                TrySetMember(manager, "Weather", profile.EnviroWeatherPresetLibrary);
        }

        /// <summary>
        /// Publishes Enviro's directional as URP/Crest main-light authority.
        /// Allowed in Edit Mode Hub (no GameManager) and world states; blocked in MainMenu.
        /// </summary>
        private void ApplySunAuthority(Component manager)
        {
            if (!CanMutateRenderSettingsSun())
                return;

            var sun = ResolveEnviroDirectionalLight(manager);
            if (sun == null || sun.type != LightType.Directional)
                return;

            RenderSettings.sun = sun;
        }

        private static Light ResolveEnviroDirectionalLight(Component manager)
        {
            if (manager == null)
                return null;

            var objects = GetMemberValue(manager, "Objects");
            if (objects == null)
                return null;

            return GetMemberValue(objects, "directionalLight") as Light;
        }

        private static bool CanMutateRenderSettingsSun()
        {
            var gm = GameManagerGateway.Instance;
            if (gm == null)
                return true;

            return gm.CurrentState != GameState.MainMenu;
        }

        private static UnityEngine.Object LoadDefaultConfig()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadMainAssetAtPath(DefaultEnviroConfigPath);
#else
            return null;
#endif
        }

        private bool EnsureEnviroManager(out Component manager)
        {
            if (TryGetEnviroManager(out manager))
                return true;

            EnsureRoot();
            var prefab = ResolveEnviroPrefab();
            if (prefab == null)
                return false;

            _enviroInstance = Instantiate(prefab, _root);
            _enviroInstance.name = "Enviro 3";
            return TryGetEnviroManager(out manager);
        }

        private GameObject ResolveEnviroPrefab()
        {
            if (_enviroPrefab != null)
                return _enviroPrefab;

#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SharedEnviroPrefabPath)
                         ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ThirdPartyEnviroPrefabPath);
            if (prefab != null)
                _enviroPrefab = prefab;
            return prefab;
#else
            return null;
#endif
        }

        private void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("EnviroRoot");
            go.transform.SetParent(transform, false);
            _root = go.transform;
        }

        private static void DestroyInstance(ref GameObject instance)
        {
            if (instance == null) return;
            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
            instance = null;
        }

        private void LogMissingOnce(string op)
        {
            if (!_logMissingOnce || _loggedMissing) return;
            _loggedMissing = true;
            Debug.LogWarning(
                $"[EnviroWorldEnvironmentBackend] Enviro manager missing during {op}. " +
                "Install Enviro under Assets/03_ThirdParty/Enviro 3 - Sky and Weather or assign an Enviro prefab.",
                this);
        }

        private static bool TryGetEnviroManager(out Component manager)
        {
            manager = null;
            var type = FindType("Enviro.EnviroManager") ??
                       FindType("EnviroManager") ??
                       FindType("Enviro3.EnviroManager");
            if (type == null) return false;

            var instanceField = type.GetField(
                "instance",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            manager = instanceField?.GetValue(null) as Component;
            if (IsSceneObject(manager)) return true;

            manager = FindSceneInstance(type);
            return manager != null;
        }

        private static Component FindSceneInstance(Type type)
        {
            if (type == null) return null;
            var all = Resources.FindObjectsOfTypeAll(type);
            for (var i = 0; i < all.Length; i++)
            {
                var candidate = all[i] as Component;
                if (!IsSceneObject(candidate)) continue;
                return candidate;
            }

            return null;
        }

        private static bool IsSceneObject(Component candidate)
        {
            if (candidate == null) return false;
            var go = candidate.gameObject;
            if (go == null) return false;
            if (!go.scene.IsValid() || !go.scene.isLoaded) return false;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(candidate)) return false;
#endif
            return true;
        }

        private static Type FindType(string name)
        {
            var direct = Type.GetType(name);
            if (direct != null) return direct;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    var type = assemblies[i].GetType(name);
                    if (type != null) return type;
                }
                catch (ReflectionTypeLoadException)
                {
                }
            }

            return null;
        }

        private static object GetMemberValue(object target, string name)
        {
            if (target == null) return null;
            var type = target.GetType();
            try
            {
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                    return prop.GetValue(target);
            }
            catch
            {
                // Some Unity SRP properties throw on get; fall through to fields.
            }

            try
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return field?.GetValue(target);
            }
            catch
            {
                return null;
            }
        }

        private static void TrySetMember(object target, string name, object value)
        {
            if (target == null || value == null) return;
            var type = target.GetType();
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                try { prop.SetValue(target, ConvertMemberValue(prop.PropertyType, value)); }
                catch { /* ignore type mismatch */ }
                return;
            }

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return;
            try { field.SetValue(target, ConvertMemberValue(field.FieldType, value)); }
            catch { /* ignore */ }
        }

        private static object ConvertMemberValue(Type targetType, object value)
        {
            if (value == null || targetType.IsInstanceOfType(value))
                return value;
            try { return Convert.ChangeType(value, targetType); }
            catch { return value; }
        }

        private static bool TryInvoke(object target, string methodName, params object[] args)
        {
            if (target == null) return false;
            var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != methodName) continue;
                var parameters = method.GetParameters();
                if (parameters.Length != args.Length) continue;
                try
                {
                    method.Invoke(target, args);
                    return true;
                }
                catch
                {
                    // try next overload
                }
            }

            return false;
        }
    }
}
