#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Editor.StyleMatch
{
        internal static class StyleMatchPainterMutations
        {
            public static bool AddFloat(string fieldName, float delta, float min, float max)
            {
                var painter = Object.FindFirstObjectByType<WorldSurfacePainter>();
                if (painter == null)
                    return false;

                var so = new SerializedObject(painter);
                var prop = so.FindProperty(fieldName);
                if (prop == null)
                    return false;

                prop.floatValue = Mathf.Clamp(prop.floatValue + delta, min, max);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(painter);
                return true;
            }

            public static bool SetFloatClamped(string fieldName, float value, float min, float max) =>
                SetFloat(fieldName, value, min, max);

            public static bool SetFloat(string fieldName, float value, float min, float max)
            {
                var painter = Object.FindFirstObjectByType<WorldSurfacePainter>();
                if (painter == null)
                    return false;

                var so = new SerializedObject(painter);
                var prop = so.FindProperty(fieldName);
                if (prop == null)
                    return false;

                prop.floatValue = Mathf.Clamp(value, min, max);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(painter);
                return true;
            }

            /// <summary>Rock band below snow band — tuned for ~1200–1400m realized WorldBuilder peaks.</summary>
            public static bool ApplyMountainElevationBandDefaults()
            {
                var ok = SetFloat("_rockMinElevationMeters", 350f, 250f, 500f);
                ok &= SetFloat("_rockFullElevationMeters", 550f, 450f, 700f);
                ok &= SetFloat("_snowMinElevationMeters", 500f, 400f, 700f);
                ok &= SetFloat("_snowFullElevationMeters", 750f, 600f, 950f);
                ok &= SetFloat("_peakExclusiveMinElevationMeters", 780f, 700f, 900f);
                ok &= SetFloat("_peakExclusiveFullElevationMeters", 980f, 850f, 1200f);
                ok &= SetFloat("_cliffSnowMaxWeight", 0.88f, 0.55f, 0.95f);
                return ok;
            }
        }

    internal static class StyleMatchProfileMutations
    {
        private const string EnvProfilePath =
            "Assets/02_Shared/ScriptableObjects/World/Profiles/WorldEnvironmentProfile.asset";

        private const string NatureProfilePath =
            "Assets/02_Shared/ScriptableObjects/World/Profiles/WorldNatureProfile.asset";

        private const string EnviroConfigPath =
            "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Profiles/Configurations/Default Enviro Configuration 3_3_2.asset";

        public static bool WireEnvironmentProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<WorldEnvironmentProfile>(EnvProfilePath);
            var config = AssetDatabase.LoadAssetAtPath<Object>(EnviroConfigPath);
            if (profile == null)
                return false;

            var so = new SerializedObject(profile);
            if (config != null)
                so.FindProperty("enviroConfig").objectReferenceValue = config;
            so.FindProperty("startingWeatherId").stringValue = "Clear";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static bool ScaleNatureDensity(float multiplier)
        {
            var profile = AssetDatabase.LoadAssetAtPath<WorldNatureProfile>(NatureProfilePath);
            if (profile == null)
                return false;

            var so = new SerializedObject(profile);
            var entries = so.FindProperty("entries");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                var density = e.FindPropertyRelative("DensityPerKm2");
                density.floatValue = Mathf.Clamp(density.floatValue * multiplier, 0f, 500f);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static bool BoostBiomeNatureDensity(string[] biomeIds, float multiplier)
        {
            var profile = AssetDatabase.LoadAssetAtPath<WorldNatureProfile>(NatureProfilePath);
            if (profile == null)
                return false;

            var so = new SerializedObject(profile);
            var entries = so.FindProperty("entries");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                if (!EntryMatchesBiome(e, biomeIds))
                    continue;

                var density = e.FindPropertyRelative("DensityPerKm2");
                density.floatValue = Mathf.Clamp(density.floatValue * multiplier, 0f, 500f);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool EntryMatchesBiome(SerializedProperty entry, string[] biomeIds)
        {
            var arr = entry.FindPropertyRelative("AllowedBiomeIds");
            for (var i = 0; i < arr.arraySize; i++)
            {
                var id = arr.GetArrayElementAtIndex(i).stringValue;
                for (var b = 0; b < biomeIds.Length; b++)
                {
                    if (id == biomeIds[b])
                        return true;
                }
            }

            return false;
        }
    }

    internal static class StyleMatchEnviroSetup
    {
        private const string EnviroPrefabPath =
            "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Enviro 3.prefab";

        private const string EnviroConfigPath =
            "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Profiles/Configurations/Default Enviro Configuration 3_3_2.asset";

        public static bool EnsureInActiveScene()
        {
            if (EnsureEnviroMinimal())
                return true;

            var toolType = typeof(Enviro3WeatherSetupTool);
            var method = toolType.GetMethod(
                "SetupEnviroWeatherInActiveScene",
                BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                return false;

            try
            {
                method.Invoke(null, null);
            }
            catch
            {
                return EnsureEnviroMinimal();
            }

            return true;
        }

        public static bool SetClearMidday()
        {
            var managerType = FindType("Enviro.EnviroManager");
            if (managerType == null)
                return false;

            var managers = Object.FindObjectsByType(managerType, FindObjectsSortMode.None);
            if (managers == null || managers.Length == 0)
                return EnsureInActiveScene();

            var manager = managers[0];
            SetMember(manager, "timeOfDay", 12f);
            SetMember(manager, "solarTime", 0.5f);
            TrySetClearWeather(manager);
            ApplyValleyHaze();
            TryInvoke(manager, "UpdateAllModules");
            return true;
        }

        private static void ApplyValleyHaze()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.78f, 0.84f, 0.9f);
            RenderSettings.fogDensity = 0.0014f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.8f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.32f, 0.28f);
        }

        private static void TrySetClearWeather(object manager)
        {
            var weather = GetMember(manager, "Weather") ?? GetMember(manager, "WeatherModule");
            if (weather == null)
                return;

            if (TryInvoke(weather, "ChangeWeather", "Clear"))
                return;

            TryInvoke(weather, "SetWeatherOverride", "Clear");
        }

        private static object GetMember(object target, string name)
        {
            if (target == null)
                return null;

            var type = target.GetType();
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(target);

            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return prop?.GetValue(target);
        }

        private static bool TryInvoke(object target, string methodName, params object[] args)
        {
            if (target == null)
                return false;

            var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != methodName)
                    continue;
                if (method.GetParameters().Length != args.Length)
                    continue;

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

        private static bool EnsureEnviroMinimal()
        {
            var managerType = FindType("Enviro.EnviroManager");
            if (managerType == null)
                return false;

            var existing = Object.FindFirstObjectByType(managerType);
            if (existing != null)
                return true;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnviroPrefabPath);
            if (prefab == null)
                return false;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Enviro 3";
            var config = AssetDatabase.LoadMainAssetAtPath(EnviroConfigPath);
            if (config != null)
                SetMember(instance.GetComponent(managerType), "configuration", config);

            return true;
        }

        private static System.Type FindType(string name)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var t = assemblies[i].GetType(name);
                if (t != null)
                    return t;
            }

            return null;
        }

        private static void SetMember(object target, string name, object value)
        {
            if (target == null)
                return;

            var type = target.GetType();
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            prop?.SetValue(target, value);
        }
    }
}
#endif
