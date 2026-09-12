#region

using System;
using System.Linq;
using UnityEngine;
using Zombera.Core;

#endregion

namespace Zombera.Environment
{
    /// <summary>Phases of the in-game day for gameplay logic (zombie spawn rates, etc.).</summary>
    public enum TimeOfDayPhase
    {
        Night,
        Dawn,
        Day,
        Dusk
    }

    /// <summary>
    ///     Controls the world day/night cycle by rotating a directional sun light and
    ///     driving skybox, ambient, and fog properties over normalized game time (0–1 = 24h).
    ///     Respects TimeSystem.CurrentTimeScale so pause/slow-mo work automatically.
    ///     Place one instance in the World scene. The editor tool wires everything up.
    /// </summary>
    [AddComponentMenu("Zombera/Environment/Day Night Controller")]
    public sealed class DayNightController : MonoBehaviour
    {
        private const float GiUpdateInterval = 5f;
        private static readonly int SAtmosphereThicknessId = Shader.PropertyToID("_AtmosphereThickness");
        private static readonly int SExposureId = Shader.PropertyToID("_Exposure");

        [Header("Clock")] [SerializeField] [Range(0f, 24f)]
        private float startingHour = 8f;

        [Tooltip("How many real seconds equal one full game day (default 1200 = 20 min).")] [SerializeField] [Min(1f)]
        private float realSecondsPerGameDay = 1200f;

        [Header("Sun")] [SerializeField] private Light sun;

        [SerializeField] private Gradient sunColor = new();
        [SerializeField] private AnimationCurve sunIntensity = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Ambient")] [SerializeField] private Gradient ambientSkyColor = new();

        [SerializeField] private Gradient ambientEquatorColor = new();
        [SerializeField] private Gradient ambientGroundColor = new();

        [Header("Fog")] [SerializeField] private Gradient fogColor = new();

        [SerializeField] private AnimationCurve fogDensity = AnimationCurve.Linear(0f, 0.003f, 1f, 0.003f);

        [Header("Skybox (Procedural)")] [SerializeField]
        private Material skyboxMaterial;

        [Header("Compatibility")] [SerializeField]
        private bool disableWhenEnviroPresent = true;

        [SerializeField] private bool driveEnviroTimeWhenPresent = true;

        [SerializeField] [Min(0.1f)] private float enviroProbeIntervalSeconds = 5f;

        [SerializeField] private AnimationCurve skyboxAtmosphere = AnimationCurve.Linear(0f, 0.5f, 1f, 0.5f);
        [SerializeField] private AnimationCurve skyboxExposure = AnimationCurve.Linear(0f, 0.2f, 1f, 0.2f);

    #if UNITY_EDITOR
        [Header("Editor Defaults")] [SerializeField]
        private DayNightDefaultsProfile defaultsProfile;
    #endif

        // Legacy debug fields are kept hidden so existing scenes can migrate automatically.
        [SerializeField] [HideInInspector] private bool showTimeDebugReadout;
        [SerializeField] [HideInInspector] private bool showTimeDebugReadoutOnlyWhenDebugMenuVisible = true;
        [SerializeField] [HideInInspector] private Vector2 debugReadoutScreenOffset = new(14f, 14f);

        private float _giTimer;
        private int _lastWholeHour = -1;
        private bool _enviroDetected;
        private bool _enviroDrivingActive;
        private float _nextEnviroProbeAt;
        private DayNightRenderContext _renderContext;
        private TimeSystem _timeSystem;
        private readonly DayNightEnviroBridge _enviroBridge = new();
        private static DayNightController _instance;
        private static bool _warnedAboutMissingInstance;

        public static DayNightController Instance
        {
            get
            {
                if (_instance == null && !_warnedAboutMissingInstance)
                {
                    _warnedAboutMissingInstance = true;
                    Debug.LogWarning("[DayNightController] Instance accessed before Awake assigned a runtime instance.");
                }

                return _instance;
            }
            private set
            {
                _instance = value;
                if (_instance != null) _warnedAboutMissingInstance = false;
            }
        }

        public float CurrentHour { get; private set; }

        public int DayNumber { get; private set; } = 1;

        public float NormalizedTime => CurrentHour / 24f;

