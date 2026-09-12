using System;
using System.Reflection;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;
using Zombera.World.CityPipeline.WorldBuilder.Views;
using Zombera.World.Roads;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Idempotent runtime wiring for the scene-visible World Builder stack.
    /// Used by the pipeline and editor provisioning.
    /// </summary>
    public static class WorldBuilderStackBootstrap
    {
        private const string CrestOceanBackendType = "Zombera.World.Crest.CrestOceanWaterBackend";
        private const string CrestWorldWaterRendererType = "Zombera.World.Crest.CrestWorldWaterRenderer";
        private const string CrestWeatherAdapterType = "Zombera.World.Crest.CrestWeatherAdapter";
        private const string EnviroEnvironmentBackendType = "Zombera.World.Enviro.EnviroWorldEnvironmentBackend";
        private const string LegacySplineRiverBackendType =
            "Zombera.World.CityPipeline.WorldBuilder.SplineRiverSurfaceBackend";

        private static bool _loggedEnsure;

        public static bool Ensure(
            WorldBuilderService service,
            CityPrefabRoadNetworkBuilder cityBuilder = null,
            WorldGenerationProfile profile = null)
        {
            if (service == null)
                return false;

            var go = service.gameObject;
            var changed = false;

            var catalog = EnsureComponent<WorldTileCatalog>(go, ref changed);
            var stateManager = EnsureComponent<WorldStateManager>(go, ref changed);
            var viewRegistry = EnsureComponent<WorldStateViewRegistry>(go, ref changed);
            var materializer = EnsureComponent<WorldBuildingMaterializer>(go, ref changed);
            var surfacePainter = EnsureComponent<WorldSurfacePainter>(go, ref changed);
            var syncBackend = EnsureComponent<MicroSplatAlphamapSyncBackend>(go, ref changed);
            var weatherDirector = EnsureComponent<WorldWeatherDirector>(go, ref changed);
            var naturePlacer = EnsureComponent<WorldNaturePlacer>(go, ref changed);
            var poiSink = EnsureComponent<WorldPoiSink>(go, ref changed);

            var oceanWater = EnsureIntegrationComponent(go, CrestOceanBackendType, ref changed);
            var crestInlandWater = EnsureIntegrationComponent(go, CrestWorldWaterRendererType, ref changed);
            EnsureIntegrationComponent(go, CrestWeatherAdapterType, ref changed);
            var environmentBackend = EnsureIntegrationComponent(go, EnviroEnvironmentBackendType, ref changed);

            if (surfacePainter != null && syncBackend != null)
                changed |= surfacePainter.BindSyncBackend(syncBackend);

            changed |= RemoveLegacyComponent(go, LegacySplineRiverBackendType);

            var inlandWater = crestInlandWater;

            changed |= service.BindPipelineReferences(new WorldBuilderPipelineBindArgs
            {
                Profile = profile,
                TileCatalog = catalog,
                StateManager = stateManager,
                SurfacePainter = surfacePainter,
                OceanWaterRenderer = oceanWater,
                WaterRenderer = inlandWater,
                EnvironmentBackend = environmentBackend,
                WeatherSource = weatherDirector,
                NaturePlacer = naturePlacer,
                PoiSink = poiSink
            });

#if UNITY_EDITOR
            var editorResolver = EnsureComponent<EditorAssembledBuildingArchetypeResolver>(go, ref changed);
            editorResolver?.Configure(cityBuilder);
            materializer?.Configure(stateManager, catalog, viewRegistry, editorResolver);
#else
            materializer?.Configure(stateManager, catalog, viewRegistry, null);
#endif

            if (changed && !_loggedEnsure)
            {
                Debug.Log("[WorldBuilderStackBootstrap] Ensured World Builder pipeline stack.", service);
                _loggedEnsure = true;
            }

            return changed;
        }

        public static bool IsComplete(WorldBuilderService service)
        {
            if (service == null)
                return false;

            var go = service.gameObject;
            if (go.GetComponent<WorldTileCatalog>() == null) return false;
            if (go.GetComponent<WorldStateManager>() == null) return false;
            if (go.GetComponent<WorldStateViewRegistry>() == null) return false;
            if (go.GetComponent<WorldBuildingMaterializer>() == null) return false;
            if (go.GetComponent<WorldSurfacePainter>() == null) return false;
            if (go.GetComponent<MicroSplatAlphamapSyncBackend>() == null) return false;
            if (go.GetComponent<WorldWeatherDirector>() == null) return false;
            if (FindType(CrestWorldWaterRendererType) != null &&
                go.GetComponent(FindType(CrestWorldWaterRendererType)) == null)
                return false;
            if (FindType(CrestOceanBackendType) != null &&
                go.GetComponent(FindType(CrestOceanBackendType)) == null)
                return false;
            if (FindType(EnviroEnvironmentBackendType) != null &&
                go.GetComponent(FindType(EnviroEnvironmentBackendType)) == null)
                return false;

            return service.TileCatalog != null
                   && service.StateManager != null
                   && service.SurfacePainter != null
                   && service.OceanWaterRenderer != null
                   && service.WaterRenderer != null
                   && service.WeatherSource != null
                   && service.EnvironmentBackend != null
                   && service.NaturePlacer != null
                   && service.PoiSink != null;
        }

        private static T EnsureComponent<T>(GameObject go, ref bool changed) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing != null)
                return existing;

            changed = true;
            return go.AddComponent<T>();
        }

        private static MonoBehaviour EnsureIntegrationComponent(
            GameObject go,
            string typeName,
            ref bool changed)
        {
            var type = FindType(typeName);
            if (type == null)
                return null;

            var existing = go.GetComponent(type) as MonoBehaviour;
            if (existing != null)
                return existing;

            changed = true;
            return go.AddComponent(type) as MonoBehaviour;
        }

        private static bool RemoveLegacyComponent(GameObject go, string typeName)
        {
            var type = FindType(typeName);
            if (type == null)
                return false;

            var existing = go.GetComponent(type);
            if (existing == null)
                return false;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(existing);
            else
                UnityEngine.Object.DestroyImmediate(existing);
            return true;
        }

        private static Type FindType(string typeName)
        {
            var direct = Type.GetType(typeName);
            if (direct != null)
                return direct;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    var type = assemblies[i].GetType(typeName);
                    if (type != null)
                        return type;
                }
                catch (ReflectionTypeLoadException)
                {
                    // Ignore unloadable assemblies.
                }
            }

            return null;
        }
    }
}
