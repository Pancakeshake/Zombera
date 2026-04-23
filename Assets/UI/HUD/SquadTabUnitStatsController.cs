using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;

namespace Zombera.UI
{
    [AddComponentMenu("Zombera/UI/Squad Tab Unit Stats Controller")]
    [DisallowMultipleComponent]
    public sealed class SquadTabUnitStatsController : MonoBehaviour
    {
        [SerializeField] private SquadPortraitStrip rosterStrip;

        [Header("Summary")]
        [SerializeField] private TextMeshProUGUI selectedNameText;
        [SerializeField] private TextMeshProUGUI roleText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI staminaText;

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI strengthValueText;
        [SerializeField] private TextMeshProUGUI shootingValueText;
        [SerializeField] private TextMeshProUGUI meleeValueText;
        [SerializeField] private TextMeshProUGUI medicalValueText;
        [SerializeField] private TextMeshProUGUI engineeringValueText;
        [SerializeField] private TextMeshProUGUI toughnessValueText;
        [SerializeField] private TextMeshProUGUI constitutionValueText;
        [SerializeField] private TextMeshProUGUI agilityValueText;
        [SerializeField] private TextMeshProUGUI enduranceValueText;
        [SerializeField] private TextMeshProUGUI scavengingValueText;
        [SerializeField] private TextMeshProUGUI stealthValueText;

        [Header("Skill Buttons")]
        [SerializeField] private Button strengthButton;
        [SerializeField] private Button shootingButton;
        [SerializeField] private Button meleeButton;
        [SerializeField] private Button medicalButton;
        [SerializeField] private Button engineeringButton;
        [SerializeField] private Button toughnessButton;
        [SerializeField] private Button constitutionButton;
        [SerializeField] private Button agilityButton;
        [SerializeField] private Button enduranceButton;
        [SerializeField] private Button scavengingButton;
        [SerializeField] private Button stealthButton;

        [Header("Skill Info Modal")]
        [SerializeField] private GameObject skillInfoModalRoot;
        [SerializeField] private Button skillInfoCloseButton;
        [SerializeField] private TextMeshProUGUI skillInfoTitleText;
        [SerializeField] private TextMeshProUGUI skillInfoLevelText;
        [SerializeField] private TextMeshProUGUI skillInfoXpText;
        [SerializeField] private TextMeshProUGUI skillInfoHowToLevelText;
        [SerializeField] private TextMeshProUGUI skillInfoEffectsText;

        private Unit _selectedUnit;
        private float _refreshTicker;
        private UnitSkillType? _activeSkillInfo;

        private void OnEnable()
        {
            BindSkillButtons();
            BindModalControls();

            if (rosterStrip != null)
            {
                rosterStrip.OnPortraitClicked += HandlePortraitClicked;
                rosterStrip.RefreshBindings();
                if (!rosterStrip.SelectPlayerOrFirstBoundUnit())
                {
                    ApplyEmptyState();
                }
            }
            else
            {
                ApplyEmptyState();
            }

            HideSkillInfoModal();
        }

        private void OnDisable()
        {
            if (rosterStrip != null)
            {
                rosterStrip.OnPortraitClicked -= HandlePortraitClicked;
            }

            if (skillInfoCloseButton != null)
            {
                skillInfoCloseButton.onClick.RemoveAllListeners();
            }
        }

        private void Update()
        {
            if (!IsUnitValid(_selectedUnit))
            {
                if (rosterStrip != null && rosterStrip.SelectPlayerOrFirstBoundUnit())
                {
                    return;
                }

                ApplyEmptyState();
                return;
            }

            _refreshTicker += Time.unscaledDeltaTime;
            if (_refreshTicker < 0.15f)
            {
                return;
            }

            _refreshTicker = 0f;
            ApplyUnitState(_selectedUnit);
        }

