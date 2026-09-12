using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;

namespace Zombera.World.CityPipeline.WorldBuilder.Views
{
    [AddComponentMenu("Zombera/World/World Building View")]
    [DisallowMultipleComponent]
    public sealed class WorldBuildingView : WorldStateEntityView
    {
        [SerializeField] private WorldTileKey _ownerTile;
        [SerializeField] private string _sourceId = string.Empty;
        [SerializeField] private string _archetypeId = string.Empty;
        [SerializeField] private string _typeId = string.Empty;
        [SerializeField] private bool _usesProxy;
        [SerializeField] private GameObject _sourcePrefab;

        public WorldTileKey OwnerTile => _ownerTile;
        public string SourceId => _sourceId;
        public string ArchetypeId => _archetypeId;
        public string TypeId => _typeId;
        public bool UsesProxy => _usesProxy;
        public GameObject SourcePrefab => _sourcePrefab;

        public void BindBuilding(
            BuildingState building,
            WorldTileKey ownerTile,
            BuildingArchetypeResolution resolution)
        {
            if (building == null)
            {
                ClearBinding();
                return;
            }

            Bind(building.id);
            _ownerTile = ownerTile;
            _sourceId = building.sourceId ?? string.Empty;
            _archetypeId = building.archetypeId ?? string.Empty;
            _typeId = building.typeId ?? string.Empty;
            _usesProxy = resolution.UsesProxy;
            _sourcePrefab = resolution.Prefab;
        }

        public override void ClearBinding()
        {
            base.ClearBinding();
            _ownerTile = default;
            _sourceId = string.Empty;
            _archetypeId = string.Empty;
            _typeId = string.Empty;
            _usesProxy = false;
            _sourcePrefab = null;
        }
    }
}
