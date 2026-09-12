using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Enviro
{
    /// <summary>
    /// Time / season / quality / world-anchor / URP validation for the survival-world Enviro setup.
    /// </summary>
    public sealed partial class EnviroWorldEnvironmentBackend
    {
        private const string HighQualityPath =
            "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Profiles/Quality/High.asset";
        private const string MediumQualityPath =
            "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Profiles/Quality/Medium.asset";
        private const string QualityProfilesFolder =
            "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Profiles/Quality";

        private void ApplyTimeAndSeasonFromProfile(Component manager, WorldEnvironmentProfile profile)
        {
            if (manager == null || profile == null) return;

            var time = GetMemberValue(manager, "Time");
            if (time != null)
            {
                var settings = GetMemberValue(time, "Settings");
                if (settings != null)
                {
                    TrySetMember(settings, "simulate", profile.SimulateTime);
                    TrySetMember(settings, "cycleLengthInMinutes",
                        Mathf.Max(1f, profile.CycleLengthInMinutes));
                }

                TryInvoke(time, "SetTimeOfDay", profile.TimeOfDayHours);
                _state.TimeOfDayHours = profile.TimeOfDayHours;
            }

            var environment = GetMemberValue(manager, "Environment");
            if (environment == null) return;

            var envSettings = GetMemberValue(environment, "Settings");
            if (envSettings != null)
                TrySetMember(envSettings, "changeSeason", profile.AutoChangeSeason);

            TryChangeSeason(environment, profile.Season);
            _state.Season = profile.Season;
        }

        private void ApplyQualityFromProfile(Component manager, WorldEnvironmentProfile profile)
        {
            if (manager == null || profile == null) return;

            var qualityModule = GetMemberValue(manager, "Quality");
            if (qualityModule == null) return;

            var qualityAsset = ResolveQualityAsset(profile);
            if (qualityAsset == null) return;

            var settings = GetMemberValue(qualityModule, "Settings");
            if (settings == null) return;

            TrySetMember(settings, "defaultQuality", qualityAsset);
            TryAddQualityToList(settings, qualityAsset);
        }

        private void ApplyWorldAnchorFromProfile(Component manager, WorldEnvironmentProfile profile)
        {
            if (manager == null || profile == null || !profile.BindWorldAnchor) return;

            EnsureRoot();
            var objects = GetMemberValue(manager, "Objects");
            if (objects == null) return;

            var existing = GetMemberValue(objects, "worldAnchor") as GameObject;
            if (existing != null) return;

            TrySetMember(objects, "worldAnchor", _root.gameObject);
        }

        private static void TryChangeSeason(object environmentModule, WorldEnvironmentSeason season)
        {
            if (environmentModule == null) return;

            var seasonEnumType = FindType("Enviro.EnviroEnvironment+Seasons")
                                 ?? FindNestedType(FindType("Enviro.EnviroEnvironment"), "Seasons");
            if (seasonEnumType == null)
            {
                var settings = GetMemberValue(environmentModule, "Settings");
                TrySetMember(settings, "season", (int)season);
                return;
            }

            if (!Enum.IsDefined(seasonEnumType, season.ToString()))
                return;

            var enumValue = Enum.Parse(seasonEnumType, season.ToString());
            if (TryInvoke(environmentModule, "ChangeSeason", enumValue))
                return;

            var settingsFallback = GetMemberValue(environmentModule, "Settings");
            TrySetMember(settingsFallback, "season", enumValue);
        }

        private static Type FindNestedType(Type parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name)) return null;
            return parent.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static UnityEngine.Object ResolveQualityAsset(WorldEnvironmentProfile profile)
        {
            if (profile.EnviroQuality != null)
                return profile.EnviroQuality;

#if UNITY_EDITOR
            var byName = TryLoadQualityByName(profile.EnviroQualityName);
            if (byName != null) return byName;

            return UnityEditor.AssetDatabase.LoadMainAssetAtPath(HighQualityPath)
                   ?? UnityEditor.AssetDatabase.LoadMainAssetAtPath(MediumQualityPath);
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        private static UnityEngine.Object TryLoadQualityByName(string qualityName)
        {
            if (string.IsNullOrWhiteSpace(qualityName)) return null;
            var path = $"{QualityProfilesFolder}/{qualityName.Trim()}.asset";
            return UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
        }
#endif

        private static void TryAddQualityToList(object qualitySettings, UnityEngine.Object qualityAsset)
        {
            if (qualitySettings == null || qualityAsset == null) return;

            var listObj = GetMemberValue(qualitySettings, "Qualities");
            if (listObj is not System.Collections.IList list) return;

            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], qualityAsset))
                    return;
            }

            try { list.Add(qualityAsset); }
            catch { /* ignore incompatible list element type */ }
        }

        private static bool TryValidateUrpRenderFeature(out string error)
        {
            error = null;
#if !UNITY_EDITOR
            return true;
#else
            try
            {
                var pipeline = GraphicsSettings.currentRenderPipeline;
                if (pipeline == null)
                {
                    error = "No SRP asset assigned; Enviro URP feature cannot be verified.";
                    return false;
                }

                var pipelineTypeName = pipeline.GetType().Name;
                if (pipelineTypeName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) < 0)
                    return true;

                if (UrpRendererHasEnviroFeature(pipeline))
                    return true;

                error =
                    "Enviro URP Render Feature not found on the active URP renderer. " +
                    "Add it to every Quality renderer and enable Depth Texture for Scene/Game fog+clouds.";
                return false;
            }
            catch (Exception ex)
            {
                error = "URP feature validation failed: " + ex.Message;
                return false;
            }
