using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World
{
    /// <summary>
    ///     Listens to world tile activation and corrects grounding for nearby units.
    /// </summary>
    [RequireComponent(typeof(WorldTileStreamSource))]
    public sealed class UnitGroundingManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float searchRadiusPadding = 10f;
        [SerializeField] private bool logCorrections = true;

        private WorldTileStreamSource _bridge;
        private IWorldTileGameplayEvents _bridgeEvents;
        private readonly List<Unit> _unitBuffer = new();

        private void Awake()
        {
            _bridge = GetComponent<WorldTileStreamSource>();
            _bridgeEvents = WorldTileStreamSourceUtility.AsGameplayEvents(_bridge);
        }

        private void OnEnable()
        {
            if (_bridgeEvents != null)
                _bridgeEvents.TileAppliedForGameplay += HandleTileApplied;
            else if (_bridge != null)
                _bridge.TileStateChanged += HandleTileStateChanged;
        }

        private void OnDisable()
        {
            if (_bridgeEvents != null)
                _bridgeEvents.TileAppliedForGameplay -= HandleTileApplied;
            else if (_bridge != null)
                _bridge.TileStateChanged -= HandleTileStateChanged;
        }

        private void HandleTileStateChanged(WorldTileTransition transition)
        {
            if (transition.Current < WorldTileState.TerrainReady) return;
            HandleTileApplied(transition.Tile);
        }

        private void HandleTileApplied(WorldTileInfo tile)
        {
            if (!UnitManager.HasInstance) return;

            var tileRect = tile.WorldRectXZ;
            var centerXZ = tileRect.center;
            var center = new Vector3(centerXZ.x, transform.position.y, centerXZ.y);
            var searchRadius = Mathf.Max(tileRect.width, tileRect.height) * 0.5f + searchRadiusPadding;

            var nearbyUnits = UnitManager.Instance.FindNearbyUnits(center, searchRadius, _unitBuffer);

            var correctionCount = 0;
            for (var i = 0; i < nearbyUnits.Count; i++)
            {
                var unit = nearbyUnits[i];
                if (unit == null) continue;

                var pos = unit.transform.position;
                if (!tileRect.Contains(new Vector2(pos.x, pos.z))) continue;

                if (TryCorrectGrounding(unit))
                    correctionCount++;
            }

            if (logCorrections && correctionCount > 0)
            {
                Debug.Log(
                    $"[UnitGroundingManager] Applied seam correction to {correctionCount} units for tile {tile.Coord} at {tileRect.center}.");
            }
        }

        private bool TryCorrectGrounding(Unit unit)
        {
            var profile = MovementGroundingSettings.Active;
            var pos = unit.transform.position;

            UnitNavUtils.TryResolveGroundReferenceY(pos, out var groundReferenceY);
            var preferTerrain = profile.PreferTerrainHeightOverNavMeshAt(pos);
            var sampleOrigin = preferTerrain
                ? new Vector3(pos.x, groundReferenceY + profile.NavSampleUpOffset, pos.z)
                : new Vector3(pos.x, pos.y + profile.NavSampleUpOffset, pos.z);

            var referenceY = preferTerrain ? groundReferenceY : pos.y;
            Vector3 navPos;
            var hasNavSample = preferTerrain
                ? UnitNavUtils.TrySampleTieredNearReferenceY(
                    sampleOrigin,
                    referenceY,
                    out navPos,
                    profile.MaxNavMeshVerticalDeltaFromGround,
                    profile.NavSampleRadii)
                : UnitNavUtils.TrySampleTiered(
                    sampleOrigin,
                    out navPos,
                    profile.NavSampleRadii,
                    UnitNavUtils.WalkableAreaMask);

            if (!hasNavSample)
                return false;

            if (preferTerrain
                && profile.TryProjectGround(navPos, out var footPoint))
                navPos = profile.BlendNavMeshWithTerrain(navPos, footPoint);

            if (pos.y - navPos.y <= profile.SeamVerticalDeltaThreshold) return false;

            if (unit.TryGetComponent<UnitController>(out var controller))
                controller.LogGroundingState("SeamCorrection.Warp");

            return UnitNavUtils.PlaceUnitOnNavMesh(unit.gameObject, navPos, 2f, allowWideFallback: false);
        }
    }
}
