using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    [Serializable]
    public sealed class RiverPolyline
    {
        public ulong StableId;
        public ulong RiverSystemStableId;
        public ulong ParentRiverStableId;
        public RiverKind Kind;
        public Vector2[] PointsXZ = Array.Empty<Vector2>();
        public float[] WidthMeters = Array.Empty<float>();
        public float[] DepthMeters = Array.Empty<float>();
        public float FlowAccumulation;

        /// <summary>True when tracing reached an ocean cell and presentation may bridge to its seam.</summary>
        public bool HasOceanMouth;
        public Vector2 OceanMouthXZ;

        public bool HasConfluence;
        public Vector2 ConfluenceXZ;

        /// <summary>Lake this river leaves from or enters (0 = none).</summary>
        public ulong SourceLakeStableId;

        /// <summary>Lake this river enters (0 = none). Set when tracing stops at lakeMask.</summary>
        public ulong JoinLakeStableId;
    }

    public enum RiverKind
    {
        MainStem = 0,
        Tributary = 1
    }

    [Serializable]
    public sealed class LakeRecord
    {
        public ulong StableId;
        public Vector2 CenterXZ;
        public float SurfaceWorldY;
        public float AreaMetersSq;
        public float MaxDepthMeters;
        public Rect BoundsXZ;
        public Vector2[] OutlineXZ = Array.Empty<Vector2>();
        public float EstimatedVolumeMetersCubed;
        public bool HasOutlet;
        public Vector2 OutletXZ;
        public ulong OutletRiverStableId;
        public ulong[] InletRiverStableIds = Array.Empty<ulong>();
        public ulong ConnectedRiverSystemStableId;

        /// <summary>Basin cell centers from extract (not an ordered shoreline).</summary>
        [FormerlySerializedAs("ShoreXZ")]
        public Vector2[] BasinCellCentersXZ = Array.Empty<Vector2>();
    }

    /// <summary>Row-major hydrology fields plus immutable river/lake records.</summary>
    public sealed class HydrologyPlan
    {
        public int Width { get; }
        public int Height { get; }
        public float CellSizeMeters { get; }
        public Vector2 OriginXZ { get; }

        public WorldWaterClass[] WaterClass { get; }
        public float[] SurfaceWorldY { get; }
        public float[] DepthMeters { get; }
        public float[] DistanceToWaterMeters { get; }
        public float[] FlowAccumulation { get; }

        public RiverPolyline[] Rivers { get; private set; }
        public LakeRecord[] Lakes { get; private set; }
        public IReadOnlyList<WaterCrossing> Crossings { get; private set; }
        /// <summary>Shared carve/Crest/road footprint, created once per hydrology plan.</summary>
        public InlandWaterFootprintPlan FootprintPlan { get; private set; }

        public void SetCrossings(IReadOnlyList<WaterCrossing> crossings) =>
            Crossings = crossings ?? Array.Empty<WaterCrossing>();

        public HydrologyPlan(IReadOnlyList<WaterCrossing> crossings = null)
            : this(1, 1, 16f, Vector2.zero, Array.Empty<RiverPolyline>(), Array.Empty<LakeRecord>(), crossings)
        {
        }

        public HydrologyPlan(
            int width,
            int height,
            float cellSizeMeters,
            Vector2 originXZ,
            RiverPolyline[] rivers = null,
            LakeRecord[] lakes = null,
            IReadOnlyList<WaterCrossing> crossings = null)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            CellSizeMeters = Mathf.Max(0.01f, cellSizeMeters);
            OriginXZ = originXZ;

            var count = Width * Height;
            WaterClass = new WorldWaterClass[count];
            SurfaceWorldY = new float[count];
            DepthMeters = new float[count];
            DistanceToWaterMeters = new float[count];
            FlowAccumulation = new float[count];

            Rivers = rivers ?? Array.Empty<RiverPolyline>();
            Lakes = lakes ?? Array.Empty<LakeRecord>();
            Crossings = crossings ?? Array.Empty<WaterCrossing>();
        }

        public int Index(int x, int z) => z * Width + x;

        public bool TrySampleCell(int x, int z, out WorldWaterSample sample)
        {
            sample = default;
            if (x < 0 || z < 0 || x >= Width || z >= Height) return false;
            var i = Index(x, z);
            sample = new WorldWaterSample(
                WaterClass[i],
                SurfaceWorldY[i],
                DepthMeters[i],
                DistanceToWaterMeters[i]);
            return true;
        }

        public void ReplaceWaterFeatures(RiverPolyline[] rivers, LakeRecord[] lakes)
        {
            Rivers = rivers ?? System.Array.Empty<RiverPolyline>();
            Lakes = lakes ?? System.Array.Empty<LakeRecord>();
            FootprintPlan = null;
        }

        public InlandWaterFootprintPlan EnsureFootprintPlan(HydrologyProfile profile) =>
            EnsureFootprintPlan(profile, InlandWaterFootprintOptions.Default);

        public InlandWaterFootprintPlan EnsureFootprintPlan(
            HydrologyProfile profile,
            InlandWaterFootprintOptions options)
        {
            if (FootprintPlan == null)
                FootprintPlan = InlandWaterFootprintPlan.Build(this, profile, options);
            return FootprintPlan;
        }
    }
}
