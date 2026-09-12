using UnityEngine;

namespace Zombera.World.Roads.Testing
{
    /// <summary>
    ///     One-click city road generate for the Road System scene.
    ///     Inspector button drives <see cref="CityPrefabRoadNetworkBuilder.GenerateCityRoadNetwork"/>.
    /// </summary>
    [AddComponentMenu("Zombera/Testing/Road Tester Generate")]
    [DisallowMultipleComponent]
    public sealed class RoadTesterGenerate : MonoBehaviour
    {
        [Header("Pipeline")]
        [SerializeField] private CityPrefabRoadNetworkBuilder cityBuilder;
        [SerializeField] private Transform groundReference;
        [Tooltip("When enabled, ignores cached roads and always rebuilds.")]
        [SerializeField] private bool forceRebuild = true;
        [Tooltip("Match World Builder fast path — leave off for quicker iterates.")]
        [SerializeField] private bool recordTerrainUndo;

        [Header("Optional Assets (auto-wired by Ensure if empty)")]
        [SerializeField] private CityMathRoadLayoutAsset layoutAsset;
        [SerializeField] private RoadNetworkSettings roadNetworkSettings;
        [SerializeField] private CityBuildConfig buildConfig;

        public CityPrefabRoadNetworkBuilder CityBuilder => cityBuilder;
        public Transform GroundReference => groundReference;
        public bool ForceRebuild => forceRebuild;
        public bool RecordTerrainUndo => recordTerrainUndo;
        public CityMathRoadLayoutAsset LayoutAsset => layoutAsset;
        public RoadNetworkSettings RoadNetworkSettings => roadNetworkSettings;
        public CityBuildConfig BuildConfig => buildConfig;

        public void SetCityBuilder(CityPrefabRoadNetworkBuilder builder) => cityBuilder = builder;
        public void SetGroundReference(Transform ground) => groundReference = ground;
        public void SetLayoutAsset(CityMathRoadLayoutAsset asset) => layoutAsset = asset;
        public void SetRoadNetworkSettings(RoadNetworkSettings settings) => roadNetworkSettings = settings;
        public void SetBuildConfig(CityBuildConfig config) => buildConfig = config;

        /// <summary>
        ///     Runs city road generation. Call EnsureReady from the custom editor first.
        /// </summary>
        public void Generate()
        {
            if (cityBuilder == null)
            {
                Debug.LogError(
                    "[RoadTesterGenerate] CityPrefabRoadNetworkBuilder is not assigned. Use Ensure Stack first.",
                    this);
                return;
            }

            var previousUndo = cityBuilder.RecordTerrainUndo;
            var previousReuse = cityBuilder.ReuseCachedRoadsOnSameSeed;
            try
            {
                cityBuilder.RecordTerrainUndo = recordTerrainUndo;
                if (forceRebuild)
                    cityBuilder.ReuseCachedRoadsOnSameSeed = false;

                Debug.Log("[RoadTesterGenerate] Generating city roads via Procedural…", this);
                cityBuilder.GenerateCityRoadNetwork(useTestLayout: false);

                if (!cityBuilder.HasGeneratedRoadNetwork)
                {
                    Debug.LogError(
                        "[RoadTesterGenerate] Generate finished without a published road network. Check console warnings.",
                        this);
                    return;
                }

                Debug.Log(
                    "[RoadTesterGenerate] Done. Meshes should be under '" +
                    ProceduralRoadNetworkNames.NetworkRoot + "/" + ProceduralRoadNetworkNames.Asphalt + "'.",
                    this);
            }
            finally
            {
                cityBuilder.RecordTerrainUndo = previousUndo;
                cityBuilder.ReuseCachedRoadsOnSameSeed = previousReuse;
            }
        }
    }
}
