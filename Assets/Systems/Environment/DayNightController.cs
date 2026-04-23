using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombera.Environment
{
    /// <summary>Phases of the in-game day for gameplay logic (zombie spawn rates, etc.).</summary>
    public enum TimeOfDayPhase { Night, Dawn, Day, Dusk }

    /// <summary>
    /// Controls the world day/night cycle by rotating a directional sun light and
    /// driving skybox, ambient, and fog properties over normalized game time (0–1 = 24h).
    ///
    /// Respects TimeSystem.CurrentTimeScale so pause/slow-mo work automatically.
    /// Place one instance in the World scene. The editor tool wires everything up.
    /// </summary>
    [AddComponentMenu("Zombera/Environment/Day Night Controller")]
    public sealed class DayNightController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Clock")]
        [SerializeField, Range(0f, 24f)] private float startingHour = 8f;
        [Tooltip("How many real seconds equal one full game day (default 1200 = 20 min).")]
        [SerializeField, Min(1f)] private float realSecondsPerGameDay = 1200f;

        [Header("Sun")]
        [SerializeField] private Light sun;
        [SerializeField] private Gradient sunColor = new Gradient();
        [SerializeField] private AnimationCurve sunIntensity = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Ambient")]
        [SerializeField] private Gradient ambientSkyColor     = new Gradient();
        [SerializeField] private Gradient ambientEquatorColor = new Gradient();
        [SerializeField] private Gradient ambientGroundColor  = new Gradient();

        [Header("Fog")]
        [SerializeField] private Gradient fogColor = new Gradient();
        [SerializeField] private AnimationCurve fogDensity = AnimationCurve.Linear(0f, 0.003f, 1f, 0.003f);

        [Header("Skybox (Procedural)")]
        [SerializeField] private Material skyboxMaterial;
        [SerializeField] private AnimationCurve skyboxAtmosphere = AnimationCurve.Linear(0f, 0.5f, 1f, 0.5f);
        [SerializeField] private AnimationCurve skyboxExposure   = AnimationCurve.Linear(0f, 0.2f, 1f, 0.2f);

        // ── Public state ──────────────────────────────────────────────────────

        public static DayNightController Instance { get; private set; }

        /// <summary>Current game hour (0–24).</summary>
        public float CurrentHour { get; private set; }

        /// <summary>Current in-game day number, starts at 1 and increments each midnight.</summary>
        public int DayNumber { get; private set; } = 1;

        /// <summary>0 = midnight, 0.5 = noon, 1 = midnight.</summary>
        public float NormalizedTime => CurrentHour / 24f;

        public TimeOfDayPhase CurrentPhase { get; private set; }

        /// <summary>Fires when the integer game hour advances.</summary>
        public event Action<float> OnHourChanged;

        /// <summary>Fires when the time-of-day phase changes (Dawn/Day/Dusk/Night).</summary>
        public event Action<TimeOfDayPhase> OnPhaseChanged;

        // ── Private state ─────────────────────────────────────────────────────

        // Use a weak reference so we don't keep the TimeSystem alive across scene reloads.
        private Core.TimeSystem _timeSystem;
        private int _lastWholeHour = -1;
        private TimeOfDayPhase _lastPhase;

        // DynamicGI.UpdateEnvironment is expensive — throttle to once per 5 s.
        private float _giTimer;
        private const float GiUpdateInterval = 5f;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _timeSystem = FindFirstObjectByType<Core.TimeSystem>();

            // Auto-discover sun if not assigned in Inspector (prevents cross-scene serialized refs).
            if (sun == null)
            {
                sun = FindDirectionalLightInScene();
                if (sun != null)
                    Debug.Log($"[DayNightController] Auto-discovered sun light: '{sun.name}'.", this);
            }

            CurrentHour = startingHour;
            _lastPhase  = GetPhase(CurrentHour);
            ApplyAll(NormalizedTime, forceGi: true);
        }

        private static Light FindDirectionalLightInScene()
        {
            Light[] all = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Light l in all)
                if (l.type == LightType.Directional) return l;
            return null;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float scale = _timeSystem != null ? _timeSystem.CurrentTimeScale : 1f;
            float nextHour = CurrentHour + Time.deltaTime * scale * 24f / realSecondsPerGameDay;
            if (nextHour >= 24f) DayNumber++;
            CurrentHour = nextHour % 24f;

            _giTimer += Time.deltaTime;
            bool doGi = _giTimer >= GiUpdateInterval;
            if (doGi) _giTimer = 0f;

            ApplyAll(NormalizedTime, forceGi: doGi);
            TickEvents();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Jump to a specific game hour (0–24). Safe to call at runtime.</summary>
        public void SetHour(float hour)
        {
            CurrentHour = Mathf.Repeat(hour, 24f);
            ApplyAll(NormalizedTime, forceGi: true);
        }

        /// <summary>Returns which phase of day the given game hour falls in.</summary>
        public static TimeOfDayPhase GetPhase(float hour)
        {
            if (hour >= 5f  && hour < 7f)  return TimeOfDayPhase.Dawn;
            if (hour >= 7f  && hour < 18f) return TimeOfDayPhase.Day;
            if (hour >= 18f && hour < 21f) return TimeOfDayPhase.Dusk;
            return TimeOfDayPhase.Night;
        }

        // ── Environment ───────────────────────────────────────────────────────

        private void ApplyAll(float t, bool forceGi)
        {
            RotateSun(t);

            if (sun != null)
            {
                sun.color     = sunColor.Evaluate(t);
                sun.intensity = sunIntensity.Evaluate(t);
            }

            RenderSettings.ambientMode         = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = ambientSkyColor.Evaluate(t);
            RenderSettings.ambientEquatorColor = ambientEquatorColor.Evaluate(t);
            RenderSettings.ambientGroundColor  = ambientGroundColor.Evaluate(t);

            if (RenderSettings.fog)
            {
                RenderSettings.fogColor   = fogColor.Evaluate(t);
                RenderSettings.fogDensity = fogDensity.Evaluate(t);
            }

            if (skyboxMaterial != null)
            {
                if (skyboxMaterial.HasProperty("_AtmosphereThickness"))
                {
                    skyboxMaterial.SetFloat("_AtmosphereThickness", skyboxAtmosphere.Evaluate(t));
                }

                if (skyboxMaterial.HasProperty("_Exposure"))
                {
                    skyboxMaterial.SetFloat("_Exposure", skyboxExposure.Evaluate(t));
                }
            }

            if (forceGi)
            {
                DynamicGI.UpdateEnvironment();
            }
        }

        private void RotateSun(float t)
        {
            if (sun == null) return;
            // t=0.00 (midnight) → X= -90 (below horizon)
            // t=0.25 (6 am)    → X=   0 (rising on horizon)
            // t=0.50 (noon)    → X=  90 (overhead)
            // t=0.75 (6 pm)    → X= 180 (setting)
            float xAngle = t * 360f - 90f;
            sun.transform.rotation = Quaternion.Euler(xAngle, -30f, 0f);
        }

        private void TickEvents()
        {
            int whole = Mathf.FloorToInt(CurrentHour);
            if (whole != _lastWholeHour)
            {
                _lastWholeHour = whole;
                OnHourChanged?.Invoke(CurrentHour);
            }

            TimeOfDayPhase phase = GetPhase(CurrentHour);
            if (phase != _lastPhase)
            {
                _lastPhase = phase;
                OnPhaseChanged?.Invoke(phase);
            }
        }

        // ── Editor defaults ───────────────────────────────────────────────────
        // Called automatically by Unity when the component is first added in the Editor.
        // Also called by the setup tool directly.
