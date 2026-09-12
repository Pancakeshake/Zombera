#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.Characters
{
    public enum PostureState
    {
        Upright,
        Crouching,
        Crawling
    }

    /// <summary>
    ///     Stores gameplay attributes used by combat, crafting, and AI behavior decisions.
    /// </summary>
    public sealed partial class UnitStats : MonoBehaviour
    {
        public const int MinSkillLevel = 1;
        public const int MaxSkillLevel = 100;
        private const float DefaultStrengthXpPerMeterHeavyCarry = 0.2f;
        private const float DefaultStrengthXpPerWeightedCombatHit = 2.5f;
        private const float DefaultStrengthXpPerWeightTrainingRep = 8f;
        private const float DefaultMeleeXpPerHit = 3f;
        private const float DefaultShootingXpPerRangedHit = 3f;
        private const float DefaultAgilityXpPerMeterSprinted = 0.1f;
        private const float DefaultEnduranceXpPerSecondExertion = 0.05f;
        private const float DefaultMedicalXpPerHealPoint = 0.08f;
        private const float DefaultEngineeringXpPerPiecePlaced = 12f;
        private const float DefaultScavengingXpPerContainer = 8f;
        private const float DefaultStealthXpPerSecondUndetected = 0.04f;
        private const float DefaultToughnessXpPerDamagePoint = 0.05f;
        private const float DefaultConstitutionXpPerDamagePoint = 0.02f;
        private const float DefaultConstitutionXpPerMeal = 10f;
        private const float DefaultConstitutionXpPerVitamin = 5f;

        [Header("Core Attributes")] [SerializeField]
        private int strength = MinSkillLevel;

        [SerializeField] private int shooting = MinSkillLevel;
        [SerializeField] private int melee = MinSkillLevel;
        [SerializeField] private int medical = MinSkillLevel;
        [SerializeField] private int engineering = MinSkillLevel;
        [SerializeField] private int toughness = MinSkillLevel;
        [SerializeField] private int constitution = MinSkillLevel;
        [SerializeField] private int agility = MinSkillLevel;
        [SerializeField] private int endurance = MinSkillLevel;
        [SerializeField] private int scavenging = MinSkillLevel;
        [SerializeField] private int stealth = MinSkillLevel;

        [Header("Survival")] [SerializeField] private float stamina = 100f;

        [SerializeField] [Min(1f)] private float baseMaxStamina = 100f;
        [SerializeField] [Min(0f)] private float staminaDrainPerSecondSprint = 15f;
        [SerializeField] [Min(0f)] private float staminaRegenPerSecondIdle = 8f;
        [SerializeField] [Min(0f)] private float staminaRegenPerSecondWalk = 4f;
        [SerializeField] [Min(0f)] private float staminaRegenDelaySeconds = 1.5f;
        [SerializeField] private UnitHealth unitHealth;

        [Header("Data-Driven Tuning")]
        [SerializeField] private AttributeConfiguration attributeConfiguration;

        [Header("Strength Progression")] [SerializeField] [Min(1f)]
        private float strengthXpBaseRequirement = 20f;

        [SerializeField] [Min(0f)] private float strengthXpRequirementGrowthPerLevel = 4f;
        [SerializeField] [Range(1f, 3f)] private float strengthDamageMultiplierAtLevel100 = 3f;
        [SerializeField] [Min(0f)] private float strengthHealthBonusPerLevelPercent = 0.2f;

        [Header("Strength Activities")] [SerializeField] [Range(0f, 1f)]
        private float heavyCarryThreshold01 = 0.7f;

        [SerializeField] [Range(0f, 0.5f)] private float encumbranceSpeedPenaltyMax = 0.35f;
        [SerializeField] [Min(0f)] private float strengthXpPerMeterHeavyCarry = 0.2f;
        [SerializeField] [Min(0f)] private float strengthXpPerWeightedCombatHit = 2.5f;
        [SerializeField] [Min(1f)] private float weightedUnarmedXpMultiplier = 1.15f;
        [SerializeField] [Min(0f)] private float strengthXpPerWeightTrainingRep = 8f;

        [Header("Toughness Progression")] [SerializeField] [Min(1f)]
        private float toughnessXpBaseRequirement = 25f;

        [SerializeField] [Min(0f)] private float toughnessXpRequirementGrowthPerLevel = 5f;
        [SerializeField] [Range(0f, 0.5f)] private float toughnessDamageReductionAtLevel100 = 0.5f;
        [SerializeField] [Min(0f)] private float toughnessHealthBonusPerLevelPercent = 0.1f;
        [SerializeField] [Min(0f)] private float toughnessXpPerDamagePoint = 0.05f;

        [Header("Constitution Progression")] [SerializeField] [Min(1f)]
        private float constitutionXpBaseRequirement = 30f;

        [SerializeField] [Min(0f)] private float constitutionXpRequirementGrowthPerLevel = 6f;
        [SerializeField] [Min(0f)] private float constitutionHealthBonusPerLevelPercent = 0.5f;
        [SerializeField] [Min(0f)] private float constitutionXpPerMeal = 10f;
        [SerializeField] [Min(0f)] private float constitutionXpPerVitamin = 5f;
        [SerializeField] [Min(0f)] private float constitutionXpPerDamagePoint = 0.02f;

        [Header("Shooting Progression")] [SerializeField] [Min(1f)]
        private float shootingXpBaseRequirement = 20f;

        [SerializeField] [Min(0f)] private float shootingXpRequirementGrowthPerLevel = 4f;
        [SerializeField] [Range(1f, 2f)] private float shootingDamageMultiplierAtLevel100 = 1.5f;
        [SerializeField] [Range(0f, 1f)] private float shootingRangeBonusAtLevel100 = 0.5f;
        [SerializeField] [Range(0f, 0.4f)] private float shootingHitChanceBonusAtLevel100 = 0.3f;
        [SerializeField] [Min(0f)] private float shootingXpPerRangedHit = 3f;

        [Header("Melee Progression")] [SerializeField] [Min(1f)]
        private float meleeXpBaseRequirement = 20f;

        [SerializeField] [Min(0f)] private float meleeXpRequirementGrowthPerLevel = 4f;
        [SerializeField] [Range(1f, 2f)] private float meleeDamageMultiplierAtLevel100 = 1.75f;
        [SerializeField] [Min(0f)] private float meleeXpPerHit = 3f;
        [SerializeField] [Range(0f, 0.5f)] private float meleeAttackSpeedBonusAtLevel100 = 0.4f;
        [SerializeField] [Range(0f, 0.5f)] private float meleeKnockbackChanceAtLevel100 = 0.3f;

        [Header("Agility Progression")] [SerializeField] [Min(1f)]
        private float agilityXpBaseRequirement = 22f;

        [SerializeField] [Min(0f)] private float agilityXpRequirementGrowthPerLevel = 4f;
        [SerializeField] [Range(0f, 0.5f)] private float agilityMoveSpeedBonusAtLevel100 = 0.4f;
        [SerializeField] [Range(0f, 0.35f)] private float agilityDodgeBonusAtLevel100 = 0.25f;
        [SerializeField] [Min(0f)] private float agilityXpPerMeterSprinted = 0.1f;

        [Header("Endurance Progression")] [SerializeField] [Min(1f)]
        private float enduranceXpBaseRequirement = 22f;

        [SerializeField] [Min(0f)] private float enduranceXpRequirementGrowthPerLevel = 4f;
        [SerializeField] [Range(0f, 1f)] private float enduranceStaminaBonusAtLevel100 = 1f;
        [SerializeField] [Range(0f, 1f)] private float enduranceRegenBonusAtLevel100 = 0.75f;
        [SerializeField] [Min(0f)] private float enduranceXpPerSecondExertion = 0.05f;

        [Header("Medical Progression")] [SerializeField] [Min(1f)]
        private float medicalXpBaseRequirement = 25f;

        [SerializeField] [Min(0f)] private float medicalXpRequirementGrowthPerLevel = 5f;
        [SerializeField] [Range(0f, 1f)] private float medicalHealBonusAtLevel100 = 0.5f;
        [SerializeField] [Min(0f)] private float medicalXpPerHealPoint = 0.08f;

        [Header("Engineering Progression")] [SerializeField] [Min(1f)]
        private float engineeringXpBaseRequirement = 25f;

        [SerializeField] [Min(0f)] private float engineeringXpRequirementGrowthPerLevel = 5f;
        [SerializeField] [Min(0f)] private float engineeringXpPerPiecePlaced = 12f;
        [SerializeField] [Range(0f, 0.5f)] private float engineeringBuildSpeedBonusAtLevel100 = 0.4f;
        [SerializeField] [Range(0f, 0.35f)] private float strengthKnockbackChanceBonusAtLevel100 = 0.25f;

        [Header("Scavenging Progression")] [SerializeField] [Min(1f)]
        private float scavengingXpBaseRequirement = 18f;

        [SerializeField] [Min(0f)] private float scavengingXpRequirementGrowthPerLevel = 3f;
        [SerializeField] [Range(0f, 1f)] private float scavengingLootBonusAtLevel100 = 0.5f;
        [SerializeField] [Min(0f)] private float scavengingXpPerContainer = 8f;

        [Header("Stealth Progression")] [SerializeField] [Min(1f)]
        private float stealthXpBaseRequirement = 20f;

        [SerializeField] [Min(0f)] private float stealthXpRequirementGrowthPerLevel = 4f;
        [SerializeField] [Range(0f, 0.75f)] private float stealthDetectionRadiusReductionAtLevel100 = 0.6f;
        [SerializeField] [Min(0f)] private float stealthXpPerSecondUndetected = 0.04f;

        [Header("Posture")] [SerializeField] [Range(0f, 2f)]
        private float crouchSpeedAtLevel1 = 0.275f;

        [SerializeField] [Range(0f, 2f)] private float crouchSpeedAtLevel100 = 1.1f;
        [SerializeField] [Range(0f, 2f)] private float crawlSpeedAtLevel1 = 0.14f;
        [SerializeField] [Range(0f, 2f)] private float crawlSpeedAtLevel100 = 0.56f;
        [SerializeField] [Range(0f, 1f)] private float crouchDetectionMultiplier = 0.65f;
        [SerializeField] [Range(0f, 1f)] private float crawlDetectionMultiplier = 0.35f;
        [SerializeField] [Min(1f)] private float crouchStealthXpMultiplier = 2f;
        [SerializeField] [Min(1f)] private float crawlStealthXpMultiplier = 4f;

        [Header("Runtime")] [SerializeField] [Min(0f)]
        private float strengthExperience;

        [SerializeField] [Min(0f)] private float toughnessExperience;
        [SerializeField] [Min(0f)] private float constitutionExperience;
        [SerializeField] [Min(0f)] private float shootingExperience;
        [SerializeField] [Min(0f)] private float meleeExperience;
        [SerializeField] [Min(0f)] private float agilityExperience;
        [SerializeField] [Min(0f)] private float enduranceExperience;
        [SerializeField] [Min(0f)] private float medicalExperience;
        [SerializeField] [Min(0f)] private float engineeringExperience;
        [SerializeField] [Min(0f)] private float scavengingExperience;
        [SerializeField] [Min(0f)] private float stealthExperience;

        [Header("Debug")] [SerializeField] private bool enableLiveXpDebug;

        [SerializeField] private bool logOnlyPlayerXp = true;
        [SerializeField] private bool liveXpDebugCombatSkillsOnly = true;

        // Flat bonuses applied by equipped items. Reflected in GetSkillValue() only.
        private readonly Dictionary<UnitSkillType, int> _equipmentBonuses = new();
        private bool _hasStrengthBaseHealth;

        // Posture state

        private float _strengthBaseMaxHealth = 100f;

        public int Strength => strength;
        public int Shooting => shooting;
        public int Melee => melee;
        public int Medical => medical;
        public int Engineering => engineering;
        public int Toughness => toughness;
        public int Constitution => constitution;
        public int Agility => agility;
        public int Endurance => endurance;
        public int Scavenging => scavenging;
        public int Stealth => stealth;
        public float Stamina => stamina;
        public float MaxStamina => baseMaxStamina * GetEnduranceStaminaMultiplier();
        public float StaminaRatio => MaxStamina > 0f ? Mathf.Clamp01(stamina / MaxStamina) : 0f;
        public float StaminaDrainPerSecondSprint => staminaDrainPerSecondSprint;
        public float StaminaRegenPerSecondIdle => staminaRegenPerSecondIdle * GetEnduranceRegenMultiplier();
        public float StaminaRegenPerSecondWalk => staminaRegenPerSecondWalk * GetEnduranceRegenMultiplier();
        public float StaminaRegenDelaySeconds => staminaRegenDelaySeconds;

        public PostureState CurrentPosture { get; private set; } = PostureState.Upright;

        /// <summary>Fires with (currentStamina, maxStamina) whenever stamina changes.</summary>
        public event Action<float, float> StaminaChanged;

        private void Awake()
        {
            AutoResolveReferences();
            ClampSkillsToBounds();
            CacheStrengthBaseHealthIfNeeded();
            ApplyAllHealthBonuses(false);
        }

    #if UNITY_EDITOR
        private void OnValidate()
        {
            AutoResolveReferences();
            ClampSkillsToBounds();
            strengthExperience = Mathf.Max(0f, strengthExperience);
            strengthDamageMultiplierAtLevel100 = Mathf.Clamp(strengthDamageMultiplierAtLevel100, 1f, 3f);
            weightedUnarmedXpMultiplier = Mathf.Max(1f, weightedUnarmedXpMultiplier);
        }
    #endif
    }

    public enum UnitSkillType
    {
        Strength,
        Shooting,
        Melee,
        Medical,
        Engineering,

        /// <summary>Reduces incoming damage by %. Slight max HP bonus. XP from taking damage.</summary>
        Toughness,

        /// <summary>Primary max HP stat. XP from eating well, vitamins, sustaining damage.</summary>
        Constitution,

        /// <summary>Move speed and dodge. XP from sprinting.</summary>
        Agility,

        /// <summary>Stamina pool and regen. XP from sustained physical exertion.</summary>
        Endurance,

        /// <summary>Loot yield multiplier. XP from searching containers.</summary>
        Scavenging,

        /// <summary>Detection radius reduction. XP from moving undetected.</summary>
        Stealth
    }
}