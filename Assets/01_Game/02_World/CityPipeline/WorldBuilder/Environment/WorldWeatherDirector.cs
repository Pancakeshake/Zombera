using System;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Deterministic weather selection using a dedicated RNG stream.</summary>
    [AddComponentMenu("Zombera/World/World Weather Director")]
    [DisallowMultipleComponent]
    public sealed class WorldWeatherDirector : MonoBehaviour, IWorldWeatherSource
    {
        [SerializeField] private WorldEnvironmentProfile _profile;

        private DeterministicRng _rng;
        private WorldEnvironmentState _state = new();
        private float _hoursAccumulated;

        public event Action<WorldWeatherSnapshot> WeatherChanged;

        public WorldWeatherSnapshot Current { get; private set; } =
            new("Clear", 0f, 0f, 0f, 18f);

        public void Configure(WorldEnvironmentContext context)
        {
            if (context?.Profile != null)
                _profile = context.Profile;

            _rng = context?.Rng?.CreateStream(0x57454154) ??
                   new DeterministicRng(context?.Session.Seed ?? 1);

            var startId = _profile != null ? _profile.StartingWeatherId : "Clear";
            _state.ActiveWeatherId = startId;
            _state.HoursUntilNextChange = RollInterval();
            Publish();
        }

        public void TickGameHours(float deltaGameHours)
        {
            if (_profile == null || deltaGameHours <= 0f) return;

            _hoursAccumulated += deltaGameHours;
            if (_hoursAccumulated < _state.HoursUntilNextChange) return;

            _hoursAccumulated = 0f;
            _state.ActiveWeatherId = PickWeightedWeather();
            _state.HoursUntilNextChange = RollInterval();
            _state.Transition01 = 0f;
            Publish();
        }

        private float RollInterval()
        {
            if (_profile == null) return 5f;
            var min = _profile.ChangeIntervalMinHours;
            var max = Mathf.Max(min, _profile.ChangeIntervalMaxHours);
            return Mathf.Lerp(min, max, _rng != null ? _rng.NextFloat01() : 0.5f);
        }

        private string PickWeightedWeather()
        {
            if (_profile?.WeatherWeights == null || _profile.WeatherWeights.Length == 0)
                return _state.ActiveWeatherId ?? "Clear";

            var total = 0f;
            for (var i = 0; i < _profile.WeatherWeights.Length; i++)
            {
                var entry = _profile.WeatherWeights[i];
                if (entry == null) continue;
                total += Mathf.Max(0f, entry.Weight);
            }

            if (total <= 0f) return _state.ActiveWeatherId ?? "Clear";

            var pick = (_rng != null ? _rng.NextFloat01() : 0.5f) * total;
            var cursor = 0f;
            for (var i = 0; i < _profile.WeatherWeights.Length; i++)
            {
                var entry = _profile.WeatherWeights[i];
                if (entry == null) continue;
                cursor += Mathf.Max(0f, entry.Weight);
                if (pick <= cursor)
                    return string.IsNullOrEmpty(entry.WeatherId) ? "Clear" : entry.WeatherId;
            }

            return _profile.WeatherWeights[^1]?.WeatherId ?? "Clear";
        }

        private void Publish()
        {
            Current = new WorldWeatherSnapshot(
                _state.ActiveWeatherId,
                _state.Transition01,
                _state.WindStrength01,
                _state.Precipitation01,
                _state.TemperatureCelsius);
            WeatherChanged?.Invoke(Current);
        }

        public WorldWeatherSaveSnapshot CaptureSnapshot()
        {
            return new WorldWeatherSaveSnapshot(
                Current.WeatherId,
                _state.Transition01,
                _rng != null ? _rng.State : 0UL,
                _rng != null ? _rng.Stream : 0UL,
                _state.HoursUntilNextChange,
                _profile != null ? 1 : 1);
        }

        public void RestoreFromSave(Zombera.Core.EnvironmentSaveData data)
        {
            if (data == null) return;
            _state.ActiveWeatherId = string.IsNullOrEmpty(data.weatherId) ? "Clear" : data.weatherId;
            _state.Transition01 = data.transitionProgress;
            _state.HoursUntilNextChange = data.hoursUntilNextChange > 0f ? data.hoursUntilNextChange : RollInterval();
            if (data.weatherRngState != 0 || data.weatherRngStream != 0)
                _rng = DeterministicRng.FromState(data.weatherRngState, data.weatherRngStream);
            Publish();
        }
    }

    public readonly struct WorldWeatherSaveSnapshot
    {
        public WorldWeatherSaveSnapshot(
            string weatherId,
            float transitionProgress,
            ulong rngState,
            ulong rngStream,
            float hoursUntilNextChange,
            int profileVersion)
        {
            WeatherId = weatherId ?? "Clear";
            TransitionProgress = transitionProgress;
            RngState = rngState;
            RngStream = rngStream;
            HoursUntilNextChange = hoursUntilNextChange;
            ProfileVersion = profileVersion;
        }

        public string WeatherId { get; }
        public float TransitionProgress { get; }
        public ulong RngState { get; }
        public ulong RngStream { get; }
        public float HoursUntilNextChange { get; }
        public int ProfileVersion { get; }
    }
}
