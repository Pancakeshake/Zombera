using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Zombera.Core
{
    [Serializable]
    public sealed class EquipmentSaveData
    {
        public int slot; // EquipmentSlot enum cast to int
        public string itemInstanceId;
        public string itemId;
    }

    [Serializable]
    public sealed class WeaponRuntimeSaveData
    {
        public string equippedWeaponId;
        public int currentAmmo;
        public string activeMode = string.Empty;
        public float reloadProgress = 0f;
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        [FormerlySerializedAs("Position")] public Vector3 position;
        [FormerlySerializedAs("Rotation")] public Quaternion rotation;
        [FormerlySerializedAs("Health")] public float health;
        [FormerlySerializedAs("Stats")] public UnitStatsSaveData stats = new();
        public string appearanceProfileJson = string.Empty;
        public List<EquipmentSaveData> equipment = new();
        public WeaponRuntimeSaveData weaponRuntime = new();
        public FormationSaveData formation = new();
    }

    [Serializable]
    public sealed class SquadMemberSaveData
    {
        [FormerlySerializedAs("UnitId")] public string unitId;
        [FormerlySerializedAs("Position")] public Vector3 position;
        [FormerlySerializedAs("Rotation")] public Quaternion rotation;
        public int squadRole = -1;
        [FormerlySerializedAs("Health")] public float health;
        [FormerlySerializedAs("Stats")] public UnitStatsSaveData stats = new();
        public string appearanceProfileJson = string.Empty;
        public List<EquipmentSaveData> equipment = new();
        public WeaponRuntimeSaveData weaponRuntime = new();
    }

    [Serializable]
    public sealed class UnitStatsSaveData
    {
        [FormerlySerializedAs("HasSkillProgressionData")]
        public bool hasSkillProgressionData;

        [FormerlySerializedAs("Stamina")] public float stamina;

        [FormerlySerializedAs("Strength")] public int strength;
        [FormerlySerializedAs("StrengthXp")] public float strengthXp;
        [FormerlySerializedAs("Shooting")] public int shooting;
        [FormerlySerializedAs("ShootingXp")] public float shootingXp;
        [FormerlySerializedAs("Melee")] public int melee;
        [FormerlySerializedAs("MeleeXp")] public float meleeXp;
        [FormerlySerializedAs("Medical")] public int medical;
        [FormerlySerializedAs("MedicalXp")] public float medicalXp;
        [FormerlySerializedAs("Engineering")] public int engineering;

        [FormerlySerializedAs("EngineeringXp")]
        public float engineeringXp;

        [FormerlySerializedAs("Toughness")] public int toughness;
        [FormerlySerializedAs("ToughnessXp")] public float toughnessXp;
        [FormerlySerializedAs("Constitution")] public int constitution;

        [FormerlySerializedAs("ConstitutionXp")]
        public float constitutionXp;

        [FormerlySerializedAs("Agility")] public int agility;
        [FormerlySerializedAs("AgilityXp")] public float agilityXp;
        [FormerlySerializedAs("Endurance")] public int endurance;
        [FormerlySerializedAs("EnduranceXp")] public float enduranceXp;
        [FormerlySerializedAs("Scavenging")] public int scavenging;
        [FormerlySerializedAs("ScavengingXp")] public float scavengingXp;
        [FormerlySerializedAs("Stealth")] public int stealth;
        [FormerlySerializedAs("StealthXp")] public float stealthXp;
    }
}
