using Crest;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Forwards package-neutral weather snapshots into Crest wave settings.
    /// Scales the global ShapeFFT weight and OceanRenderer wind between baseline and storm.
    /// </summary>
    [AddComponentMenu("Zombera/World/Crest Weather Adapter")]
    [DisallowMultipleComponent]
    public sealed class CrestWeatherAdapter : MonoBehaviour, IWorldWeatherConsumer
    {
        private const string GlobalWaveShapeName = "Crest Wave Shape";

        [SerializeField] private float _stormWaveWeight = 1f;
        [SerializeField] private float _stormWindSpeedKph = 55f;

        private float _lastWind01 = -1f;
        private float _baselineOuterWeight = -1f;
        private float _baselineOuterWindKph = -1f;

        public void ApplyWeather(in WorldWeatherSnapshot weather)
        {
            var wind = Mathf.Clamp01(weather.WindStrength01);
            // Skip only when Crest is live and already has this wind; otherwise retry
            // (Environment may bind before ocean exists, or ocean may have been rebuilt).
            if (_lastWind01 >= 0f &&
                Mathf.Abs(wind - _lastWind01) < 0.01f &&
                OceanRenderer.Instance != null)
                return;

            if (!TryApplyCrestWind(wind))
            {
                if (Time.frameCount % 600 != 0)
                    return;

                Debug.Log(
                    "[CrestWeatherAdapter] Crest wave target unavailable; weather snapshot kept package-neutral.",
                    this);
                return;
            }

            _lastWind01 = wind;
        }

        private bool TryApplyCrestWind(float wind01)
        {
            var ocean = OceanRenderer.Instance;
            if (ocean == null)
                return false;

            CacheBaselines(ocean);

            var weight = Mathf.Lerp(_baselineOuterWeight, _stormWaveWeight, wind01);
            var windKph = Mathf.Lerp(_baselineOuterWindKph, _stormWindSpeedKph, wind01);

            ocean._globalWindSpeed = windKph;

            var globalFft = FindGlobalShapeFft(ocean.transform);
            if (globalFft == null)
                return true;

            globalFft._weight = Mathf.Clamp01(weight);
            globalFft._overrideGlobalWindSpeed = true;
            globalFft._windSpeed = windKph;
            return true;
        }

        private void CacheBaselines(OceanRenderer ocean)
        {
            if (_baselineOuterWeight >= 0f)
                return;

            var globalFft = FindGlobalShapeFft(ocean.transform);
            _baselineOuterWeight = globalFft != null
                ? Mathf.Clamp01(globalFft._weight)
                : 1f;
            _baselineOuterWindKph = ocean._globalWindSpeed > 0f
                ? ocean._globalWindSpeed
                : 28f;

            if (_stormWaveWeight < _baselineOuterWeight)
                _stormWaveWeight = Mathf.Clamp01(_baselineOuterWeight + 0.25f);
        }

        private static ShapeFFT FindGlobalShapeFft(Transform oceanRoot)
        {
            if (oceanRoot == null)
                return null;

            var ffts = oceanRoot.GetComponentsInChildren<ShapeFFT>(true);
            for (var i = 0; i < ffts.Length; i++)
            {
                var fft = ffts[i];
                if (fft != null && fft.name == GlobalWaveShapeName)
                    return fft;
            }

            for (var i = 0; i < ffts.Length; i++)
            {
                var fft = ffts[i];
                if (fft == null || fft.name == "Crest Wave Shape Coastal")
                    continue;
                if (fft.GetComponent<MeshRenderer>() == null)
                    return fft;
            }

            return ffts.Length > 0 ? ffts[0] : null;
        }
    }
}