#endif
        }

#if UNITY_EDITOR
        private static bool UrpRendererHasEnviroFeature(UnityEngine.Object pipelineAsset)
        {
            if (pipelineAsset == null) return false;

            // Prefer SerializedObject — URP private property getters can throw via reflection.
            var so = new UnityEditor.SerializedObject(pipelineAsset);
            var listProp = so.FindProperty("m_RendererDataList");
            if (listProp != null && listProp.isArray)
            {
                for (var i = 0; i < listProp.arraySize; i++)
                {
                    var renderer = listProp.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (renderer != null && RendererDataHasEnviroFeature(renderer))
                        return true;
                }

                return false;
            }

            var rendererList = SafeGetMemberValue(pipelineAsset, "m_RendererDataList")
                               ?? SafeGetMemberValue(pipelineAsset, "rendererDataList");
            if (rendererList is not Array array) return false;

            for (var i = 0; i < array.Length; i++)
            {
                var rendererData = array.GetValue(i);
                if (rendererData == null) continue;
                if (RendererDataHasEnviroFeature(rendererData as UnityEngine.Object ?? rendererData))
                    return true;
            }

            return false;
        }

        private static bool RendererDataHasEnviroFeature(object rendererData)
        {
            if (rendererData is UnityEngine.Object unityObj)
            {
                var so = new UnityEditor.SerializedObject(unityObj);
                var featuresProp = so.FindProperty("m_RendererFeatures");
                if (featuresProp != null && featuresProp.isArray)
                {
                    for (var i = 0; i < featuresProp.arraySize; i++)
                    {
                        var feature = featuresProp.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (feature == null) continue;
                        if (feature.GetType().Name.IndexOf("Enviro", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }

                    return false;
                }
            }

            var features = SafeGetMemberValue(rendererData, "m_RendererFeatures")
                           ?? SafeGetMemberValue(rendererData, "rendererFeatures");
            if (features is not System.Collections.IEnumerable enumerable) return false;

            foreach (var feature in enumerable)
            {
                if (feature == null) continue;
                var typeName = feature.GetType().Name;
                if (typeName.IndexOf("Enviro", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static object SafeGetMemberValue(object target, string name)
        {
            try { return GetMemberValue(target, name); }
            catch { return null; }
        }
#endif

        /// <summary>One-line status for hub/tests after Configure.</summary>
        public string DescribeConfiguredSetup()
        {
            if (!TryGetEnviroManager(out var manager) || manager == null)
                return "Enviro manager: missing";

            var weather = GetMemberValue(manager, "Weather");
            var target = GetMemberValue(weather, "targetWeatherType");
            var weatherName = target != null ? target.ToString() : _state.ActiveWeatherId;

            var time = GetMemberValue(manager, "Time");
            var tod = time != null && TryInvokeFloat(time, "GetTimeOfDay", out var hours)
                ? hours
                : _state.TimeOfDayHours;

            var fog = GetMemberValue(manager, "Fog");
            var fogOn = fog != null && IsTruthy(GetMemberValue(fog, "active"));
            var clouds = GetMemberValue(manager, "VolumetricClouds");
            var cloudsOn = clouds != null && IsTruthy(GetMemberValue(clouds, "active"));

            var quality = GetMemberValue(GetMemberValue(manager, "Quality"), "Settings");
            var defaultQ = GetMemberValue(quality, "defaultQuality");
            var qualityName = defaultQ != null ? defaultQ.ToString() : "unset";

            var objects = GetMemberValue(manager, "Objects");
            var anchor = GetMemberValue(objects, "worldAnchor") as GameObject;
            var sun = RenderSettings.sun != null ? RenderSettings.sun.name : "null";

            return
                $"weather={weatherName}; tod={tod:0.##}; season={_state.Season}; " +
                $"fog={fogOn}; volumetricClouds={cloudsOn}; quality={qualityName}; " +
                $"worldAnchor={(anchor != null ? anchor.name : "null")}; sun={sun}";
        }

        private static bool TryInvokeFloat(object target, string methodName, out float value)
        {
            value = 0f;
            if (target == null) return false;
            var method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (method == null || method.GetParameters().Length != 0) return false;
            try
            {
                var result = method.Invoke(target, null);
                if (result is float f)
                {
                    value = f;
                    return true;
                }

                if (result is double d)
                {
                    value = (float)d;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool IsTruthy(object value)
        {
            if (value is bool b) return b;
            return value != null;
        }
    }
}
