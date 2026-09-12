using Crest;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Ocean prepare/activate and WaterBody re-sync after Crest edit-mode init.
    /// </summary>
    public sealed partial class CrestOceanWaterBackend
    {
        public void ResyncOceanToBounds(Rect boundsXZ, float seaLevelWorldY)
        {
            if (boundsXZ.width <= 0f || boundsXZ.height <= 0f)
                return;

            ApplySeaLevel(seaLevelWorldY);
            ResyncFullMapWaterBody(boundsXZ, seaLevelWorldY);
            RefreshOceanDepthCache(boundsXZ, seaLevelWorldY);
        }

        private void ResyncFullMapWaterBody(Rect boundsXZ, float seaLevelWorldY)
        {
            for (var i = 0; i < _waterBodies.Count; i++)
            {
                var waterBody = _waterBodies[i];
                if (waterBody == null || waterBody.name != "CrestWaterBody_Map")
                    continue;

                var placement = OceanWaterBodyPlacementUtility.CreateFullSquarePlacement(
                    boundsXZ,
                    seaLevelWorldY);
                waterBody.transform.SetPositionAndRotation(placement.Center, Quaternion.identity);
                waterBody.transform.localScale = placement.Scale;
                CrestOceanConfigurator.SyncWaterBody(waterBody);
                return;
            }
        }

        private bool PrepareOcean(float seaLevel)
        {
            if (_oceanInstance != null && _oceanRenderer != null)
            {
                // Do not SetActive(false): edit-mode OnDisable CleanUp tears Root down,
                // and BuildWaterSurfaces would undo BuildOceanSurfaces.
                ApplySeaLevel(seaLevel);
                ApplyOceanMaterialFromProfile();
                CrestOceanConfigurator.Configure(
                    _oceanRenderer,
                    seaLevel,
                    _waterProfile,
                    _lastCoreBoundsXZ,
                    _lastOceanRingTiles);
                return true;
            }

            var prefab = _oceanRendererPrefab;
#if UNITY_EDITOR
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(OceanPrefabPath);
#endif
            if (prefab == null)
                return false;

            _oceanInstance = Instantiate(prefab, _root);
            _oceanInstance.SetActive(false);
            _oceanInstance.name = "CrestOcean";
            _oceanRenderer = _oceanInstance.GetComponent<OceanRenderer>()
                             ?? _oceanInstance.GetComponentInChildren<OceanRenderer>(true);
            if (_oceanRenderer == null)
            {
                DestroyOceanInstance();
                return false;
            }

            ApplySeaLevel(seaLevel);
            ApplyOceanMaterialFromProfile();
            CrestOceanConfigurator.Configure(
                _oceanRenderer,
                seaLevel,
                _waterProfile,
                _lastCoreBoundsXZ,
                _lastOceanRingTiles);
            return true;
        }

        /// <summary>
        /// Applies <see cref="WorldWaterProfile.CrestOceanMaterial"/> when set so look
        /// knobs live on the versioned profile material rather than the prefab alone.
        /// </summary>
        private void ApplyOceanMaterialFromProfile()
        {
            if (_oceanRenderer == null || _waterProfile == null)
                return;

            if (_waterProfile.CrestOceanMaterial is not Material material)
                return;

            if (_oceanRenderer.OceanMaterial == material)
                return;

            _oceanRenderer.OceanMaterial = material;
        }

        private void ActivateOcean()
        {
            if (_oceanInstance == null || _oceanRenderer == null)
                return;

            CrestOceanConfigurator.ApplyBoundedOceanSettings(_oceanRenderer, _waterProfile);
            CrestOceanConfigurator.Configure(
                _oceanRenderer,
                _seaLevelWorldY,
                _waterProfile,
                _lastCoreBoundsXZ,
                _lastOceanRingTiles);
            _oceanInstance.SetActive(true);
            CrestOceanConfigurator.ForceEditModeReady(_oceanRenderer);
        }

        private void SyncAllWaterBodiesUnderRoot()
        {
            if (_root == null || _oceanRenderer == null || _oceanRenderer.Root == null)
                return;

            var bodies = _root.GetComponentsInChildren<WaterBody>(true);
            for (var i = 0; i < bodies.Length; i++)
            {
                var waterBody = bodies[i];
                if (waterBody == null)
                    continue;

                if (!waterBody.gameObject.activeSelf)
                    waterBody.gameObject.SetActive(true);

                CrestOceanConfigurator.SyncWaterBody(waterBody);
            }
        }

        private void ActivateWaterBodies()
        {
            for (var i = 0; i < _waterBodies.Count; i++)
            {
                var waterBody = _waterBodies[i];
                if (waterBody == null)
                    continue;

                waterBody.gameObject.SetActive(true);
                CrestOceanConfigurator.SyncWaterBody(waterBody);
            }
        }

        private void LogWaterBodyState(Rect bounds)
        {
            if (_waterBodies.Count == 0)
                return;

            var sides = new System.Text.StringBuilder();
            for (var i = 0; i < _waterBodies.Count; i++)
            {
                if (i > 0)
                    sides.Append(", ");
                sides.Append(_waterBodies[i].name.Replace("CrestWaterBody_", string.Empty));
            }

            var sample = _waterBodies[0];
            var aabb = sample.AABB;
            var clipEnabled = _oceanRenderer != null && _oceanRenderer.CreateClipSurfaceData;
            var clipDefault = _oceanRenderer != null
                ? _oceanRenderer._defaultClippingState.ToString()
                : "missing";
            Debug.Log(
                "[CrestOceanWaterBackend] Active ocean edge strips on sides: " + sides +
                $". Map bounds ({bounds.xMin:0}, {bounds.yMin:0})-({bounds.xMax:0}, {bounds.yMax:0}). " +
                $"Sample AABB center {aabb.center}, size {aabb.size}. " +
                $"Ocean clip={clipEnabled}, default={clipDefault}.",
                this);
        }
    }
}
