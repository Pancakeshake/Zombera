using UnityEngine;
using Zombera.BuildingSystem;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Views
{
    public static class WorldBuildingViewApplier
    {
        public static WorldBuildingView Apply(
            GameObject instance,
            BuildingState building,
            WorldTileKey ownerTile,
            BuildingArchetypeResolution resolution,
            RuntimePlacedStructureFixer structureFixer,
            bool logDiagnostics)
        {
            if (instance == null || building == null)
                return null;

            instance.name = "WorldBuilding_" + (building.id.value ?? string.Empty);
            instance.transform.SetPositionAndRotation(building.position, building.rotation);
            instance.transform.localScale = building.scale == Vector3.zero ? Vector3.one : building.scale;

            var view = EnsureBuildingView(instance, building.id);
            view.BindBuilding(building, ownerTile, resolution);

            if (resolution.UsesProxy)
                InitializeProxySwap(instance, resolution, structureFixer, logDiagnostics);
            else
                AddStructuralComponentsIfNeeded(instance, resolution);

            structureFixer?.ProcessPlacedStructure(instance);
            return view;
        }

        private static WorldBuildingView EnsureBuildingView(GameObject instance, WorldEntityId id)
        {
            var existingView = instance.GetComponent<WorldStateEntityView>();
            var view = existingView as WorldBuildingView;
            if (view != null)
                return view;

            view = instance.GetComponent<WorldBuildingView>();
            if (view == null)
                view = instance.AddComponent<WorldBuildingView>();

            if (existingView == null)
                WorldStateEntityViewBinding.Bind(instance, id);

            return view;
        }

        private static void InitializeProxySwap(
            GameObject instance,
            BuildingArchetypeResolution resolution,
            RuntimePlacedStructureFixer structureFixer,
            bool logDiagnostics)
        {
            if (resolution.FullBuildingPrefab == null)
            {
                AddStructuralComponentsIfNeeded(instance, resolution);
                return;
            }

            var swap = instance.GetComponent<StreamedCityBuildingProxySwap>();
            if (swap == null)
                swap = instance.AddComponent<StreamedCityBuildingProxySwap>();

            swap.Initialize(
                resolution.FullBuildingPrefab,
                structureFixer,
                resolution.ProxySwapDistanceMeters,
                resolution.ProxySwapOnFirstDamage,
                true,
                resolution.EnsureStructureHealth,
                resolution.StructureMaxHealth,
                resolution.EnsureBuildPiece,
                resolution.BuildPieceCategory,
                logDiagnostics);
        }

        private static void AddStructuralComponentsIfNeeded(
            GameObject instance,
            BuildingArchetypeResolution resolution)
        {
            if (resolution.EnsureStructureHealth)
            {
                var health = instance.GetComponent<StructureHealth>();
                if (health == null)
                    health = instance.AddComponent<StructureHealth>();
                health.SetMaxHealth(resolution.StructureMaxHealth, true);
            }

            if (!resolution.EnsureBuildPiece)
                return;

            var piece = instance.GetComponent<BuildPiece>();
            if (piece == null)
                piece = instance.AddComponent<BuildPiece>();
            piece.SetCategory(resolution.BuildPieceCategory);
        }
    }
}
