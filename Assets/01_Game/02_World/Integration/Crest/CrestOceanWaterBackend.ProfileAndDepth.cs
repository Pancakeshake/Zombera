using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>Profile binding and sea-floor depth-cache lifecycle for the Crest ocean backend.</summary>
    public sealed partial class CrestOceanWaterBackend
    {
        /// <summary>
        /// Ensures an OceanRenderer exists for Crest inland water even when the map has no ocean edges.
        /// </summary>
        public bool EnsureOceanSimulator(float seaLevelWorldY, Rect depthBoundsXZ)
        {
            EnsureRoot();
            RemoveForeignOceanRenderers();
            if (!PrepareOcean(seaLevelWorldY))
                return false;

            CrestOceanConfigurator.EnsureFlowAndFoam(_oceanRenderer, _waterProfile);
            ActivateOcean();
            SyncAllWaterBodiesUnderRoot();
            EnsureSeaFloorTileBinder().BindProfile(_waterProfile);
            if (depthBoundsXZ.width > 0f && depthBoundsXZ.height > 0f)
                RefreshOceanDepthCache(depthBoundsXZ, seaLevelWorldY);

            return HasActiveOcean;
        }

        public void RefreshOceanDepthCache(Rect boundsXZ, float seaLevelWorldY)
        {
            if (_oceanInstance == null || boundsXZ.width <= 0f || boundsXZ.height <= 0f)
                return;

            _lastDepthBoundsXZ = boundsXZ;
            CrestOceanConfigurator.EnsureDepthCache(
                _oceanInstance.transform,
                seaLevelWorldY,
                boundsXZ,
                _waterProfile);
        }

        /// <summary>Re-populate using the last full depth bounds (tile streaming refresh).</summary>
        public void RefreshOceanDepthCacheFromLast()
        {
            if (_lastDepthBoundsXZ.width <= 0f || _lastDepthBoundsXZ.height <= 0f)
                return;
            RefreshOceanDepthCache(_lastDepthBoundsXZ, _seaLevelWorldY);
        }

        /// <summary>
        /// Union tile bounds into the last depth footprint and refresh so streaming terrain
        /// contributes to Crest Sea Floor Depth / shoreline foam.
        /// </summary>
        public void RefreshOceanDepthCacheUnion(Rect additionalBoundsXZ)
        {
            if (additionalBoundsXZ.width <= 0f || additionalBoundsXZ.height <= 0f)
            {
                RefreshOceanDepthCacheFromLast();
                return;
            }

            if (_lastDepthBoundsXZ.width <= 0f || _lastDepthBoundsXZ.height <= 0f)
            {
                RefreshOceanDepthCache(additionalBoundsXZ, _seaLevelWorldY);
                return;
            }

            RefreshOceanDepthCache(UnionRect(_lastDepthBoundsXZ, additionalBoundsXZ), _seaLevelWorldY);
        }

        public void ApplySeaLevel(float seaLevelWorldY)
        {
            _seaLevelWorldY = seaLevelWorldY;
            if (_oceanInstance == null)
                return;

            var position = _oceanInstance.transform.position;
            position.y = seaLevelWorldY;
            _oceanInstance.transform.position = position;
        }

        public void BindProfile(WorldWaterProfile water, HydrologyProfile hydrology)
        {
            _waterProfile = water;
            ApplyProfilePrefabs(water);
            _seaLevelWorldY = ResolveProfileSeaLevel(water, hydrology);
            ApplyOceanMaterialFromProfile();
            EnsureSeaFloorTileBinder().BindProfile(water);
        }

        private void ApplyProfilePrefabs(WorldWaterProfile water)
        {
            if (water == null)
                return;
            if (water.OceanRendererPrefab != null)
                _oceanRendererPrefab = water.OceanRendererPrefab;
            if (water.WaterBodyPrefab != null)
                _waterBodyPrefab = water.WaterBodyPrefab;
        }

        private float ResolveProfileSeaLevel(WorldWaterProfile water, HydrologyProfile hydrology)
        {
            if (hydrology != null)
                return hydrology.SeaLevelWorldY;
            return water != null ? water.SeaLevelWorldY : _seaLevelWorldY;
        }

        private CrestSeaFloorTileBinder EnsureSeaFloorTileBinder()
        {
            var binder = GetComponent<CrestSeaFloorTileBinder>()
                         ?? GetComponentInChildren<CrestSeaFloorTileBinder>(true);
            return binder != null ? binder : gameObject.AddComponent<CrestSeaFloorTileBinder>();
        }

        private static Rect UnionRect(Rect a, Rect b)
        {
            var xMin = Mathf.Min(a.xMin, b.xMin);
            var yMin = Mathf.Min(a.yMin, b.yMin);
            var xMax = Mathf.Max(a.xMax, b.xMax);
            var yMax = Mathf.Max(a.yMax, b.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