        private void HandlePortraitClicked(Unit unit)
        {
            if (!IsUnitValid(unit))
            {
                if (rosterStrip != null && rosterStrip.SelectPlayerOrFirstBoundUnit())
                {
                    return;
                }

                ApplyEmptyState();
                return;
            }

            _selectedUnit = unit;
            _refreshTicker = 0f;
            ApplyUnitState(unit);
        }

        private void BindSkillButtons()
        {
            BindSkillButton(strengthButton, UnitSkillType.Strength);
            BindSkillButton(shootingButton, UnitSkillType.Shooting);
            BindSkillButton(meleeButton, UnitSkillType.Melee);
            BindSkillButton(medicalButton, UnitSkillType.Medical);
            BindSkillButton(engineeringButton, UnitSkillType.Engineering);
            BindSkillButton(toughnessButton, UnitSkillType.Toughness);
            BindSkillButton(constitutionButton, UnitSkillType.Constitution);
            BindSkillButton(agilityButton, UnitSkillType.Agility);
            BindSkillButton(enduranceButton, UnitSkillType.Endurance);
            BindSkillButton(scavengingButton, UnitSkillType.Scavenging);
            BindSkillButton(stealthButton, UnitSkillType.Stealth);
        }

        private void BindSkillButton(Button button, UnitSkillType skill)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ShowSkillInfoModal(skill));
        }

        private void BindModalControls()
        {
            if (skillInfoCloseButton == null)
            {
                return;
            }

            skillInfoCloseButton.onClick.RemoveAllListeners();
            skillInfoCloseButton.onClick.AddListener(HideSkillInfoModal);
        }

        private void ShowSkillInfoModal(UnitSkillType skillType)
        {
            _activeSkillInfo = skillType;
            if (skillInfoModalRoot != null)
            {
                skillInfoModalRoot.SetActive(true);
            }

            RefreshSkillInfoModal();
        }

        private void HideSkillInfoModal()
        {
            _activeSkillInfo = null;
            if (skillInfoModalRoot != null)
            {
                skillInfoModalRoot.SetActive(false);
            }
        }

        private void RefreshSkillInfoModal()
        {
            if (!_activeSkillInfo.HasValue)
            {
                return;
            }

            if (skillInfoModalRoot != null && !skillInfoModalRoot.activeSelf)
            {
                skillInfoModalRoot.SetActive(true);
            }

            UnitSkillType skillType = _activeSkillInfo.Value;
            SetText(skillInfoTitleText, skillType.ToString().ToUpperInvariant());

            UnitStats stats = ResolveUnitStats(_selectedUnit);
            if (stats == null)
            {
                SetText(skillInfoLevelText, "Level: -");
                SetText(skillInfoXpText, "XP: -");
                SetText(skillInfoHowToLevelText, GetHowToLevelText(skillType));
                SetText(skillInfoEffectsText, "Effects: No active unit stats available.");
                return;
            }

            int level = stats.GetSkillLevel(skillType);
            float currentXp = stats.GetCurrentExperience(skillType);
            float requiredXp = stats.GetExperienceRequiredForNextLevel(skillType);
            float totalXp = stats.GetTotalExperienceEarned(skillType);

            SetText(skillInfoLevelText, $"Level: {level} / {UnitStats.MaxSkillLevel}");
            if (level >= UnitStats.MaxSkillLevel)
            {
                SetText(skillInfoXpText, $"XP: MAX   Total XP: {Mathf.RoundToInt(totalXp)}");
            }
            else
            {
                SetText(
                    skillInfoXpText,
                    $"XP: {currentXp:0.#} / {requiredXp:0.#}   Total XP: {Mathf.RoundToInt(totalXp)}");
            }

            SetText(skillInfoHowToLevelText, GetHowToLevelText(skillType));
            SetText(skillInfoEffectsText, GetEffectsText(skillType, stats, _selectedUnit));
        }

        private static bool IsUnitValid(Unit unit)
        {
            return unit != null && unit.IsAlive;
        }

        private void ApplyUnitState(Unit unit)
        {
            if (unit == null)
            {
                ApplyEmptyState();
                return;
            }

            UnitStats stats = ResolveUnitStats(unit);
            UnitHealth health = ResolveUnitHealth(unit);

            SetText(selectedNameText, unit.gameObject.name);
            SetText(roleText, $"Role: {unit.Role}");

            if (health != null)
            {
                SetText(healthText, $"Health: {Mathf.RoundToInt(health.CurrentHealth)} / {Mathf.RoundToInt(health.MaxHealth)}");
            }
            else
            {
                SetText(healthText, "Health: -");
            }

            if (stats != null)
            {
                SetText(staminaText, $"Stamina: {Mathf.RoundToInt(stats.Stamina)} / {Mathf.RoundToInt(stats.MaxStamina)}");
                SetText(strengthValueText, stats.Strength.ToString());
                SetText(shootingValueText, stats.Shooting.ToString());
                SetText(meleeValueText, stats.Melee.ToString());
                SetText(medicalValueText, stats.Medical.ToString());
                SetText(engineeringValueText, stats.Engineering.ToString());
                SetText(toughnessValueText, stats.Toughness.ToString());
                SetText(constitutionValueText, stats.Constitution.ToString());
                SetText(agilityValueText, stats.Agility.ToString());
                SetText(enduranceValueText, stats.Endurance.ToString());
                SetText(scavengingValueText, stats.Scavenging.ToString());
                SetText(stealthValueText, stats.Stealth.ToString());
            }
            else
            {
                SetText(staminaText, "Stamina: -");
                SetText(strengthValueText, "-");
                SetText(shootingValueText, "-");
                SetText(meleeValueText, "-");
                SetText(medicalValueText, "-");
                SetText(engineeringValueText, "-");
                SetText(toughnessValueText, "-");
                SetText(constitutionValueText, "-");
                SetText(agilityValueText, "-");
                SetText(enduranceValueText, "-");
                SetText(scavengingValueText, "-");
                SetText(stealthValueText, "-");
            }

            RefreshSkillInfoModal();
        }

        private void ApplyEmptyState()
        {
            _selectedUnit = null;
            SetText(selectedNameText, "No Unit Selected");
            SetText(roleText, "Role: -");
            SetText(healthText, "Health: -");
            SetText(staminaText, "Stamina: -");
            SetText(strengthValueText, "-");
            SetText(shootingValueText, "-");
            SetText(meleeValueText, "-");
            SetText(medicalValueText, "-");
            SetText(engineeringValueText, "-");
            SetText(toughnessValueText, "-");
            SetText(constitutionValueText, "-");
            SetText(agilityValueText, "-");
            SetText(enduranceValueText, "-");
            SetText(scavengingValueText, "-");
            SetText(stealthValueText, "-");
            HideSkillInfoModal();
        }

        private static UnitStats ResolveUnitStats(Unit unit)
        {
            if (unit == null)
            {
                return null;
            }

            if (unit.Stats != null)
            {
                return unit.Stats;
            }

            UnitStats stats = unit.GetComponent<UnitStats>();
            if (stats == null)
            {
                stats = unit.GetComponentInChildren<UnitStats>();
            }

            if (stats == null)
            {
                stats = unit.GetComponentInParent<UnitStats>();
            }

            return stats;
        }

        private static UnitHealth ResolveUnitHealth(Unit unit)
        {
            if (unit == null)
            {
                return null;
            }

            if (unit.Health != null)
            {
                return unit.Health;
            }

            UnitHealth health = unit.GetComponent<UnitHealth>();
            if (health == null)
            {
                health = unit.GetComponentInChildren<UnitHealth>();
            }

            if (health == null)
            {
                health = unit.GetComponentInParent<UnitHealth>();
            }

            return health;
        }

        private static string GetHowToLevelText(UnitSkillType skillType)
        {
            switch (skillType)
            {
                case UnitSkillType.Strength:
                    return "How to level: Carry heavy loads, land weighted/unarmed hits, and complete weight-training reps.";
                case UnitSkillType.Shooting:
                    return "How to level: Land ranged hits on enemies.";
                case UnitSkillType.Melee:
                    return "How to level: Land melee hits in close combat.";
                case UnitSkillType.Medical:
                    return "How to level: Heal allies and recover health on units.";
                case UnitSkillType.Engineering:
                    return "How to level: Place and build construction pieces.";
                case UnitSkillType.Toughness:
                    return "How to level: Take incoming damage and survive encounters.";
                case UnitSkillType.Constitution:
                    return "How to level: Consume meals/vitamins and endure damage over time.";
                case UnitSkillType.Agility:
                    return "How to level: Sprint and maintain mobile movement.";
                case UnitSkillType.Endurance:
                    return "How to level: Stay physically exerted over time (long runs and strain).";
                case UnitSkillType.Scavenging:
                    return "How to level: Search and loot containers in the world.";
                case UnitSkillType.Stealth:
                    return "How to level: Move undetected while avoiding enemy awareness.";
                default:
                    return "How to level: Perform actions related to this skill.";
            }
        }

        private static string GetEffectsText(UnitSkillType skillType, UnitStats stats, Unit unit)
        {
            switch (skillType)
            {
                case UnitSkillType.Strength:
                    return $"Effects: +{(stats.GetStrengthDamageMultiplier() - 1f) * 100f:0.#}% strength damage, +{stats.GetStrengthKnockbackChanceBonus() * 100f:0.#}% knockback chance.";
                case UnitSkillType.Shooting:
                    return $"Effects: +{(stats.ApplyShootingDamageScaling(100f) - 100f):0.#}% ranged damage, +{(stats.GetShootingEffectiveRangeMultiplier() - 1f) * 100f:0.#}% range, +{stats.GetShootingHitChanceBonus() * 100f:0.#}% hit chance.";
                case UnitSkillType.Melee:
                    return $"Effects: +{(stats.ApplyMeleeDamageScaling(100f) - 100f):0.#}% melee damage, +{(stats.GetMeleeAttackSpeedMultiplier() - 1f) * 100f:0.#}% attack speed, +{stats.GetMeleeKnockbackChance() * 100f:0.#}% knockback chance.";
                case UnitSkillType.Medical:
                    return $"Effects: +{(stats.GetMedicalHealMultiplier() - 1f) * 100f:0.#}% healing output.";
                case UnitSkillType.Engineering:
                    return $"Effects: +{(stats.GetEngineeringBuildSpeedMultiplier() - 1f) * 100f:0.#}% build speed.";
                case UnitSkillType.Toughness:
                    return $"Effects: {stats.GetToughnessDamageReduction() * 100f:0.#}% incoming damage reduction.";
                case UnitSkillType.Constitution:
                    return unit != null && unit.Health != null
                        ? $"Effects: Boosts max-health scaling and resilience. Current max health: {Mathf.RoundToInt(unit.Health.MaxHealth)}."
                        : "Effects: Boosts max-health scaling and resilience.";
                case UnitSkillType.Agility:
                    return $"Effects: +{(stats.GetAgilityMoveSpeedMultiplier() - 1f) * 100f:0.#}% move speed, +{stats.GetAgilityDodgeChance() * 100f:0.#}% dodge chance.";
                case UnitSkillType.Endurance:
                    return $"Effects: +{(stats.GetEnduranceStaminaMultiplier() - 1f) * 100f:0.#}% max stamina, +{(stats.GetEnduranceRegenMultiplier() - 1f) * 100f:0.#}% stamina regen.";
                case UnitSkillType.Scavenging:
                    return $"Effects: +{(stats.GetScavengingLootMultiplier() - 1f) * 100f:0.#}% loot yield.";
                case UnitSkillType.Stealth:
                    return $"Effects: {Mathf.Abs((stats.GetStealthDetectionRadiusMultiplier() - 1f) * 100f):0.#}% lower detection radius.";
                default:
                    return "Effects: No effect data available.";
            }
        }

        private static void SetText(TextMeshProUGUI target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
