using System;
using System.Reflection;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Enviro
{
    /// <summary>
    /// Camera bind + Fog / VolumetricClouds / FlatClouds application from WorldEnvironmentProfile.
    /// </summary>
    public sealed partial class EnviroWorldEnvironmentBackend
    {
        private static readonly string[] AtmosphereModuleNames =
        {
            "Time",
            "Lighting",
            "Sky",
            "Fog",
            "VolumetricClouds",
            "FlatClouds",
            "Weather",
            "Environment",
            "Quality"
        };

        private void ApplyAtmosphereFromProfile(Component manager, WorldEnvironmentProfile profile)
        {
            if (manager == null || profile == null) return;

            if (profile.BindGameplayCamera)
                EnsureGameplayCamera(manager);

            EnsureAtmosphereModules(manager);
            TryInvoke(manager, "LoadConfiguration");
            TryInvoke(manager, "LoadAllModules");

            ApplyFogSettings(manager, profile);
            ApplyVolumetricCloudSettings(manager, profile);
            ApplyFlatCloudSettings(manager, profile);
            ApplyTimeAndSeasonFromProfile(manager, profile);
            ApplyQualityFromProfile(manager, profile);
            ApplyWorldAnchorFromProfile(manager, profile);
        }

        private static void EnsureGameplayCamera(Component manager)
        {
            if (GetMemberValue(manager, "Camera") is Camera existing && existing != null)
                return;

            var cam = Camera.main;
            if (cam == null)
                cam = FindFirstGameCamera();

            if (cam == null) return;
            TryInvoke(manager, "ChangeCamera", cam);
        }

        private static Camera FindFirstGameCamera()
        {
            var cameras = Resources.FindObjectsOfTypeAll<Camera>();
            for (var i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || !cam.enabled) continue;
                if (!cam.gameObject.scene.IsValid()) continue;
                if (cam.cameraType != CameraType.Game) continue;
                return cam;
            }

            return null;
        }

        private static void EnsureAtmosphereModules(Component manager)
        {
            var moduleTypeEnum = FindType("Enviro.EnviroManagerBase+ModuleType");
            if (moduleTypeEnum == null) return;

            for (var i = 0; i < AtmosphereModuleNames.Length; i++)
            {
                var name = AtmosphereModuleNames[i];
                if (GetMemberValue(manager, name) != null) continue;
                if (!Enum.IsDefined(moduleTypeEnum, name)) continue;
                var enumValue = Enum.Parse(moduleTypeEnum, name);
                TryInvoke(manager, "AddModule", enumValue);
            }
        }

        private static void ApplyFogSettings(Component manager, WorldEnvironmentProfile profile)
        {
            var fog = GetMemberValue(manager, "Fog");
            if (fog == null) return;

            // The hub suppresses fog while it generates (see EnviroFogOverride): this apply runs from
            // the Environment stages mid-run, so without honouring the flag it would put the authored
            // density straight back and hide the terrain being built.
            var suppress = EnviroFogOverride.SuppressFog;
            TrySetMember(fog, "active", !suppress && profile.EnableFog);
            var settings = GetMemberValue(fog, "Settings");
            if (settings == null) return;

            TrySetMember(settings, "fog", !suppress && profile.EnableFog);
            TrySetMember(settings, "volumetrics", !suppress && profile.EnableVolumetricFog);
            TrySetMember(settings, "fogDensity", suppress ? 0f : profile.FogDensity);
            TrySetMember(settings, "fogHeightFalloff", profile.FogHeightFalloff);
            TrySetMember(settings, "fogHeight", profile.FogHeightWorldY);
            TrySetMember(settings, "globalFogHeight", profile.FogHeightWorldY);

            // The profile drives only the first fog layer's density, so suppression has to clear what
            // it does not own: Enviro renders a second layer (fogDensity2) and scales the whole effect
            // by fogMaxOpacity (default 1 = fully opaque). Leaving those behind kept the scene foggy
            // even with the authored density at zero.
            if (!suppress)
                return;

            TrySetMember(settings, "fogDensity2", 0f);
            TrySetMember(settings, "fogMaxOpacity", 0f);
        }

        private static void ApplyVolumetricCloudSettings(Component manager, WorldEnvironmentProfile profile)
        {
            var clouds = GetMemberValue(manager, "VolumetricClouds");
            if (clouds == null) return;

            TrySetMember(clouds, "active", profile.EnableVolumetricClouds);

            var quality = GetMemberValue(clouds, "settingsQuality");
            if (quality != null)
                TrySetMember(quality, "volumetricClouds", profile.EnableVolumetricClouds);

            var volume = GetMemberValue(clouds, "settingsVolume");
            if (volume == null) return;

            TrySetMember(volume, "coverage", profile.VolumetricCloudCoverage);
            TrySetMember(volume, "density", profile.VolumetricCloudDensity);
        }

        private static void ApplyFlatCloudSettings(Component manager, WorldEnvironmentProfile profile)
        {
            var flat = GetMemberValue(manager, "FlatClouds");
            if (flat == null) return;

            TrySetMember(flat, "active", profile.EnableFlatClouds);
            var settings = GetMemberValue(flat, "settings") ?? GetMemberValue(flat, "Settings");
            if (settings == null) return;

            TrySetMember(settings, "useCirrusClouds", profile.EnableCirrusClouds);
            TrySetMember(settings, "cirrusCloudsAlpha", profile.CirrusCloudAlpha);
            TrySetMember(settings, "cirrusCloudsCoverage", profile.CirrusCloudCoverage);
        }

        private static bool TrySetWeather(Component manager, string weatherId)
        {
            if (manager == null || string.IsNullOrEmpty(weatherId)) return false;

            var weather = GetMemberValue(manager, "Weather") ?? GetMemberValue(manager, "WeatherModule");
            if (weather == null) return false;

            if (TryChangeWeather(weather, weatherId))
                return true;

            var aliases = ResolveWeatherAliases(weatherId);
            for (var i = 0; i < aliases.Length; i++)
            {
                if (TryChangeWeather(weather, aliases[i]))
                    return true;
            }

            return false;
        }

        private static bool TryChangeWeather(object weather, string weatherId)
        {
            if (TryInvoke(weather, "ChangeWeatherInstant", weatherId))
                return true;
            if (TryInvoke(weather, "ChangeWeather", weatherId))
                return true;
            return TryInvoke(weather, "SetWeatherOverride", weatherId);
        }

        private static string[] ResolveWeatherAliases(string weatherId)
        {
            switch (weatherId.Trim().ToLowerInvariant())
            {
                case "clear":
                    return new[] { "Clear Sky", "Clear" };
                case "cloudy":
                    return new[] { "Cloudy 1", "Cloudy 2", "Cloudy" };
                case "fog":
                case "foggy":
                    return new[] { "Foggy", "Fog" };
                case "rain":
                    return new[] { "Rain" };
                case "storm":
                    return new[] { "Storm" };
                case "snow":
                    return new[] { "Snow" };
                default:
                    return Array.Empty<string>();
            }
        }
    }
}