#if UNITY_EDITOR
        private void Reset()
        {
            ApplyZomberaSurvivalDefaults();
        }

        internal void ApplyZomberaSurvivalDefaults()
        {
            // ── Curves ──────────────────────────────────────────────────────
            // Sun is dark at night, ramps up at dawn, peaks at noon, falls at dusk.
            sunIntensity = new AnimationCurve(
                new Keyframe(0.00f, 0.00f, 0f,  0f),
                new Keyframe(0.22f, 0.00f, 0f,  0f),   // ~5:15 am still dark
                new Keyframe(0.27f, 0.30f, 4f,  4f),   // ~6:30 am sunrise
                new Keyframe(0.50f, 1.00f, 0f,  0f),   // noon
                new Keyframe(0.73f, 0.30f, -4f, -4f),  // ~5:30 pm sunset
                new Keyframe(0.78f, 0.00f, 0f,  0f),   // ~6:45 pm dark
                new Keyframe(1.00f, 0.00f, 0f,  0f)
            );

            // Fog is denser at night, thinner during daylight.
            fogDensity = new AnimationCurve(
                new Keyframe(0.00f, 0.006f),
                new Keyframe(0.22f, 0.006f),
                new Keyframe(0.32f, 0.002f),
                new Keyframe(0.68f, 0.002f),
                new Keyframe(0.78f, 0.006f),
                new Keyframe(1.00f, 0.006f)
            );

            // Atmosphere thicker near horizon at sunrise/sunset.
            skyboxAtmosphere = new AnimationCurve(
                new Keyframe(0.00f, 0.40f),
                new Keyframe(0.25f, 0.75f),
                new Keyframe(0.50f, 1.00f),
                new Keyframe(0.75f, 0.75f),
                new Keyframe(1.00f, 0.40f)
            );

            // Skybox exposure very low at night, bright at noon.
            skyboxExposure = new AnimationCurve(
                new Keyframe(0.00f, 0.18f),
                new Keyframe(0.25f, 0.65f),
                new Keyframe(0.50f, 1.30f),
                new Keyframe(0.75f, 0.65f),
                new Keyframe(1.00f, 0.18f)
            );

            // ── Gradients ────────────────────────────────────────────────────
            sunColor            = BuildSunColorGradient();
            ambientSkyColor     = BuildAmbientSkyGradient();
            ambientEquatorColor = BuildAmbientEquatorGradient();
            ambientGroundColor  = BuildAmbientGroundGradient();
            fogColor            = BuildFogGradient();
        }

        // Gritty zombie-survival sun color: moonlight → orange dawn → dusty noon → fiery sunset.
        private static Gradient BuildSunColorGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.50f, 0.56f, 0.78f), 0.00f), // moonlight blue-white
                    new GradientColorKey(new Color(1.00f, 0.48f, 0.19f), 0.25f), // orange sunrise
                    new GradientColorKey(new Color(1.00f, 0.91f, 0.69f), 0.33f), // warm morning
                    new GradientColorKey(new Color(0.96f, 0.91f, 0.82f), 0.50f), // muted dusty noon
                    new GradientColorKey(new Color(0.94f, 0.82f, 0.50f), 0.65f), // warm afternoon haze
                    new GradientColorKey(new Color(1.00f, 0.31f, 0.06f), 0.75f), // deep orange/red sunset
                    new GradientColorKey(new Color(0.44f, 0.13f, 0.31f), 0.82f), // dark reddish dusk
                    new GradientColorKey(new Color(0.50f, 0.56f, 0.78f), 1.00f), // moonlight
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return g;
        }

        private static Gradient BuildAmbientSkyGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.04f, 0.07f, 0.14f), 0.00f), // deep night
                    new GradientColorKey(new Color(0.78f, 0.31f, 0.19f), 0.25f), // dawn orange-red
                    new GradientColorKey(new Color(0.53f, 0.69f, 0.88f), 0.33f), // morning sky
                    new GradientColorKey(new Color(0.38f, 0.56f, 0.75f), 0.50f), // midday sky
                    new GradientColorKey(new Color(0.47f, 0.61f, 0.77f), 0.65f), // afternoon haze
                    new GradientColorKey(new Color(0.75f, 0.25f, 0.06f), 0.75f), // sunset
                    new GradientColorKey(new Color(0.19f, 0.08f, 0.13f), 0.82f), // dusk
                    new GradientColorKey(new Color(0.04f, 0.07f, 0.14f), 1.00f), // deep night
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return g;
        }

        private static Gradient BuildAmbientEquatorGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.03f, 0.04f, 0.08f), 0.00f),
                    new GradientColorKey(new Color(0.38f, 0.19f, 0.10f), 0.25f),
                    new GradientColorKey(new Color(0.25f, 0.31f, 0.38f), 0.40f),
                    new GradientColorKey(new Color(0.19f, 0.22f, 0.27f), 0.50f),
                    new GradientColorKey(new Color(0.25f, 0.22f, 0.17f), 0.65f),
                    new GradientColorKey(new Color(0.38f, 0.15f, 0.05f), 0.75f),
                    new GradientColorKey(new Color(0.09f, 0.04f, 0.06f), 0.82f),
                    new GradientColorKey(new Color(0.03f, 0.04f, 0.08f), 1.00f),
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return g;
        }

        private static Gradient BuildAmbientGroundGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.02f, 0.02f, 0.03f), 0.00f),
                    new GradientColorKey(new Color(0.13f, 0.08f, 0.04f), 0.25f),
                    new GradientColorKey(new Color(0.09f, 0.10f, 0.07f), 0.40f),
                    new GradientColorKey(new Color(0.07f, 0.08f, 0.06f), 0.50f),
                    new GradientColorKey(new Color(0.09f, 0.09f, 0.06f), 0.65f),
                    new GradientColorKey(new Color(0.13f, 0.06f, 0.02f), 0.75f),
                    new GradientColorKey(new Color(0.04f, 0.02f, 0.02f), 0.82f),
                    new GradientColorKey(new Color(0.02f, 0.02f, 0.03f), 1.00f),
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return g;
        }

        private static Gradient BuildFogGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.06f, 0.08f, 0.12f), 0.00f), // night: dark blue
                    new GradientColorKey(new Color(0.43f, 0.31f, 0.24f), 0.25f), // dawn: orange-grey
                    new GradientColorKey(new Color(0.60f, 0.67f, 0.73f), 0.40f), // morning: light blue-grey
                    new GradientColorKey(new Color(0.56f, 0.63f, 0.69f), 0.50f), // midday: desaturated blue haze
                    new GradientColorKey(new Color(0.63f, 0.53f, 0.44f), 0.70f), // afternoon: warm haze
                    new GradientColorKey(new Color(0.62f, 0.40f, 0.33f), 0.75f), // sunset: orange
                    new GradientColorKey(new Color(0.13f, 0.08f, 0.12f), 0.82f), // dusk
                    new GradientColorKey(new Color(0.06f, 0.08f, 0.12f), 1.00f), // night
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return g;
        }
#endif
    }
}
