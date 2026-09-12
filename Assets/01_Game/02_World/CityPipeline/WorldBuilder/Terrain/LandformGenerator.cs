using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    /// Continental shelf + partitioned hills + residual mountain detail + orogen SDF.
    /// Orogen height is owned solely by <see cref="InteriorLandformRelief"/>.
    /// </summary>
    public static partial class LandformGenerator
    {
        public static LandformField Generate(
            WorldPlan plan,
            LandformProfile landformProfile,
            HydrologyProfile hydrologyProfile,
            TerrainGridProfile gridProfile,
            int landformSeed,
            bool fastLandforms = false)
        {
            return GenerateWithOrogen(
                plan,
                landformProfile,
                hydrologyProfile,
                gridProfile,
                landformSeed,
                fastLandforms,
                out _);
        }

        public static LandformField GenerateWithOrogen(
            WorldPlan plan,
            LandformProfile landformProfile,
            HydrologyProfile hydrologyProfile,
            TerrainGridProfile gridProfile,
            int landformSeed,
            bool fastLandforms,
            out OrogenPlan orogenPlan)
        {
            var totalWatch = Stopwatch.StartNew();
            orogenPlan = new OrogenPlan(
                System.Array.Empty<InteriorLandformRelief.MountainRange>(),
                System.Array.Empty<Vector2>());

            var field = new LandformField(
                plan.Width,
                plan.Height,
                plan.CellSizeMeters,
                plan.PlanningOriginXZ);

            var continental = new DeterministicNoise2D(landformSeed + landformProfile.ContinentalnessSeedOffset);
            var warp = new DeterministicNoise2D(landformSeed + landformProfile.DomainWarpSeedOffset);
            var hills = new DeterministicNoise2D(landformSeed + landformProfile.HillsSeedOffset);
            var mountains = new DeterministicNoise2D(landformSeed + landformProfile.MountainSeedOffset);
            var barrierNoise = new DeterministicNoise2D(landformSeed + landformProfile.EdgeBarrierSeedOffset);

            var seaLevel = hydrologyProfile != null ? hydrologyProfile.SeaLevelWorldY : 0f;
            var baseY = gridProfile != null ? gridProfile.GetTerrainBaseY(seaLevel) : seaLevel - 200f;
            var maxY = baseY + (gridProfile != null ? gridProfile.TerrainVerticalSize : 1000f);
            var heightMin = baseY + 0.01f;
            var heightMax = maxY - 0.01f;
            var bounds = plan.Session.WorldBoundsXZ;
            var plainsBias = landformProfile.PlainsBias;
            var boundaryLayout = WorldMapBoundaryLayout.Resolve(
                plan.Session,
                landformProfile);

            var rangesWatch = Stopwatch.StartNew();
            var interiorRanges = InteriorLandformRelief.BuildRanges(
                landformSeed,
                landformProfile,
                bounds,
                boundaryLayout);
            orogenPlan = InteriorLandformRelief.BuildOrogenPlan(interiorRanges);
            rangesWatch.Stop();

            var interiorRidge = new DeterministicNoise2D(
                landformSeed + landformProfile.InteriorMountainSeedOffset + 7);
            var rollingNoise = new DeterministicNoise2D(
                landformSeed + landformProfile.InteriorMountainSeedOffset + 13);

            var cScale = Mathf.Max(1f, landformProfile.ContinentalnessScale);
            var hScale = Mathf.Max(1f, landformProfile.HillsScale);
            var mScale = Mathf.Max(1f, landformProfile.MountainScale);
            var invCScale = 1f / cScale;
            var invHScale = 1f / hScale;
            var invMScale = 1f / mScale;
            var invHScaleRidged = 1f / (hScale * 0.65f);
            var invMScaleRidged = 1f / (mScale * 0.42f);
            var invMScaleBarrier = 1f / (mScale * 0.48f);
            var originX = field.OriginXZ.x;
            var originZ = field.OriginXZ.y;
            var cellSize = field.CellSize;
            var width = field.Width;
            var height = field.Height;
            var heights = field.WorldHeights;
            var useOrogenAuthority = landformProfile.InteriorMountainsEnabled &&
                                     interiorRanges != null &&
                                     interiorRanges.Length > 0;
            // Snapshot on the main thread — ScriptableObject reads are not safe in Parallel.For.
            var compose = LandformComposeParams.Capture(
                landformProfile,
                bakeMountainMaskCurve: !useOrogenAuthority);

            var composeArgs = new LandformComposeArgs
            {
                Noises = new LandformComposeNoiseSet
                {
                    Continental = continental,
                    Warp = warp,
                    Hills = hills,
                    Mountains = mountains,
                    Barrier = barrierNoise,
                    InteriorRidge = interiorRidge,
                    Rolling = rollingNoise
                },
                Profile = compose,
                BoundaryLayout = boundaryLayout,
                InteriorRanges = interiorRanges,
                Bounds = bounds,
                SeaLevel = seaLevel,
                PlainsBias = plainsBias,
                Scales = new LandformComposeScales
                {
                    InvC = invCScale,
                    InvH = invHScale,
                    InvHRidged = invHScaleRidged,
                    InvM = invMScale,
                    InvMRidged = invMScaleRidged,
                    InvMBarrier = invMScaleBarrier
                },
                Octaves = new LandformComposeOctaves
                {
                    Cont = fastLandforms ? 3 : 5,
                    Hill = fastLandforms ? 3 : 5,
                    HillRidge = fastLandforms ? 2 : 3,
                    Mountain = fastLandforms ? 3 : 5,
                    MountainFine = fastLandforms ? 2 : 3,
                    Barrier = fastLandforms ? 4 : 6,
                    BarrierFine = fastLandforms ? 2 : 4,
                    BarrierMacro = fastLandforms ? 2 : 3
                },
                FastLandforms = fastLandforms,
                UseOrogenAuthority = useOrogenAuthority
            };

            var fillWatch = Stopwatch.StartNew();
            Parallel.For(0, height, z =>
            {
                var centerZ = originZ + (z + 0.5f) * cellSize;
                var row = z * width;
                for (var x = 0; x < width; x++)
                {
                    var centerX = originX + (x + 0.5f) * cellSize;
                    var cellHeight = EvaluateCell(centerX, centerZ, in composeArgs);
                    heights[row + x] = Mathf.Clamp(cellHeight, heightMin, heightMax);
                }
            });
            fillWatch.Stop();
            totalWatch.Stop();

            LastGenerateTiming = new LandformGenerateTiming(
                width,
                height,
                fastLandforms,
                rangesWatch.ElapsedMilliseconds,
                fillWatch.ElapsedMilliseconds,
                totalWatch.ElapsedMilliseconds);

            return field;
        }

        /// <summary>Timing from the most recent <see cref="GenerateWithOrogen"/> call.</summary>
        public static LandformGenerateTiming LastGenerateTiming { get; private set; }

        public readonly struct LandformGenerateTiming
        {
            public readonly int Width;
            public readonly int Height;
            public readonly bool FastLandforms;
            public readonly long BuildRangesMs;
            public readonly long FillHeightsMs;
            public readonly long TotalMs;

            public LandformGenerateTiming(
                int width,
                int height,
                bool fastLandforms,
                long buildRangesMs,
                long fillHeightsMs,
                long totalMs)
            {
                Width = width;
                Height = height;
                FastLandforms = fastLandforms;
                BuildRangesMs = buildRangesMs;
                FillHeightsMs = fillHeightsMs;
                TotalMs = totalMs;
            }
        }
    }
}
