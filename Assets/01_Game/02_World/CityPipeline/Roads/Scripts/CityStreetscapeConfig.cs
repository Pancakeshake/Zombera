using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Combined above-road content config — prefabs AND placement behaviour together.
    ///     Merges the old streetscape settings (toggles/spacing) and
    ///     catalog (prefab references) into a single ScriptableObject.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/World/City Streetscape Config", fileName = "CityStreetscapeConfig")]
    public sealed class CityStreetscapeConfig : ScriptableObject
    {
        // ── Fences ───────────────────────────────────────────────────

        [Header("Fences")]
        [Tooltip("Prefab used for residential lot fences.")]
        public GameObject fencePrefab;

        // ── Buildings ────────────────────────────────────────────────

        [Header("Buildings")]
        [Tooltip("Folder containing assembled building prefabs.")]
        public string assembledPrefabFolder = "Assets/02_Shared/Prefabs/Building/Buildings_Modular_Complete";

        [Tooltip("Folder containing building proxy prefabs.")]
        public string proxyPrefabFolder = "Assets/02_Shared/Proxies/Buildings_Complete";

        // ── Lot Decoration ───────────────────────────────────────────

        [Header("Lot Decoration")]
        [Tooltip("Folder containing tree and bush prefabs (root level only).")]
        public string treePrefabFolder = "Assets/02_Shared/Prefabs/Props/Nature";

        // ── Traffic Lights ───────────────────────────────────────────

        [Header("Traffic Lights")]
        [Tooltip("Prefab for traffic signal poles at junctions.")]
        public GameObject trafficSignalPrefab;
        public bool spawnTrafficLights = true;
        [Min(0f)] public float trafficSignalSetbackMeters = 2.5f;
        [Min(0f)] public float trafficSignalLateralOffsetMeters = 1.8f;
        public bool placeSignalsOnTerminatingBranch = true;

        // ── Street Signs ─────────────────────────────────────────────

        [Header("Street Signs")]
        [Tooltip("Prefab for street name signs at junctions.")]
        public GameObject streetSignPrefab;
        public bool spawnStreetSigns = true;
        [Min(0f)] public float streetSignCornerOffsetMeters = 3.5f;

        // ── Street Lamps ─────────────────────────────────────────────

        [Header("Street Lamps")]
        [Tooltip("Prefab for street lamps placed along road edges.")]
        public GameObject streetLampPrefab;

        // ── Power Lines ──────────────────────────────────────────────

        [Header("Power Lines")]
        [Tooltip("Prefab for utility / power-line poles.")]
        public GameObject utilityPolePrefab;
        [Tooltip("Material for power-line wires.")]
        public Material powerLineWireMaterial;
        public bool spawnPowerLines = true;
        [Min(1f)] public float utilityPoleSpacingMeters = 40f;
        [Tooltip("Distance of the pole line outside each lot's street-facing edge, onto the sidewalk/footpath strip.")]
        [Min(0f)] public float utilityPoleLateralOffsetMeters = 2f;
        [Tooltip("Extra clearance around each lot's painted driveway and front-door path so poles stand clear of the crossings.")]
        [Min(0f)] public float utilityPoleCrossingClearanceMeters = 0.75f;
        public bool alternatePowerLineSides = true;
        [Min(0.01f)] public float powerLineWireWidthMeters = 0.05f;

        // ── Parked Cars ──────────────────────────────────────────────

        [Header("Parked Cars")]
        [Tooltip("Prefabs for parked cars (randomly selected).")]
        public GameObject[] parkedCarPrefabs;
        public bool spawnParkedCars = true;
        [Tooltip("Chance (0–1) that a residential house gets a car parked on its driveway.")]
        [Range(0f, 1f)] public float parkedCarLotChance = 0.5f;
        [Min(1f)] public float parkedCarSpacingMeters = 12f;
        [Min(0f)] public float parkedCarShoulderOffsetMeters = 0.8f;
        [Range(0f, 1f)] public float parkedCarLocalDensity = 0.7f;
        [Range(0f, 1f)] public float parkedCarArterialDensity = 0.35f;
        [Range(0f, 1f)] public float parkedCarHighwayDensity = 0.1f;
        [Min(0f)] public float parkedCarJunctionExclusionMeters = 12f;

        // ── Street Furniture ─────────────────────────────────────────

        [Header("Street Furniture")]
        [Tooltip("Prefab for benches.")]
        public GameObject benchPrefab;
        [Tooltip("Prefab for mailboxes.")]
        public GameObject mailboxPrefab;
        [Tooltip("Prefab for fire hydrants.")]
        public GameObject fireHydrantPrefab;
        [Tooltip("Prefab for trash cans.")]
        public GameObject trashCanPrefab;
        public bool spawnStreetFurniture = true;
        [Min(0f)] public float streetFurnitureMinSeparationMeters = 4f;
        [Range(0f, 1f)] public float benchDensityPer100Meters = 0.15f;
        [Range(0f, 1f)] public float mailboxDensityPer100Meters = 0.08f;
        [Range(0f, 1f)] public float hydrantDensityPer100Meters = 0.05f;
        [Range(0f, 1f)] public float trashCanDensityPer100Meters = 0.1f;
    }
}
