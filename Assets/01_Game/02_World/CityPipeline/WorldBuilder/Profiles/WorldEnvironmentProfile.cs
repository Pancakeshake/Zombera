using System;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public enum WorldEnvironmentSeason
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }

    [Serializable]
    public sealed class WorldWeatherWeightEntry
    {
        public string WeatherId = "Clear";
        [Range(0f, 1f)] public float Weight = 1f;
        public string BiomeOrRegionFilter;
    }

    /// <summary>
    /// Authoring surface for pipeline Enviro: weather cycle, time/season/quality, fog/clouds.
    /// Applied by <c>EnviroWorldEnvironmentBackend</c> onto the Enviro under EnviroRoot.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldEnvironmentProfile",
        menuName = "Zombera/World/World Environment Profile")]
    public sealed class WorldEnvironmentProfile : ScriptableObject
    {
        [Header("Weather Cycle")]
        [SerializeField] private string startingWeatherId = "Cloudy";
        [SerializeField] private WorldWeatherWeightEntry[] weatherWeights =
        {
            new() { WeatherId = "Clear", Weight = 0.55f },
            new() { WeatherId = "Cloudy", Weight = 0.25f },
            new() { WeatherId = "Rain", Weight = 0.15f },
            new() { WeatherId = "Storm", Weight = 0.05f }
        };

        [SerializeField] private float transitionDurationSeconds = 45f;
        [SerializeField] private float changeIntervalMinHours = 3.5f;
        [SerializeField] private float changeIntervalMaxHours = 8.5f;

        [Header("Time Of Day")]
        [Tooltip("Starting local time of day in hours (0–24). Morning survival default is ~10.")]
        [SerializeField] [Range(0f, 24f)] private float timeOfDayHours = 10f;
        [SerializeField] private bool simulateTime = true;
        [Tooltip("Realtime minutes for a full 24h Enviro cycle.")]
        [SerializeField] private float cycleLengthInMinutes = 40f;

        [Header("Season")]
        [SerializeField] private WorldEnvironmentSeason season = WorldEnvironmentSeason.Autumn;
        [Tooltip("When false, locks the authored season (Enviro will not auto-advance by day-of-year).")]
        [SerializeField] private bool autoChangeSeason;

        [Header("Enviro Assets")]
        [SerializeField] private UnityEngine.Object enviroConfig;
        [SerializeField] private UnityEngine.Object enviroWeatherPresetLibrary;
        [SerializeField] private UnityEngine.Object enviroZone;
        [SerializeField] private UnityEngine.Object enviroQuality;
        [Tooltip("Fallback quality asset name when enviroQuality is unset (e.g. High, Medium).")]
        [SerializeField] private string enviroQualityName = "High";

        [Header("Atmosphere (pipeline Enviro)")]
        [Tooltip("Bind Camera.main (or first game camera) so Enviro fog/clouds can render.")]
        [SerializeField] private bool bindGameplayCamera = true;
        [Tooltip("Assign Enviro Objects.worldAnchor for floating-point / large-world cloud stability.")]
        [SerializeField] private bool bindWorldAnchor = true;
        [Tooltip("Soft-fail Validate when Enviro URP Render Feature is missing from the active renderer.")]
        [SerializeField] private bool requireUrpRenderFeature = true;

        [SerializeField] private bool enableFog = true;
        [SerializeField] private bool enableVolumetricFog = true;
        [SerializeField] [Range(0.001f, 0.2f)] private float fogDensity = 0.034f;
        [SerializeField] [Range(0.001f, 0.1f)] private float fogHeightFalloff = 0.012f;
        [SerializeField] private float fogHeightWorldY;

        [SerializeField] private bool enableVolumetricClouds = true;
        [SerializeField] [Range(0f, 1.5f)] private float volumetricCloudCoverage = 0.45f;
        [SerializeField] [Range(0.1f, 3f)] private float volumetricCloudDensity = 1f;

        [SerializeField] private bool enableFlatClouds = true;
        [SerializeField] private bool enableCirrusClouds = true;
        [SerializeField] [Range(0f, 1f)] private float cirrusCloudAlpha = 0.35f;
        [SerializeField] [Range(0f, 1f)] private float cirrusCloudCoverage = 0.3f;

        public string StartingWeatherId => startingWeatherId;
        public WorldWeatherWeightEntry[] WeatherWeights => weatherWeights;
        public float TransitionDurationSeconds => transitionDurationSeconds;
        public float ChangeIntervalMinHours => changeIntervalMinHours;
        public float ChangeIntervalMaxHours => changeIntervalMaxHours;

        public float TimeOfDayHours => timeOfDayHours;
        public bool SimulateTime => simulateTime;
        public float CycleLengthInMinutes => cycleLengthInMinutes;
        public WorldEnvironmentSeason Season => season;
        public bool AutoChangeSeason => autoChangeSeason;

        public UnityEngine.Object EnviroConfig => enviroConfig;
        public UnityEngine.Object EnviroWeatherPresetLibrary => enviroWeatherPresetLibrary;
        public UnityEngine.Object EnviroZone => enviroZone;
        public UnityEngine.Object EnviroQuality => enviroQuality;
        public string EnviroQualityName => enviroQualityName;

        public bool BindGameplayCamera => bindGameplayCamera;
        public bool BindWorldAnchor => bindWorldAnchor;
        public bool RequireUrpRenderFeature => requireUrpRenderFeature;
        public bool EnableFog => enableFog;
        public bool EnableVolumetricFog => enableVolumetricFog;
        public float FogDensity => fogDensity;
        public float FogHeightFalloff => fogHeightFalloff;
        public float FogHeightWorldY => fogHeightWorldY;
        public bool EnableVolumetricClouds => enableVolumetricClouds;
        public float VolumetricCloudCoverage => volumetricCloudCoverage;
        public float VolumetricCloudDensity => volumetricCloudDensity;
        public bool EnableFlatClouds => enableFlatClouds;
        public bool EnableCirrusClouds => enableCirrusClouds;
        public float CirrusCloudAlpha => cirrusCloudAlpha;
        public float CirrusCloudCoverage => cirrusCloudCoverage;
    }
}
