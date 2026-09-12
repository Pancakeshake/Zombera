using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Crest
{
    /// <summary>
    /// Inland Crest presenter matching the authored lake/river spline stack.
    /// Ocean edges stay on CrestOceanWaterBackend.
    /// </summary>
    [AddComponentMenu("Zombera/World/Crest World Water Renderer")]
    [DisallowMultipleComponent]
    public sealed class CrestWorldWaterRenderer : MonoBehaviour, IWorldWaterRenderer, IWorldWaterProfileBinder
    {
        [SerializeField] private CrestOceanWaterBackend _oceanBackend;
        [SerializeField] private WorldWaterProfile _waterProfile;
        private HydrologyProfile _hydrologyProfile;

        private void Awake() => ResolveBackends();

        private void OnValidate() => ResolveBackends();

        public IEnumerator BuildSurfaces(
            HydrologyPlan hydrology,
            IReadOnlyList<WaterCrossing> crossings,
            WorldBuildScope scope)
        {
            _ = crossings;
            ResolveBackends();
            Clear(scope);

            if (hydrology == null)
                yield break;
            if (_oceanBackend == null)
                throw new InvalidOperationException(
                    "CrestWorldWaterRenderer is bound, but CrestOceanWaterBackend is missing.");

            var seaLevel = ResolveSeaLevel(hydrology);
            var bounds = ResolveDepthBounds(hydrology, scope);
            if (!_oceanBackend.EnsureOceanSimulator(seaLevel, bounds))
                throw new InvalidOperationException(
                    "CrestWorldWaterRenderer could not ensure the Crest ocean simulator.");

            CrestOceanConfigurator.EnsureFlowAndFoam(_oceanBackend.OceanRenderer, _waterProfile);
            var inland = SpawnCrestSystems(hydrology, scope, seaLevel);
            while (inland.MoveNext())
                yield return inland.Current;

            _oceanBackend.RefreshOceanDepthCache(bounds, seaLevel);
        }

        private IEnumerator SpawnCrestSystems(HydrologyPlan hydrology, WorldBuildScope scope, float seaLevel)
        {
            var waterBodies = _oceanBackend.EnsureInlandWaterFolder();
            if (waterBodies == null)
                throw new InvalidOperationException("Crest WaterBodies folder could not be created.");
            if (_hydrologyProfile == null)
                throw new InvalidOperationException(
                    "CrestWorldWaterRenderer needs a HydrologyProfile: inland Crest splines are generated " +
                    "from the shared InlandWaterFootprintPlan and cannot fall back to scene-authored widths.");

            // The carve's waterline footprint — not the authored example width — is authoritative.
            var footprint = hydrology.EnsureFootprintPlan(
                _hydrologyProfile,
                InlandWaterFootprintOptions.FromWater(_waterProfile));
            SpawnLakes(hydrology, footprint, scope, seaLevel, waterBodies);
            yield return null;
            SpawnRivers(hydrology, footprint, scope, seaLevel, waterBodies);
        }

        private void SpawnLakes(
            HydrologyPlan hydrology,
            InlandWaterFootprintPlan footprint,
            WorldBuildScope scope,
            float seaLevel,
            Transform lakeFolder)
        {
            if (hydrology.Lakes == null || hydrology.Lakes.Length == 0)
                return;

            CrestLakeSplineBuilder.SpawnLakes(
                lakeFolder,
                hydrology,
                footprint,
                scope,
                _waterProfile,
                CrestOceanConfigurator.ResolveInlandSpectrum(
                    _waterProfile != null ? _waterProfile.LakeSpectrum : null,
                    river: false),
                seaLevel);
        }

        private void SpawnRivers(
            HydrologyPlan hydrology,
            InlandWaterFootprintPlan footprint,
            WorldBuildScope scope,
            float seaLevel,
            Transform riverFolder)
        {
            if (hydrology.Rivers == null || hydrology.Rivers.Length == 0)
                return;

            CrestRiverSplineBuilder.SpawnRivers(
                riverFolder,
                hydrology,
                footprint,
                scope,
                _waterProfile,
                CrestOceanConfigurator.ResolveInlandSpectrum(
                    _waterProfile != null ? _waterProfile.RiverSpectrum : null,
                    river: true),
                seaLevel);
        }

        public void Clear(WorldBuildScope scope)
        {
            _ = scope;
            ResolveBackends();
            // One shared inland folder: clears current and retired topology so rebuilds cannot
            // leave duplicate river/lake roots behind.
            _oceanBackend?.ClearInlandWater();
        }

        public void TearDown() => Clear(default);

        public void BindProfile(WorldWaterProfile water, HydrologyProfile hydrology)
        {
            _waterProfile = water;
            _hydrologyProfile = hydrology;
            ResolveBackends();
            _oceanBackend?.BindProfile(water, hydrology);
        }

        private float ResolveSeaLevel(HydrologyPlan hydrology)
        {
            if (_oceanBackend != null && _oceanBackend.HasActiveOcean)
                return _oceanBackend.CurrentSeaLevelWorldY;
            if (hydrology?.WaterClass == null || hydrology.SurfaceWorldY == null)
                return 0f;
            for (var i = 0; i < hydrology.WaterClass.Length; i++)
            {
                if (hydrology.WaterClass[i] == WorldWaterClass.Ocean)
                    return hydrology.SurfaceWorldY[i];
            }

            return 0f;
        }

        private static Rect ResolveDepthBounds(HydrologyPlan hydrology, WorldBuildScope scope)
        {
            if (scope.Kind != WorldBuildScopeKind.FullMap && scope.BoundsXZ.width > 0f && scope.BoundsXZ.height > 0f)
                return scope.BoundsXZ;
            return new Rect(
                hydrology.OriginXZ.x,
                hydrology.OriginXZ.y,
                hydrology.Width * hydrology.CellSizeMeters,
                hydrology.Height * hydrology.CellSizeMeters);
        }

        private void ResolveBackends()
        {
            if (_oceanBackend == null)
            {
                _oceanBackend = GetComponent<CrestOceanWaterBackend>()
                                ?? GetComponentInChildren<CrestOceanWaterBackend>(true);
            }
        }
    }
}
