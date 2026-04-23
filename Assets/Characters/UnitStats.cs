using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Characters
{
    public enum PostureState { Upright, Crouching, Crawling }

    /// <summary>
    /// Stores gameplay attributes used by combat, crafting, and AI behavior decisions.
    /// </summary>
    public sealed class UnitStats : MonoBehaviour
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

        [Header("Core Attributes")]
        [SerializeField] private int strength = MinSkillLevel;
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

        [Header("Survival")]
        [SerializeField] private float stamina = 100f;
        [SerializeField, Min(1f)] private float baseMaxStamina = 100f;
        [SerializeField, Min(0f)] private float staminaDrainPerSecondSprint = 15f;
        [SerializeField, Min(0f)] private float staminaRegenPerSecondIdle = 8f;
        [SerializeField, Min(0f)] private float staminaRegenPerSecondWalk = 4f;
        [SerializeField, Min(0f)] private float staminaRegenDelaySeconds = 1.5f;
        [SerializeField] private UnitHealth unitHealth;

        [Header("Strength Progression")]
        [SerializeField, Min(1f)] private float strengthXpBaseRequirement = 20f;
        [SerializeField, Min(0f)] private float strengthXpRequirementGrowthPerLevel = 4f;
        [SerializeField, Range(1f, 3f)] private float strengthDamageMultiplierAtLevel100 = 3f;
        [SerializeField, Min(0f)] private float strengthHealthBonusPerLevelPercent = 0.2f;

        [Header("Strength Activities")]
        [SerializeField, Range(0f, 1f)] private float heavyCarryThreshold01 = 0.7f;
        [SerializeField, Range(0f, 0.5f)] private float encumbranceSpeedPenaltyMax = 0.35f;
        [SerializeField, Min(0f)] private float strengthXpPerMeterHeavyCarry = 0.2f;
        [SerializeField, Min(0f)] private float strengthXpPerWeightedCombatHit = 2.5f;
        [SerializeField, Min(1f)] private float weightedUnarmedXpMultiplier = 1.15f;
        [SerializeField, Min(0f)] private float strengthXpPerWeightTrainingRep = 8f;

        [Header("Toughness Progression")]
        [SerializeField, Min(1f)] private float toughnessXpBaseRequirement = 25f;
        [SerializeField, Min(0f)] private float toughnessXpRequirementGrowthPerLevel = 5f;
        [SerializeField, Range(0f, 0.5f)] private float toughnessDamageReductionAtLevel100 = 0.5f;
        [SerializeField, Min(0f)] private float toughnessHealthBonusPerLevelPercent = 0.1f;
        [SerializeField, Min(0f)] private float toughnessXpPerDamagePoint = 0.05f;

        [Header("Constitution Progression")]
        [SerializeField, Min(1f)] private float constitutionXpBaseRequirement = 30f;
        [SerializeField, Min(0f)] private float constitutionXpRequirementGrowthPerLevel = 6f;
        [SerializeField, Min(0f)] private float constitutionHealthBonusPerLevelPercent = 0.5f;
        [SerializeField, Min(0f)] private float constitutionXpPerMeal = 10f;
        [SerializeField, Min(0f)] private float constitutionXpPerVitamin = 5f;
        [SerializeField, Min(0f)] private float constitutionXpPerDamagePoint = 0.02f;

        [Header("Shooting Progression")]
        [SerializeField, Min(1f)] private float shootingXpBaseRequirement = 20f;
        [SerializeField, Min(0f)] private float shootingXpRequirementGrowthPerLevel = 4f;
        [SerializeField, Range(1f, 2f)] private float shootingDamageMultiplierAtLevel100 = 1.5f;
        [SerializeField, Range(0f, 1f)] private float shootingRangeBonusAtLevel100 = 0.5f;
        [SerializeField, Range(0f, 0.4f)] private float shootingHitChanceBonusAtLevel100 = 0.3f;
        [SerializeField, Min(0f)] private float shootingXpPerRangedHit = 3f;

        [Header("Melee Progression")]
        [SerializeField, Min(1f)] private float meleeXpBaseRequirement = 20f;
        [SerializeField, Min(0f)] private float meleeXpRequirementGrowthPerLevel = 4f;
        [SerializeField, Range(1f, 2f)] private float meleeDamageMultiplierAtLevel100 = 1.75f;
        [SerializeField, Min(0f)] private float meleeXpPerHit = 3f;
        [SerializeField, Range(0f, 0.5f)] private float meleeAttackSpeedBonusAtLevel100 = 0.4f;
        [SerializeField, Range(0f, 0.5f)] private float meleeKnockbackChanceAtLevel100 = 0.3f;

        [Header("Agility Progression")]
        [SerializeField, Min(1f)] private float agilityXpBaseRequirement = 22f;
        [SerializeField, Min(0f)] private float agilityXpRequirementGrowthPerLevel = 4f;
        [SerializeField, Range(0f, 0.5f)] private float agilityMoveSpeedBonusAtLevel100 = 0.4f;
        [SerializeField, Range(0f, 0.35f)] private float agilityDodgeBonusAtLevel100 = 0.25f;
        [SerializeField, Min(0f)] private float agilityXpPerMeterSprinted = 0.1f;

        [Header("Endurance Progression")]
        [SerializeField, Min(1f)] private float enduranceXpBaseRequirement = 22f;
        [SerializeField, Min(0f)] private float enduranceXpRequirementGrowthPerLevel = 4f;
        [SerializeField, Range(0f, 1f)] private float enduranceStaminaBonusAtLevel100 = 1f;
        [SerializeField, Range(0f, 1f)] private float enduranceRegenBonusAtLevel100 = 0.75f;
        [SerializeField, Min(0f)] private float enduranceXpPerSecondExertion = 0.05f;

        [Header("Medical Progression")]
        [SerializeField, Min(1f)] private float medicalXpBaseRequirement = 25f;
        [SerializeField, Min(0f)] private float medicalXpRequirementGrowthPerLevel = 5f;
        [SerializeField, Range(0f, 1f)] private float medicalHealBonusAtLevel100 = 0.5f;
        [SerializeField, Min(0f)] private float medicalXpPerHealPoint = 0.08f;

        [Header("Engineering Progression")]
        [SerializeField, Min(1f)] private float engineeringXpBaseRequirement = 25f;
        [SerializeField, Min(0f)] private float engineeringXpRequirementGrowthPerLevel = 5f;
        [SerializeField, Min(0f)] private float engineeringXpPerPiecePlaced = 12f;
        [SerializeField, Range(0f, 0.5f)] private float engineeringBuildSpeedBonusAtLevel100 = 0.4f;
        [SerializeField, Range(0f, 0.35f)] private float strengthKnockbackChanceBonusAtLevel100 = 0.25f;

        [Header("Scavenging Progression")]
        [SerializeField, Min(1f)] private float scavengingXpBaseRequirement = 18f;
        [SerializeField, Min(0f)] private float scavengingXpRequirementGrowthPerLevel = 3f;
        [SerializeField, Range(0f, 1f)] private float scavengingLootBonusAtLevel100 = 0.5f;
        [SerializeField, Min(0f)] private float scavengingXpPerContainer = 8f;

        [Header("Stealth Progression")]
        [SerializeField, Min(1f)] private float stealthXpBaseRequirement = 20f;
        [SerializeField, Min(0f)] private float stealthXpRequirementGrowthPerLevel = 4f;
        [SerializeField, Range(0f, 0.75f)] private float stealthDetectionRadiusReductionAtLevel100 = 0.6f;
        [SerializeField, Min(0f)] private float stealthXpPerSecondUndetected = 0.04f;

        [Header("Posture")]
        [SerializeField, Range(0f, 2f)] private float crouchSpeedAtLevel1        = 0.275f;
        [SerializeField, Range(0f, 2f)] private float crouchSpeedAtLevel100      = 1.1f;
        [SerializeField, Range(0f, 2f)] private float crawlSpeedAtLevel1         = 0.14f;
        [SerializeField, Range(0f, 2f)] private float crawlSpeedAtLevel100       = 0.56f;
        [SerializeField, Range(0f, 1f)] private float crouchDetectionMultiplier  = 0.65f;
        [SerializeField, Range(0f, 1f)] private float crawlDetectionMultiplier   = 0.35f;
        [SerializeField, Min(1f)]       private float crouchStealthXpMultiplier  = 2f;
        [SerializeField, Min(1f)]       private float crawlStealthXpMultiplier   = 4f;

        [Header("Runtime")]
        [SerializeField, Min(0f)] private float strengthExperience;
        [SerializeField, Min(0f)] private float toughnessExperience;
        [SerializeField, Min(0f)] private float constitutionExperience;
        [SerializeField, Min(0f)] private float shootingExperience;
        [SerializeField, Min(0f)] private float meleeExperience;
        [SerializeField, Min(0f)] private float agilityExperience;
        [SerializeField, Min(0f)] private float enduranceExperience;
        [SerializeField, Min(0f)] private float medicalExperience;
        [SerializeField, Min(0f)] private float engineeringExperience;
        [SerializeField, Min(0f)] private float scavengingExperience;
        [SerializeField, Min(0f)] private float stealthExperience;

        [Header("Debug")]
        [SerializeField] private bool enableLiveXpDebug = true;
        [SerializeField] private bool logOnlyPlayerXp = true;
        [SerializeField] private bool liveXpDebugCombatSkillsOnly = true;

        private float strengthBaseMaxHealth = 100f;
        private bool hasStrengthBaseHealth;

        // Flat bonuses applied by equipped items. Reflected in GetSkillValue() only.
        private readonly Dictionary<UnitSkillType, int> _equipmentBonuses = new Dictionary<UnitSkillType, int>();

        // Posture state
        private PostureState _postureState = PostureState.Upright;

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
        public float StrengthExperience => strengthExperience;
        public float ToughnessExperience => toughnessExperience;
        public float ConstitutionExperience => constitutionExperience;
        public float ShootingExperience => shootingExperience;
        public float MeleeExperience => meleeExperience;
        public float AgilityExperience => agilityExperience;
        public float EnduranceExperience => enduranceExperience;
        public float MedicalExperience => medicalExperience;
        public float EngineeringExperience => engineeringExperience;
        public float ScavengingExperience => scavengingExperience;
        public float StealthExperience => stealthExperience;

        public int GetSkillLevel(UnitSkillType skillType)
        {
            return skillType switch
            {
                UnitSkillType.Strength => strength,
                UnitSkillType.Shooting => shooting,
                UnitSkillType.Melee => melee,
                UnitSkillType.Medical => medical,
                UnitSkillType.Engineering => engineering,
                UnitSkillType.Toughness => toughness,
                UnitSkillType.Constitution => constitution,
                UnitSkillType.Agility => agility,
                UnitSkillType.Endurance => endurance,
                UnitSkillType.Scavenging => scavenging,
                UnitSkillType.Stealth => stealth,
                _ => MinSkillLevel
            };
        }

        public float GetCurrentExperience(UnitSkillType skillType)
        {
            return skillType switch
            {
                UnitSkillType.Strength => strengthExperience,
                UnitSkillType.Shooting => shootingExperience,
                UnitSkillType.Melee => meleeExperience,
                UnitSkillType.Medical => medicalExperience,
                UnitSkillType.Engineering => engineeringExperience,
                UnitSkillType.Toughness => toughnessExperience,
                UnitSkillType.Constitution => constitutionExperience,
                UnitSkillType.Agility => agilityExperience,
                UnitSkillType.Endurance => enduranceExperience,
                UnitSkillType.Scavenging => scavengingExperience,
                UnitSkillType.Stealth => stealthExperience,
                _ => 0f
            };
        }

        public float GetExperienceRequiredForNextLevel(UnitSkillType skillType)
        {
            if (!TryGetProgressionParameters(skillType, out int level, out _, out float baseReq, out float growthPerLevel))
            {
                return 0f;
            }

            if (level >= MaxSkillLevel)
            {
                return 0f;
            }

            return baseReq + (level - MinSkillLevel) * growthPerLevel;
        }

        public float GetTotalExperienceEarned(UnitSkillType skillType)
        {
            if (!TryGetProgressionParameters(skillType, out int level, out float currentXp, out float baseReq, out float growthPerLevel))
            {
                return 0f;
            }

            int levelsGained = Mathf.Max(0, level - MinSkillLevel);
            float spentXp = levelsGained * baseReq;
            spentXp += ((levelsGained - 1) * levelsGained * 0.5f) * growthPerLevel;
            return Mathf.Max(0f, spentXp + currentXp);
        }

        public event Action<int> StrengthLeveledUp;
        public event Action<int> ToughnessLeveledUp;
        public event Action<int> ConstitutionLeveledUp;
        public event Action<int> ShootingLeveledUp;
        public event Action<int> MeleeLeveledUp;
        public event Action<int> AgilityLeveledUp;
        public event Action<int> EnduranceLeveledUp;
        public event Action<int> MedicalLeveledUp;
        public event Action<int> EngineeringLeveledUp;
        public event Action<int> ScavengingLeveledUp;
        public event Action<int> StealthLeveledUp;
        /// <summary>Fires with (currentStamina, maxStamina) whenever stamina changes.</summary>
        public event Action<float, float> StaminaChanged;
        private void Awake()
        {
            AutoResolveReferences();
            ClampSkillsToBounds();
            CacheStrengthBaseHealthIfNeeded();
            ApplyAllHealthBonuses(refillCurrentHealth: false);
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

        public int GetSkillValue(UnitSkillType skillType)
        {
            int bonus = _equipmentBonuses.TryGetValue(skillType, out int b) ? b : 0;
            switch (skillType)
            {
                case UnitSkillType.Strength:
                    return Mathf.Clamp(strength + bonus, MinSkillLevel, MaxSkillLevel);
                case UnitSkillType.Shooting:
                    return Mathf.Clamp(shooting + bonus, MinSkillLevel, MaxSkillLevel);
                case UnitSkillType.Melee:
                    return Mathf.Clamp(melee + bonus, MinSkillLevel, MaxSkillLevel);
                case UnitSkillType.Medical:
                    return Mathf.Clamp(medical + bonus, MinSkillLevel, MaxSkillLevel);
                case UnitSkillType.Engineering:
                    return Mathf.Clamp(engineering + bonus, MinSkillLevel, MaxSkillLevel);
                case UnitSkillType.Toughness:
                    return Mathf.Clamp(toughness + bonus, MinSkillLevel, MaxSkillLevel);
                case UnitSkillType.Constitution:
                    return Mathf.Clamp(constitution + bonus, MinSkillLevel, MaxSkillLevel);
                case UnitSkillType.Agility:
                    return Mathf.Clamp(agility + bonus, MinSkillLevel, MaxSkillLevel);
                case UnitSkillType.Endurance:
                    return Mathf.Clamp(endurance + bonus, MinSkillLevel, MaxSkillLevel);
                case UnitSkillType.Scavenging:
                    return Mathf.Clamp(scavenging + bonus, MinSkillLevel, MaxSkillLevel);
                case UnitSkillType.Stealth:
                    return Mathf.Clamp(stealth + bonus, MinSkillLevel, MaxSkillLevel);
                default:
                    return 0;
            }
        }

        /// <summary>Adds a flat equipment bonus to a skill (clamped in <see cref="GetSkillValue"/>).</summary>
        public void AddEquipmentBonus(UnitSkillType skill, int flat)
        {
            if (flat == 0) return;
            _equipmentBonuses.TryGetValue(skill, out int current);
            _equipmentBonuses[skill] = current + flat;
        }

        /// <summary>Removes a previously applied flat equipment bonus from a skill.</summary>
        public void RemoveEquipmentBonus(UnitSkillType skill, int flat)
        {
            if (flat == 0 || !_equipmentBonuses.TryGetValue(skill, out int current)) return;
            int newVal = current - flat;
            if (newVal == 0)
                _equipmentBonuses.Remove(skill);
            else
                _equipmentBonuses[skill] = newVal;
        }

        public void SetSkill(UnitSkillType skillType, int value)
        {
            int clampedValue = Mathf.Clamp(value, MinSkillLevel, MaxSkillLevel);
            bool healthScalingSkillChanged = false;

            switch (skillType)
            {
                case UnitSkillType.Strength:
                    healthScalingSkillChanged = strength != clampedValue;
                    strength = clampedValue;
                    break;
                case UnitSkillType.Shooting:
                    shooting = clampedValue;
                    break;
                case UnitSkillType.Melee:
                    melee = clampedValue;
                    break;
                case UnitSkillType.Medical:
                    medical = clampedValue;
                    break;
                case UnitSkillType.Engineering:
                    engineering = clampedValue;
                    break;
                case UnitSkillType.Toughness:
                    healthScalingSkillChanged = healthScalingSkillChanged || toughness != clampedValue;
                    toughness = clampedValue;
                    break;
                case UnitSkillType.Constitution:
                    healthScalingSkillChanged = healthScalingSkillChanged || constitution != clampedValue;
                    constitution = clampedValue;
                    break;
                case UnitSkillType.Agility:
                    agility = clampedValue;
                    break;
                case UnitSkillType.Endurance:
                    endurance = clampedValue;
                    break;
                case UnitSkillType.Scavenging:
                    scavenging = clampedValue;
                    break;
                case UnitSkillType.Stealth:
                    stealth = clampedValue;
                    break;
            }

            if (healthScalingSkillChanged)
            {
                ApplyAllHealthBonuses(refillCurrentHealth: false);
            }
        }

        /// <summary>
        /// Restores a skill's level and current XP progress from saved runtime data.
        /// XP is clamped to the valid range for the provided level.
        /// </summary>
        public void SetSkillProgress(UnitSkillType skillType, int level, float currentExperience)
        {
            int clampedLevel = Mathf.Clamp(level, MinSkillLevel, MaxSkillLevel);
            float clampedXp = Mathf.Max(0f, currentExperience);

            if (clampedLevel >= MaxSkillLevel)
            {
                clampedXp = 0f;
            }
            else
            {
                float maxXpForLevel = GetXpRequiredForLevel(skillType, clampedLevel);
                if (maxXpForLevel > 0f)
                {
                    clampedXp = Mathf.Clamp(clampedXp, 0f, maxXpForLevel - 0.0001f);
                }
                else
                {
                    clampedXp = 0f;
                }
            }

            bool healthScalingSkillChanged = false;

            switch (skillType)
            {
                case UnitSkillType.Strength:
                    healthScalingSkillChanged = strength != clampedLevel;
                    strength = clampedLevel;
                    strengthExperience = clampedXp;
                    break;
                case UnitSkillType.Shooting:
                    shooting = clampedLevel;
                    shootingExperience = clampedXp;
                    break;
                case UnitSkillType.Melee:
                    melee = clampedLevel;
                    meleeExperience = clampedXp;
                    break;
                case UnitSkillType.Medical:
                    medical = clampedLevel;
                    medicalExperience = clampedXp;
                    break;
                case UnitSkillType.Engineering:
                    engineering = clampedLevel;
                    engineeringExperience = clampedXp;
                    break;
                case UnitSkillType.Toughness:
                    healthScalingSkillChanged = healthScalingSkillChanged || toughness != clampedLevel;
                    toughness = clampedLevel;
                    toughnessExperience = clampedXp;
                    break;
                case UnitSkillType.Constitution:
                    healthScalingSkillChanged = healthScalingSkillChanged || constitution != clampedLevel;
                    constitution = clampedLevel;
                    constitutionExperience = clampedXp;
                    break;
                case UnitSkillType.Agility:
                    agility = clampedLevel;
                    agilityExperience = clampedXp;
                    break;
                case UnitSkillType.Endurance:
                    endurance = clampedLevel;
                    enduranceExperience = clampedXp;
                    break;
                case UnitSkillType.Scavenging:
                    scavenging = clampedLevel;
                    scavengingExperience = clampedXp;
                    break;
                case UnitSkillType.Stealth:
                    stealth = clampedLevel;
                    stealthExperience = clampedXp;
                    break;
            }

            if (healthScalingSkillChanged)
            {
                ApplyAllHealthBonuses(refillCurrentHealth: false);
            }
        }

        public void SetStamina(float value)
        {
            baseMaxStamina = Mathf.Max(1f, value);
            stamina = baseMaxStamina;
            StaminaChanged?.Invoke(stamina, MaxStamina);
        }

        public void DrainStamina(float amount)
        {
            if (amount <= 0f) return;
            stamina = Mathf.Max(0f, stamina - amount);
            StaminaChanged?.Invoke(stamina, MaxStamina);
        }

        public void RegenStamina(float amount)
        {
            if (amount <= 0f) return;
            float max = MaxStamina;
            stamina = Mathf.Min(max, stamina + amount);
            StaminaChanged?.Invoke(stamina, max);
        }

        public void ResetAllSkillsToLevelOne()
        {
            strength = MinSkillLevel;
            shooting = MinSkillLevel;
            melee = MinSkillLevel;
            medical = MinSkillLevel;
            engineering = MinSkillLevel;
            toughness = MinSkillLevel;
            agility = MinSkillLevel;
            endurance = MinSkillLevel;
            scavenging = MinSkillLevel;
            stealth = MinSkillLevel;
            strengthExperience = 0f;
            toughnessExperience = 0f;
            constitutionExperience = 0f;
            shootingExperience = 0f;
            meleeExperience = 0f;
            agilityExperience = 0f;
            enduranceExperience = 0f;
            medicalExperience = 0f;
            engineeringExperience = 0f;
            scavengingExperience = 0f;
            stealthExperience = 0f;
            ApplyAllHealthBonuses(refillCurrentHealth: false);
        }

        public bool AddStrengthExperience(float amount)
        {
            if (amount <= 0f)
            {
                LogXpDebug(UnitSkillType.Strength, $"SKIP add XP amount={amount:0.###}");
                return false;
            }

            if (strength >= MaxSkillLevel)
            {
                LogXpDebug(UnitSkillType.Strength, $"SKIP add XP at max level {strength}");
                return false;
            }

            return AddExperience(UnitSkillType.Strength, ref strengthExperience, ref strength, amount,
                strengthXpBaseRequirement, strengthXpRequirementGrowthPerLevel,
                newLevel => { StrengthLeveledUp?.Invoke(newLevel); ApplyAllHealthBonuses(false); });
        }

        public bool AddToughnessExperience(float amount)
        {
            if (amount <= 0f)
            {
                LogXpDebug(UnitSkillType.Toughness, $"SKIP add XP amount={amount:0.###}");
                return false;
            }

            if (toughness >= MaxSkillLevel)
            {
                LogXpDebug(UnitSkillType.Toughness, $"SKIP add XP at max level {toughness}");
                return false;
            }

            return AddExperience(UnitSkillType.Toughness, ref toughnessExperience, ref toughness, amount,
                toughnessXpBaseRequirement, toughnessXpRequirementGrowthPerLevel,
                newLevel => { ToughnessLeveledUp?.Invoke(newLevel); ApplyAllHealthBonuses(false); });
        }

        public bool AddConstitutionExperience(float amount)
        {
            if (amount <= 0f)
            {
                LogXpDebug(UnitSkillType.Constitution, $"SKIP add XP amount={amount:0.###}");
                return false;
            }

            if (constitution >= MaxSkillLevel)
            {
                LogXpDebug(UnitSkillType.Constitution, $"SKIP add XP at max level {constitution}");
                return false;
            }

            return AddExperience(UnitSkillType.Constitution, ref constitutionExperience, ref constitution, amount,
                constitutionXpBaseRequirement, constitutionXpRequirementGrowthPerLevel,
                newLevel => { ConstitutionLeveledUp?.Invoke(newLevel); ApplyAllHealthBonuses(false); });
        }

        public bool AddShootingExperience(float amount)
        {
            if (amount <= 0f)
            {
                LogXpDebug(UnitSkillType.Shooting, $"SKIP add XP amount={amount:0.###}");
                return false;
            }

            if (shooting >= MaxSkillLevel)
            {
                LogXpDebug(UnitSkillType.Shooting, $"SKIP add XP at max level {shooting}");
                return false;
            }

            return AddExperience(UnitSkillType.Shooting, ref shootingExperience, ref shooting, amount,
                shootingXpBaseRequirement, shootingXpRequirementGrowthPerLevel,
                newLevel => ShootingLeveledUp?.Invoke(newLevel));
        }

        public bool AddMeleeExperience(float amount)
        {
            if (amount <= 0f)
            {
                LogXpDebug(UnitSkillType.Melee, $"SKIP add XP amount={amount:0.###}");
                return false;
            }

            if (melee >= MaxSkillLevel)
            {
                LogXpDebug(UnitSkillType.Melee, $"SKIP add XP at max level {melee}");
                return false;
            }

            return AddExperience(UnitSkillType.Melee, ref meleeExperience, ref melee, amount,
                meleeXpBaseRequirement, meleeXpRequirementGrowthPerLevel,
                newLevel => MeleeLeveledUp?.Invoke(newLevel));
        }

        public bool AddAgilityExperience(float amount)
        {
            if (amount <= 0f || agility >= MaxSkillLevel) return false;
            return AddExperience(UnitSkillType.Agility, ref agilityExperience, ref agility, amount,
                agilityXpBaseRequirement, agilityXpRequirementGrowthPerLevel,
                newLevel => AgilityLeveledUp?.Invoke(newLevel));
        }

        public bool AddEnduranceExperience(float amount)
        {
            if (amount <= 0f || endurance >= MaxSkillLevel) return false;
            return AddExperience(UnitSkillType.Endurance, ref enduranceExperience, ref endurance, amount,
                enduranceXpBaseRequirement, enduranceXpRequirementGrowthPerLevel,
                newLevel => EnduranceLeveledUp?.Invoke(newLevel));
        }

        public bool AddScavengingExperience(float amount)
        {
            if (amount <= 0f || scavenging >= MaxSkillLevel) return false;
            return AddExperience(UnitSkillType.Scavenging, ref scavengingExperience, ref scavenging, amount,
                scavengingXpBaseRequirement, scavengingXpRequirementGrowthPerLevel,
                newLevel => ScavengingLeveledUp?.Invoke(newLevel));
        }

        public bool AddStealthExperience(float amount)
        {
            if (amount <= 0f || stealth >= MaxSkillLevel) return false;
            return AddExperience(UnitSkillType.Stealth, ref stealthExperience, ref stealth, amount,
                stealthXpBaseRequirement, stealthXpRequirementGrowthPerLevel,
                newLevel => StealthLeveledUp?.Invoke(newLevel));
        }

        public bool AddMedicalExperience(float amount)
        {
            if (amount <= 0f || medical >= MaxSkillLevel) return false;
            return AddExperience(UnitSkillType.Medical, ref medicalExperience, ref medical, amount,
                medicalXpBaseRequirement, medicalXpRequirementGrowthPerLevel,
                newLevel => MedicalLeveledUp?.Invoke(newLevel));
        }

        public bool AddEngineeringExperience(float amount)
        {
            if (amount <= 0f || engineering >= MaxSkillLevel) return false;
            return AddExperience(UnitSkillType.Engineering, ref engineeringExperience, ref engineering, amount,
                engineeringXpBaseRequirement, engineeringXpRequirementGrowthPerLevel,
                newLevel => EngineeringLeveledUp?.Invoke(newLevel));
        }

        // ── Activity recording ────────────────────────────────────────────────

        public void RecordWeightTrainingRep(float intensity = 1f)
        {
            float clampedIntensity = Mathf.Max(0.1f, intensity);
            float xpPerRep = strengthXpPerWeightTrainingRep > 0f ? strengthXpPerWeightTrainingRep : DefaultStrengthXpPerWeightTrainingRep;
            AddStrengthExperience(xpPerRep * clampedIntensity);
        }

        public void RecordHeavyCarryWalkDistance(float distanceMeters, float carryRatio01)
        {
            if (distanceMeters <= 0f) return;
            float normalizedCarryRatio = Mathf.Clamp01(carryRatio01);
            if (normalizedCarryRatio < heavyCarryThreshold01) return;
            float overload = Mathf.InverseLerp(heavyCarryThreshold01, 1f, normalizedCarryRatio);
            float xpMultiplier = Mathf.Lerp(0.5f, 1.5f, overload);
            float xpPerMeter = strengthXpPerMeterHeavyCarry > 0f ? strengthXpPerMeterHeavyCarry : DefaultStrengthXpPerMeterHeavyCarry;
            AddStrengthExperience(distanceMeters * xpPerMeter * xpMultiplier);
        }

        public void RecordWeightedCombatHit(bool armed)
        {
            float xp = strengthXpPerWeightedCombatHit > 0f ? strengthXpPerWeightedCombatHit : DefaultStrengthXpPerWeightedCombatHit;
            if (!armed) xp *= weightedUnarmedXpMultiplier;
            LogXpDebug(UnitSkillType.Strength, $"RecordWeightedCombatHit armed={armed} xp={xp:0.###}");
            AddStrengthExperience(xp);
        }

        /// <summary>Called when this unit takes damage. Awards Toughness and Constitution XP.</summary>
        public void RecordDamageTaken(float damageAmount)
        {
            if (damageAmount <= 0f) return;
            float toughnessRate = toughnessXpPerDamagePoint > 0f ? toughnessXpPerDamagePoint : DefaultToughnessXpPerDamagePoint;
            float constitutionRate = constitutionXpPerDamagePoint > 0f ? constitutionXpPerDamagePoint : DefaultConstitutionXpPerDamagePoint;
            LogXpDebug(UnitSkillType.Toughness, $"RecordDamageTaken damage={damageAmount:0.###} toughnessRate={toughnessRate:0.###} constitutionRate={constitutionRate:0.###}");
            AddToughnessExperience(damageAmount * toughnessRate);
            AddConstitutionExperience(damageAmount * constitutionRate);
        }

        /// <summary>Call when the unit eats a nutritious meal.</summary>
        public void RecordMealConsumed(float quality = 1f)
        {
            float xpPerMeal = constitutionXpPerMeal > 0f ? constitutionXpPerMeal : DefaultConstitutionXpPerMeal;
            AddConstitutionExperience(xpPerMeal * Mathf.Max(0.1f, quality));
        }

        /// <summary>Call when the unit consumes a vitamin supplement.</summary>
        public void RecordVitaminConsumed()
        {
            float xpPerVitamin = constitutionXpPerVitamin > 0f ? constitutionXpPerVitamin : DefaultConstitutionXpPerVitamin;
            AddConstitutionExperience(xpPerVitamin);
        }

        /// <summary>Call each frame the unit is sprinting (pass Time.deltaTime * distanceThisFrame).</summary>
        public void RecordSprintDistance(float distanceMeters)
        {
            if (distanceMeters <= 0f) return;
            float xpPerMeter = agilityXpPerMeterSprinted > 0f ? agilityXpPerMeterSprinted : DefaultAgilityXpPerMeterSprinted;
            AddAgilityExperience(distanceMeters * xpPerMeter);
        }

        /// <summary>Call while the unit is physically exerting (pass seconds elapsed).</summary>
        public void RecordExertionTime(float seconds)
        {
            if (seconds <= 0f) return;
            float xpPerSecond = enduranceXpPerSecondExertion > 0f ? enduranceXpPerSecondExertion : DefaultEnduranceXpPerSecondExertion;
            AddEnduranceExperience(seconds * xpPerSecond);
        }

        /// <summary>Call when the unit searches/loots a container.</summary>
        public void RecordContainerSearched()
        {
            float xpPerContainer = scavengingXpPerContainer > 0f ? scavengingXpPerContainer : DefaultScavengingXpPerContainer;
            AddScavengingExperience(xpPerContainer);
        }

        /// <summary>Call each second the unit moves while undetected.</summary>
        public void RecordUndetectedTime(float seconds)
        {
            if (seconds <= 0f) return;
            float xpPerSecond = stealthXpPerSecondUndetected > 0f ? stealthXpPerSecondUndetected : DefaultStealthXpPerSecondUndetected;
            AddStealthExperience(seconds * xpPerSecond * GetPostureStealthXpMultiplier());
        }

        /// <summary>Call each frame unit lands a ranged hit.</summary>
        public void RecordRangedHit()
        {
            float xpPerHit = shootingXpPerRangedHit > 0f ? shootingXpPerRangedHit : DefaultShootingXpPerRangedHit;
            LogXpDebug(UnitSkillType.Shooting, $"RecordRangedHit xp={xpPerHit:0.###}");
            AddShootingExperience(xpPerHit);
        }

        /// <summary>Call each frame unit lands a melee hit.</summary>
        public void RecordMeleeHit()
        {
            float xpPerHit = meleeXpPerHit > 0f ? meleeXpPerHit : DefaultMeleeXpPerHit;
            LogXpDebug(UnitSkillType.Melee, $"RecordMeleeHit xp={xpPerHit:0.###}");
            AddMeleeExperience(xpPerHit);
        }

        /// <summary>Call when this unit applies healing to another unit.</summary>
        public void RecordHealApplied(float healAmount)
        {
            if (healAmount <= 0f) return;
            float xpPerHealPoint = medicalXpPerHealPoint > 0f ? medicalXpPerHealPoint : DefaultMedicalXpPerHealPoint;
            AddMedicalExperience(healAmount * xpPerHealPoint);
        }

        /// <summary>Call when this unit places a build piece.</summary>
        public void RecordBuildPiecePlaced()
        {
            float xpPerPiece = engineeringXpPerPiecePlaced > 0f ? engineeringXpPerPiecePlaced : DefaultEngineeringXpPerPiecePlaced;
            AddEngineeringExperience(xpPerPiece);
        }

        // ── Effect getters ────────────────────────────────────────────────────

        public bool IsHeavyCarry(float carryRatio01)
        {
            return Mathf.Clamp01(carryRatio01) >= heavyCarryThreshold01;
        }

        public float ApplyStrengthDamageScaling(float baseDamage)
        {
            return Mathf.Max(0f, baseDamage) * GetStrengthDamageMultiplier();
        }

        public float ApplyShootingDamageScaling(float baseDamage)
        {
            float t = SkillT(shooting);
            return Mathf.Max(0f, baseDamage) * Mathf.Lerp(1f, shootingDamageMultiplierAtLevel100, t);
        }

        public float ApplyMeleeDamageScaling(float baseDamage)
        {
            float t = SkillT(melee);
            return Mathf.Max(0f, baseDamage) * Mathf.Lerp(1f, meleeDamageMultiplierAtLevel100, t);
        }

        /// <summary>Attack-speed multiplier from Melee. Divide cooldown by this. 1.0 at level 1, up to 1.4 at level 100.</summary>
        public float GetMeleeAttackSpeedMultiplier()
        {
            return 1f + Mathf.Lerp(0f, meleeAttackSpeedBonusAtLevel100, SkillT(melee));
        }

        /// <summary>Knockback chance from Melee skill. 0 at level 1, up to 30% at level 100.</summary>
        public float GetMeleeKnockbackChance()
        {
            return Mathf.Lerp(0f, meleeKnockbackChanceAtLevel100, SkillT(melee));
        }

        /// <summary>Additional knockback chance from Strength. 0 at level 1, up to 25% at level 100.</summary>
        public float GetStrengthKnockbackChanceBonus()
        {
            return Mathf.Lerp(0f, strengthKnockbackChanceBonusAtLevel100, SkillT(strength));
        }

        /// <summary>Build-speed multiplier from Engineering. 1.0 at level 1, up to 1.4 at level 100.</summary>
        public float GetEngineeringBuildSpeedMultiplier()
        {
            return 1f + Mathf.Lerp(0f, engineeringBuildSpeedBonusAtLevel100, SkillT(engineering));
        }

        /// <summary>Returns 0–50% damage reduction from Toughness.</summary>
        public float GetToughnessDamageReduction()
        {
            return Mathf.Lerp(0f, toughnessDamageReductionAtLevel100, SkillT(toughness));
        }

        /// <summary>Apply Toughness damage reduction. Returns the actual damage to deal.</summary>
        public float ApplyToughnessDamageReduction(float incomingDamage)
        {
            float reduction = GetToughnessDamageReduction();
            return Mathf.Max(0f, incomingDamage * (1f - reduction));
        }

        /// <summary>Move speed multiplier from Agility. 1.0 at level 1, up to 1.4 at level 100.</summary>
        public float GetAgilityMoveSpeedMultiplier()
        {
            return 1f + Mathf.Lerp(0f, agilityMoveSpeedBonusAtLevel100, SkillT(agility));
        }

        /// <summary>Dodge chance from Agility. 0 at level 1, up to 25% at level 100.</summary>
        public float GetAgilityDodgeChance()
        {
            return Mathf.Lerp(0f, agilityDodgeBonusAtLevel100, SkillT(agility));
        }

        /// <summary>Max stamina multiplier from Endurance. 1.0 at level 1, up to 2.0 at level 100.</summary>
        public float GetEnduranceStaminaMultiplier()
        {
            return 1f + Mathf.Lerp(0f, enduranceStaminaBonusAtLevel100, SkillT(endurance));
        }

        /// <summary>Stamina regen rate multiplier from Endurance. 1.0 at level 1, up to 1.75 at level 100.</summary>
        public float GetEnduranceRegenMultiplier()
        {
            return 1f + Mathf.Lerp(0f, enduranceRegenBonusAtLevel100, SkillT(endurance));
        }

        /// <summary>Heal-amount multiplier from Medical. 1.0 at level 1, up to 1.5 at level 100.</summary>
        public float GetMedicalHealMultiplier()
        {
            return 1f + Mathf.Lerp(0f, medicalHealBonusAtLevel100, SkillT(medical));
        }

        /// <summary>Loot yield multiplier from Scavenging. 1.0 at level 1, up to 1.5 at level 100.</summary>
        public float GetScavengingLootMultiplier()
        {
            return 1f + Mathf.Lerp(0f, scavengingLootBonusAtLevel100, SkillT(scavenging));
        }

        /// <summary>
        /// Speed penalty from encumbrance. Returns 1.0 when carry is below the heavy threshold,
        /// down to (1 - encumbranceSpeedPenaltyMax) at 100% carry weight.
        /// </summary>
        public float GetEncumbranceSpeedMultiplier(float carryRatio01)
        {
            float ratio = Mathf.Clamp01(carryRatio01);
            if (ratio < heavyCarryThreshold01) return 1f;
            float overload = Mathf.InverseLerp(heavyCarryThreshold01, 1f, ratio);
            return 1f - Mathf.Lerp(0f, encumbranceSpeedPenaltyMax, overload);
        }

        /// <summary>Detection radius multiplier from Stealth. 1.0 at level 1, down to 0.4 at level 100.</summary>
        public float GetStealthDetectionRadiusMultiplier()
        {
            return 1f - Mathf.Lerp(0f, stealthDetectionRadiusReductionAtLevel100, SkillT(stealth));
        }

        /// <summary>Additional detection-radius multiplier from current posture (crouch/crawl). Stacks with stealth skill.</summary>
        public float GetPostureDetectionMultiplier()
        {
            return _postureState switch
            {
                PostureState.Crouching => crouchDetectionMultiplier,
                PostureState.Crawling  => crawlDetectionMultiplier,
                _                     => 1f,
            };
        }

        /// <summary>Move speed multiplier applied by posture, scaled by stealth level.</summary>
        public float GetPostureSpeedMultiplier()
        {
            float t = SkillT(stealth);
            return _postureState switch
            {
                PostureState.Crouching => Mathf.Lerp(crouchSpeedAtLevel1, crouchSpeedAtLevel100, t),
                PostureState.Crawling  => Mathf.Lerp(crawlSpeedAtLevel1,  crawlSpeedAtLevel100,  t),
                _                     => 1f,
            };
        }

        /// <summary>Set the current posture. Returns the speed multiplier the caller should apply.</summary>
        public float SetPostureState(PostureState state)
        {
            _postureState = state;
            return GetPostureSpeedMultiplier();
        }

        public PostureState CurrentPosture => _postureState;

        /// <summary>XP rate multiplier used when the unit evades detection while in this posture.</summary>
        public float GetPostureStealthXpMultiplier()
        {
            return _postureState switch
            {
                PostureState.Crouching => crouchStealthXpMultiplier,
                PostureState.Crawling  => crawlStealthXpMultiplier,
                _                     => 1f,
            };
        }

        /// <summary>Effective range multiplier from Shooting. 1.0 at level 1, up to 1.5 at level 100.</summary>
        public float GetShootingEffectiveRangeMultiplier()
        {
            return 1f + Mathf.Lerp(0f, shootingRangeBonusAtLevel100, SkillT(shooting));
        }

        /// <summary>
        /// Hit-chance bonus from Shooting skill that reduces range-accuracy falloff.
        /// 0 at level 1, up to +0.3 (30 percentage points) at level 100.
        /// </summary>
        public float GetShootingHitChanceBonus()
        {
            return Mathf.Lerp(0f, shootingHitChanceBonusAtLevel100, SkillT(shooting));
        }

        public float GetStrengthDamageMultiplier()
        {
            return Mathf.Lerp(1f, strengthDamageMultiplierAtLevel100, SkillT(strength));
        }

        public void SetStrengthBaseHealth(float baseMaxHealth, bool refillCurrentHealth)
        {
            strengthBaseMaxHealth = Mathf.Max(1f, baseMaxHealth);
            hasStrengthBaseHealth = true;
            ApplyAllHealthBonuses(refillCurrentHealth);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private float SkillT(int skillValue)
        {
            return (Mathf.Clamp(skillValue, MinSkillLevel, MaxSkillLevel) - MinSkillLevel) / (float)(MaxSkillLevel - MinSkillLevel);
        }

        private bool TryGetProgressionParameters(
            UnitSkillType skillType,
            out int level,
            out float currentXp,
            out float baseReq,
            out float growthPerLevel)
        {
            switch (skillType)
            {
                case UnitSkillType.Strength:
                    level = strength;
                    currentXp = strengthExperience;
                    baseReq = strengthXpBaseRequirement;
                    growthPerLevel = strengthXpRequirementGrowthPerLevel;
                    return true;
                case UnitSkillType.Shooting:
                    level = shooting;
                    currentXp = shootingExperience;
                    baseReq = shootingXpBaseRequirement;
                    growthPerLevel = shootingXpRequirementGrowthPerLevel;
                    return true;
                case UnitSkillType.Melee:
                    level = melee;
                    currentXp = meleeExperience;
                    baseReq = meleeXpBaseRequirement;
                    growthPerLevel = meleeXpRequirementGrowthPerLevel;
                    return true;
                case UnitSkillType.Medical:
                    level = medical;
                    currentXp = medicalExperience;
                    baseReq = medicalXpBaseRequirement;
                    growthPerLevel = medicalXpRequirementGrowthPerLevel;
                    return true;
                case UnitSkillType.Engineering:
                    level = engineering;
                    currentXp = engineeringExperience;
                    baseReq = engineeringXpBaseRequirement;
                    growthPerLevel = engineeringXpRequirementGrowthPerLevel;
                    return true;
                case UnitSkillType.Toughness:
                    level = toughness;
                    currentXp = toughnessExperience;
                    baseReq = toughnessXpBaseRequirement;
                    growthPerLevel = toughnessXpRequirementGrowthPerLevel;
                    return true;
                case UnitSkillType.Constitution:
                    level = constitution;
                    currentXp = constitutionExperience;
                    baseReq = constitutionXpBaseRequirement;
                    growthPerLevel = constitutionXpRequirementGrowthPerLevel;
                    return true;
                case UnitSkillType.Agility:
                    level = agility;
                    currentXp = agilityExperience;
                    baseReq = agilityXpBaseRequirement;
                    growthPerLevel = agilityXpRequirementGrowthPerLevel;
                    return true;
                case UnitSkillType.Endurance:
                    level = endurance;
                    currentXp = enduranceExperience;
                    baseReq = enduranceXpBaseRequirement;
                    growthPerLevel = enduranceXpRequirementGrowthPerLevel;
                    return true;
                case UnitSkillType.Scavenging:
                    level = scavenging;
                    currentXp = scavengingExperience;
                    baseReq = scavengingXpBaseRequirement;
                    growthPerLevel = scavengingXpRequirementGrowthPerLevel;
                    return true;
                case UnitSkillType.Stealth:
                    level = stealth;
                    currentXp = stealthExperience;
                    baseReq = stealthXpBaseRequirement;
                    growthPerLevel = stealthXpRequirementGrowthPerLevel;
                    return true;
                default:
                    level = MinSkillLevel;
                    currentXp = 0f;
                    baseReq = 0f;
                    growthPerLevel = 0f;
                    return false;
            }
        }

        private bool AddExperience(UnitSkillType skillType, ref float xp, ref int level, float amount,
            float baseReq, float growthPerLevel, Action<int> onLevelUp)
        {
            float startingXp = xp;
            int startingLevel = level;
            xp += amount;
            bool leveledUp = false;

            while (level < MaxSkillLevel)
            {
                float required = baseReq + (level - MinSkillLevel) * growthPerLevel;
                if (xp < required) break;
                xp -= required;
                level++;
                leveledUp = true;
                onLevelUp?.Invoke(level);
            }

            if (level >= MaxSkillLevel)
            {
                level = MaxSkillLevel;
                xp = 0f;
            }

            LogXpDebug(skillType,
                $"Applied +{amount:0.###} XP | level {startingLevel}->{level} | currentXP {startingXp:0.###}->{xp:0.###} | leveled={leveledUp}");

            return leveledUp;
        }

        private void LogXpDebug(UnitSkillType skillType, string message)
        {
            if (!ShouldLogXpDebug(skillType))
            {
                return;
            }

            Unit unit = GetComponent<Unit>();
            string unitName = unit != null ? unit.gameObject.name : gameObject.name;
            string role = unit != null ? unit.Role.ToString() : "UnknownRole";
            Debug.Log($"[XP DEBUG][UnitStats][{unitName}][{role}][{skillType}] {message}", this);
        }

        private bool ShouldLogXpDebug(UnitSkillType skillType)
        {
            if (!enableLiveXpDebug)
            {
                return false;
            }

            if (liveXpDebugCombatSkillsOnly && !IsCombatSkill(skillType))
            {
                return false;
            }

            if (!logOnlyPlayerXp)
            {
                return true;
            }

            Unit unit = GetComponent<Unit>();
            return unit != null && unit.Role == UnitRole.Player;
        }

        private static bool IsCombatSkill(UnitSkillType skillType)
        {
            switch (skillType)
            {
                case UnitSkillType.Strength:
                case UnitSkillType.Shooting:
                case UnitSkillType.Melee:
                case UnitSkillType.Toughness:
                case UnitSkillType.Constitution:
                    return true;
                default:
                    return false;
            }
        }

        private float GetXpRequiredForLevel(UnitSkillType skillType, int level)
        {
            int clampedLevel = Mathf.Clamp(level, MinSkillLevel, MaxSkillLevel);

            if (clampedLevel >= MaxSkillLevel)
            {
                return 0f;
            }

            if (!TryGetProgressionParameters(skillType, out _, out _, out float baseReq, out float growthPerLevel))
            {
                return 0f;
            }

            return Mathf.Max(0f, baseReq + (clampedLevel - MinSkillLevel) * growthPerLevel);
        }

        private void ApplyAllHealthBonuses(bool refillCurrentHealth)
        {
            if (unitHealth == null) return;
            CacheStrengthBaseHealthIfNeeded();
            float targetMaxHealth = strengthBaseMaxHealth
                * GetStrengthHealthMultiplier()
                * GetToughnessHealthMultiplier()
                * GetConstitutionHealthMultiplier();
            unitHealth.SetMaxHealth(targetMaxHealth, refillCurrentHealth);
        }

        private float GetStrengthHealthMultiplier()
        {
            return 1f + (SkillT(strength) * (MaxSkillLevel - MinSkillLevel)) * strengthHealthBonusPerLevelPercent * 0.01f;
        }

        private float GetToughnessHealthMultiplier()
        {
            return 1f + (SkillT(toughness) * (MaxSkillLevel - MinSkillLevel)) * toughnessHealthBonusPerLevelPercent * 0.01f;
        }

        private float GetConstitutionHealthMultiplier()
        {
            return 1f + (SkillT(constitution) * (MaxSkillLevel - MinSkillLevel)) * constitutionHealthBonusPerLevelPercent * 0.01f;
        }

        private void CacheStrengthBaseHealthIfNeeded()
        {
            if (hasStrengthBaseHealth || unitHealth == null)
            {
                return;
            }

            strengthBaseMaxHealth = Mathf.Max(1f, unitHealth.MaxHealth);
            hasStrengthBaseHealth = true;
        }

        private void ClampSkillsToBounds()
        {
            strength = Mathf.Clamp(strength, MinSkillLevel, MaxSkillLevel);
            shooting = Mathf.Clamp(shooting, MinSkillLevel, MaxSkillLevel);
            melee = Mathf.Clamp(melee, MinSkillLevel, MaxSkillLevel);
            medical = Mathf.Clamp(medical, MinSkillLevel, MaxSkillLevel);
            engineering = Mathf.Clamp(engineering, MinSkillLevel, MaxSkillLevel);
            toughness = Mathf.Clamp(toughness, MinSkillLevel, MaxSkillLevel);
            constitution = Mathf.Clamp(constitution, MinSkillLevel, MaxSkillLevel);
            agility = Mathf.Clamp(agility, MinSkillLevel, MaxSkillLevel);
            endurance = Mathf.Clamp(endurance, MinSkillLevel, MaxSkillLevel);
            scavenging = Mathf.Clamp(scavenging, MinSkillLevel, MaxSkillLevel);
            stealth = Mathf.Clamp(stealth, MinSkillLevel, MaxSkillLevel);
        }

        private void AutoResolveReferences()
        {
            if (unitHealth == null)
            {
                unitHealth = GetComponent<UnitHealth>();
            }
        }
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