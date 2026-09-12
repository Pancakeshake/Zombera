#region

using UnityEngine;

#endregion

namespace Zombera.Combat
{
    /// <summary>
    ///     Single source of truth for combat tuning values that previously drifted
    ///     across CombatEncounterManager, ZombieController, PlayerInputController,
    ///     CommandSystem, and SquadController.
    ///     Place one asset at Resources/CombatTuningConfig to override the defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatTuningConfig", menuName = "Zombera/Combat/Combat Tuning Config")]
    public sealed class CombatTuningConfig : ScriptableObject
    {
        private const string ResourcePath = "CombatTuningConfig";

        private static CombatTuningConfig _active;
        private static bool _resolved;

        [Header("Encounter Ranges")]
        [SerializeField] [Min(0.1f)] private float engageRange = 1.4f;

        [SerializeField] [Min(0.1f)] private float disengageRange = 1.75f;

        [Header("Target Scanning")]
        [SerializeField] [Min(0f)] private float attackScanRadius = 20f;

        [Header("Facing")]
        [SerializeField] [Min(0f)] private float facingTurnSpeedDegreesPerSecond = 720f;

        public float EngageRange => Mathf.Max(0.1f, engageRange);
        public float DisengageRange => Mathf.Max(EngageRange + 0.1f, disengageRange);
        public float AttackScanRadius => Mathf.Max(0f, attackScanRadius);
        public float FacingTurnSpeedDegreesPerSecond => Mathf.Max(0f, facingTurnSpeedDegreesPerSecond);

        /// <summary>The Resources-loaded config, or null when no asset exists.</summary>
        public static CombatTuningConfig Active
        {
            get
            {
                if (_resolved) return _active;

                _resolved = true;
                _active = Resources.Load<CombatTuningConfig>(ResourcePath);
                return _active;
            }
        }

        public static float EngageRangeOr(float fallback)
        {
            var config = Active;
            return config != null ? config.EngageRange : fallback;
        }

        public static float DisengageRangeOr(float fallback)
        {
            var config = Active;
            return config != null ? config.DisengageRange : fallback;
        }

        public static float AttackScanRadiusOr(float fallback)
        {
            var config = Active;
            return config != null ? config.AttackScanRadius : fallback;
        }

        public static float FacingTurnSpeedOr(float fallback)
        {
            var config = Active;
            return config != null ? config.FacingTurnSpeedDegreesPerSecond : fallback;
        }
    }
}