        public TimeOfDayPhase CurrentPhase { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning(
                    $"[DayNightController] Duplicate instance detected on '{name}'. Keeping '{_instance.name}' and destroying duplicate GameObject.",
                    this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            _timeSystem = FindFirstObjectByType<TimeSystem>();
            if (sun == null) sun = FindDirectionalLightInScene();

            CurrentHour = startingHour;
            CurrentPhase = GetPhase(CurrentHour);

            _enviroDetected = _enviroBridge.DetectEnviro();
            if (_enviroDetected && driveEnviroTimeWhenPresent)
            {
                _enviroDrivingActive = true;
                _ = _enviroBridge.SyncTime(CurrentHour, DayNumber, true);
                return;
            }

            if (_enviroDetected && disableWhenEnviroPresent)
            {
                enabled = false;
                return;
            }

            ApplyAll(NormalizedTime, true);
        }

        private void Update()
        {
            if (!CanAdvanceClockForCurrentGameState()) return;

            if (_timeSystem == null)
                _timeSystem = FindFirstObjectByType<TimeSystem>();

            if (!_enviroDetected && Time.unscaledTime >= _nextEnviroProbeAt && _enviroBridge.DetectEnviro())
            {
                _enviroDetected = true;

                if (driveEnviroTimeWhenPresent)
                    _enviroDrivingActive = true;
                else if (disableWhenEnviroPresent)
                {
                    enabled = false;
                    return;
                }
            }

            if (!_enviroDetected)
                _nextEnviroProbeAt = Time.unscaledTime + Mathf.Max(0.1f, enviroProbeIntervalSeconds);

            var scale = _timeSystem != null ? _timeSystem.CurrentTimeScale : 1f;
            var next = DayNightClockLogic.Advance(CurrentHour, DayNumber, Time.unscaledDeltaTime, scale, realSecondsPerGameDay);
            CurrentHour = next.Hour;
            DayNumber = next.DayNumber;

            if (_enviroDrivingActive)
            {
                _ = _enviroBridge.SyncTime(CurrentHour, DayNumber, false);
                TickEvents();
                return;
            }

            _giTimer += Time.deltaTime;
            var doGi = _giTimer >= GiUpdateInterval;
            if (doGi) _giTimer = 0f;

            ApplyAll(NormalizedTime, doGi);
            TickEvents();
        }

        private static bool CanAdvanceClockForCurrentGameState()
        {
            var gm = GameManagerGateway.Instance;
            if (gm == null) return true;

            var state = gm.CurrentState;
            return state is GameState.LoadingWorld or GameState.Playing or GameState.Paused;
        }

        private void OnDestroy()
        {
            if (_instance == this) Instance = null;
        }

        public event Action<float> OnHourChanged;

        public event Action<TimeOfDayPhase> OnPhaseChanged;

        private static Light FindDirectionalLightInScene()
        {
            return FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(static light => light.type == LightType.Directional);
        }

        public void SetHour(float hour)
        {
            CurrentHour = DayNightClockLogic.NormalizeHour(hour);

            if (_enviroDrivingActive)
            {
                _ = _enviroBridge.SyncTime(CurrentHour, DayNumber, true);
                TickEvents();
                return;
            }

            ApplyAll(NormalizedTime, true);
        }

        public void SetDayNumber(int dayNumber)
        {
            DayNumber = Mathf.Max(1, dayNumber);
            if (_enviroDrivingActive)
                _ = _enviroBridge.SyncTime(CurrentHour, DayNumber, true);
        }

        public static TimeOfDayPhase GetPhase(float hour)
        {
            return DayNightClockLogic.GetPhase(hour);
        }

        public bool TryReadEnviroTime(out float enviroHour)
        {
            return _enviroBridge.TryReadEnviroTime(out enviroHour);
        }

        private void ApplyAll(float t, bool forceGi)
        {
            PopulateRenderContext();
            DayNightRenderApplier.Apply(in _renderContext, t, forceGi);
        }

        private void TickEvents()
        {
            var tick = DayNightClockLogic.Tick(CurrentHour, CurrentPhase, _lastWholeHour);
            _lastWholeHour = tick.LastWholeHour;

            if (tick.HourChanged)
                OnHourChanged?.Invoke(CurrentHour);

            if (!tick.PhaseChanged) return;

            CurrentPhase = tick.Phase;
            OnPhaseChanged?.Invoke(CurrentPhase);
        }

        private void PopulateRenderContext()
        {
            _renderContext.Sun = sun;
            _renderContext.SunColor = sunColor;
            _renderContext.SunIntensity = sunIntensity;
            _renderContext.AmbientSkyColor = ambientSkyColor;
            _renderContext.AmbientEquatorColor = ambientEquatorColor;
            _renderContext.AmbientGroundColor = ambientGroundColor;
            _renderContext.FogColor = fogColor;
            _renderContext.FogDensity = fogDensity;
            _renderContext.SkyboxMaterial = skyboxMaterial;
            _renderContext.SkyboxAtmosphere = skyboxAtmosphere;
            _renderContext.SkyboxExposure = skyboxExposure;
            _renderContext.AtmosphereThicknessShaderId = SAtmosphereThicknessId;
            _renderContext.ExposureShaderId = SExposureId;
        }

#if UNITY_EDITOR
        internal DayNightDefaultsProfile DefaultsProfile => defaultsProfile;

        private void Reset()
        {
            DayNightDefaultsProfileUtility.ApplyDefaultsToController(this);
        }

        internal void ApplyZomberaSurvivalDefaults()
        {
            DayNightDefaultsProfileUtility.ApplyDefaultsToController(this);
        }

        internal void ApplyDefaultsProfile(DayNightDefaultsProfile profile)
        {
            defaultsProfile = profile;
            if (profile == null) return;

            sunColor = CloneGradient(profile.SunColor);
            sunIntensity = CloneCurve(profile.SunIntensity);
            ambientSkyColor = CloneGradient(profile.AmbientSkyColor);
            ambientEquatorColor = CloneGradient(profile.AmbientEquatorColor);
            ambientGroundColor = CloneGradient(profile.AmbientGroundColor);
            fogColor = CloneGradient(profile.FogColor);
            fogDensity = CloneCurve(profile.FogDensity);
            skyboxAtmosphere = CloneCurve(profile.SkyboxAtmosphere);
            skyboxExposure = CloneCurve(profile.SkyboxExposure);
        }

        private static AnimationCurve CloneCurve(AnimationCurve source)
        {
            return source == null ? new AnimationCurve() : new AnimationCurve(source.keys);
        }

        private static Gradient CloneGradient(Gradient source)
        {
            var gradient = new Gradient();
            if (source == null) return gradient;

            gradient.SetKeys(source.colorKeys, source.alphaKeys);
            gradient.mode = source.mode;
            return gradient;
        }
#endif
    }
}