using UnityEngine;
using Zombera.BuildingSystem;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Views
{
    public interface IBuildingArchetypeResolver
    {
        bool TryResolve(BuildingState building, out BuildingArchetypeResolution resolution);
    }

    public readonly struct BuildingArchetypeResolution
    {
        public readonly string ArchetypeId;
        public readonly GameObject Prefab;
        public readonly GameObject FullBuildingPrefab;
        public readonly bool UsesProxy;
        public readonly float ProxySwapDistanceMeters;
        public readonly bool ProxySwapOnFirstDamage;
        public readonly bool EnsureStructureHealth;
        public readonly float StructureMaxHealth;
        public readonly bool EnsureBuildPiece;
        public readonly BuildPieceCategory BuildPieceCategory;

        public BuildingArchetypeResolution(
            string archetypeId,
            GameObject prefab,
            GameObject fullBuildingPrefab,
            bool usesProxy,
            float proxySwapDistanceMeters,
            bool proxySwapOnFirstDamage,
            bool ensureStructureHealth,
            float structureMaxHealth,
            bool ensureBuildPiece,
            BuildPieceCategory buildPieceCategory)
        {
            ArchetypeId = archetypeId ?? string.Empty;
            Prefab = prefab;
            FullBuildingPrefab = fullBuildingPrefab;
            UsesProxy = usesProxy;
            ProxySwapDistanceMeters = proxySwapDistanceMeters;
            ProxySwapOnFirstDamage = proxySwapOnFirstDamage;
            EnsureStructureHealth = ensureStructureHealth;
            StructureMaxHealth = Mathf.Max(1f, structureMaxHealth);
            EnsureBuildPiece = ensureBuildPiece;
            BuildPieceCategory = buildPieceCategory;
        }

        public bool IsValid => Prefab != null;
    }
}
