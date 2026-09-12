#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    public static partial class ModularSingleLevelHouseGeneratorTool
    {
        private static readonly HashSet<int> s_EmptyIntSet = new();

        private static readonly string[] s_DefaultRoomNames =
        {
            "Lounge", "Kitchen", "Bathroom", "Bedroom", "Hallway", "Dining", "Storage"
        };

        /// <summary>
        ///     Editor-visualisation colours for each RoomType so designers can distinguish
        ///     rooms on the floor plan at a glance.
        /// </summary>
        private static readonly Dictionary<RoomType, Color> RoomTypeColors = new Dictionary<RoomType, Color>
        {
            [RoomType.LivingRoom] = new Color(0.95f, 0.78f, 0.46f, 0.55f),  // warm gold
            [RoomType.Kitchen] = new Color(0.90f, 0.55f, 0.35f, 0.55f),    // terracotta
            [RoomType.Bedroom] = new Color(0.38f, 0.68f, 0.90f, 0.55f),    // soft blue
            [RoomType.Bathroom] = new Color(0.50f, 0.80f, 0.85f, 0.55f),   // aqua
            [RoomType.Hallway] = new Color(0.82f, 0.82f, 0.82f, 0.55f),    // light grey
            [RoomType.Dining] = new Color(0.85f, 0.62f, 0.50f, 0.55f),     // warm peach
            [RoomType.Storage] = new Color(0.65f, 0.60f, 0.72f, 0.55f),    // muted lavender
            [RoomType.Garage] = new Color(0.55f, 0.55f, 0.60f, 0.55f),     // grey
            [RoomType.Entry] = new Color(0.72f, 0.80f, 0.68f, 0.55f),      // sage
            [RoomType.Utility] = new Color(0.58f, 0.65f, 0.72f, 0.55f),    // steel blue
            [RoomType.SalesFloor] = new Color(0.35f, 0.85f, 0.70f, 0.55f), // mint
            [RoomType.Stockroom] = new Color(0.62f, 0.52f, 0.40f, 0.55f),  // brown-grey
            [RoomType.CommercialKitchen] = new Color(0.95f, 0.45f, 0.25f, 0.55f), // deep orange
            [RoomType.ChangeRoom] = new Color(0.95f, 0.60f, 0.75f, 0.55f), // pink
            [RoomType.Office] = new Color(0.55f, 0.65f, 0.85f, 0.55f),     // blue-grey
            [RoomType.StaffRoom] = new Color(0.45f, 0.80f, 0.85f, 0.55f)   // teal
        };

        private static Color ResolveRoomTypeColor(RoomType type)
        {
            return RoomTypeColors.TryGetValue(type, out var c) ? c : new Color(0.75f, 0.75f, 0.75f, 0.55f);
        }

        /// <summary>
        ///     Returns the base room type colour with a slight random hue and saturation
        ///     shift so each room instance looks distinct, even within the same type.
        ///     Alpha is preserved from the base colour.
        /// </summary>
        private static Color ResolveRandomizedRoomColor(RoomType type)
        {
            var baseColor = ResolveRoomTypeColor(type);
            float h, s, v;
            Color.RGBToHSV(baseColor, out h, out s, out v);

            // Jitter hue by ±0.04 (keeps rooms in the same colour family)
            h = (h + UnityEngine.Random.Range(-0.04f, 0.04f) + 1f) % 1f;
            // Jitter saturation by ±0.08
            s = Mathf.Clamp01(s + UnityEngine.Random.Range(-0.08f, 0.08f));
            // Jitter value (brightness) by ±0.06
            v = Mathf.Clamp01(v + UnityEngine.Random.Range(-0.06f, 0.06f));

            var randomized = Color.HSVToRGB(h, s, v);
            randomized.a = baseColor.a; // preserve alpha from base
            return randomized;
        }

        private readonly struct FloorCell : IEquatable<FloorCell>
        {
            public readonly int Level;
            public readonly int Ix;
            public readonly int Iz;

            public FloorCell(int level, int ix, int iz)
            {
                Level = level;
                Ix = ix;
                Iz = iz;
            }

            public bool Equals(FloorCell other) =>
                Level == other.Level && Ix == other.Ix && Iz == other.Iz;

            public override bool Equals(object obj)
            {
                return obj is FloorCell other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Level, Ix, Iz);
            }
        }

        private readonly struct StairTransition
        {
            public readonly int LowerLevel;
            public readonly int Ix;
            public readonly int Iz;
            public readonly float RotationY;

            public StairTransition(int lowerLevel, int ix, int iz, float rotationY)
            {
                LowerLevel = lowerLevel;
                Ix = ix;
                Iz = iz;
                RotationY = rotationY;
            }
        }

        private readonly struct WallSegment
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public WallSegment(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }
        }

        private readonly struct InteriorWallSegment
        {
            public readonly WallSegment Segment;
            public readonly bool AllowDoorway;

            public InteriorWallSegment(WallSegment segment, bool allowDoorway)
            {
                Segment = segment;
                AllowDoorway = allowDoorway;
            }
        }

        private readonly struct RoomRegion
        {
            public readonly int Ix0;
            public readonly int Iz0;
            public readonly int Ix1;
            public readonly int Iz1;
            public readonly string Name;
            public readonly RoomType RoomType;

            /// <summary>
            ///     Legacy constructor for BSP rooms (no explicit room type).
            /// </summary>
            public RoomRegion(int ix0, int iz0, int ix1, int iz1, string name)
            {
                Ix0 = ix0;
                Iz0 = iz0;
                Ix1 = ix1;
                Iz1 = iz1;
                Name = name;
                RoomType = RoomType.LivingRoom; // fallback
            }

            /// <summary>
            ///     Template-driven constructor with explicit room type.
            /// </summary>
            public RoomRegion(int ix0, int iz0, int ix1, int iz1, string name, RoomType roomType)
            {
                Ix0 = ix0;
                Iz0 = iz0;
                Ix1 = ix1;
                Iz1 = iz1;
                Name = name;
                RoomType = roomType;
            }
        }

        /// <summary>
        ///     A concrete room slot resolved from a template, carrying only a target floor percentage.
        ///     Every cell is always filled — rooms may expand beyond their target if needed.
        /// </summary>
        private readonly struct RoomSlot
        {
            public readonly RoomType Type;
            public readonly int TargetPct;

            public RoomSlot(RoomType type, int targetPct)
            {
                Type = type;
                TargetPct = targetPct;
            }
        }

        private readonly struct PlannedPerimeterSegment
        {
            public readonly WallSegment Segment;
            public readonly int SegmentIndex;
            public readonly bool IsDoor;
            public readonly bool IsWindow;

            public PlannedPerimeterSegment(WallSegment segment, int segmentIndex, bool isDoor, bool isWindow)
            {
                Segment = segment;
                SegmentIndex = segmentIndex;
                IsDoor = isDoor;
                IsWindow = isWindow;
            }
        }

        private readonly struct PlannedInteriorWallSegment
        {
            public readonly WallSegment Segment;
            public readonly int RoomA;
            public readonly int RoomB;
            public readonly int SegmentIndex;
            public readonly bool IsDoorway;

            public PlannedInteriorWallSegment(WallSegment segment, int roomA, int roomB, int segmentIndex,
                bool isDoorway)
            {
                Segment = segment;
                RoomA = roomA;
                RoomB = roomB;
                SegmentIndex = segmentIndex;
                IsDoorway = isDoorway;
            }
        }

        private sealed class LevelWallPlan
        {
            internal int Level;
            internal float WallY;
            internal readonly List<PlannedPerimeterSegment> Segments = new();
        }

        private sealed class FloorRoomPlan
        {
            internal int Level;
            internal float FloorY;
            internal int FloorPlanWidth;
            internal int FloorPlanDepth;
            internal List<RoomRegion> Rooms = new();
            internal List<PlannedInteriorWallSegment> InteriorWalls = new();
            /// <summary>Per-cell room ownership grid: -1 = unowned, otherwise the region index into Rooms.</summary>
            internal int[,] RoomAt;
        }

        private sealed class GenerationPlan
        {
            internal int Width;
            internal int Depth;
            internal int FloorCount;
            internal bool NeedsStairs;
            internal StairTransition[] StairTransitions = Array.Empty<StairTransition>();
            internal HashSet<FloorCell> SkipUpperLandingFloors = new();
            internal HashSet<int> GroundDoorIndices = s_EmptyIntSet;

            /// <summary>Perimeter segment index of the primary ground door (the building's front).</summary>
            internal int PrimaryDoorSegmentIndex = -1;

            /// <summary>True when the storefront glass continues around a corner onto one side wall.</summary>
            internal bool CornerStorefront;

            /// <summary>When <see cref="CornerStorefront"/>, true = glass wraps the left corner.</summary>
            internal bool CornerSideIsLeft;
            internal readonly List<LevelWallPlan> PerimeterWallPlans = new();
            internal readonly List<FloorRoomPlan> FloorRoomPlans = new();

            // Skyscraper mode — per-floor footprint shrinkage
            internal bool SkyscraperMode;
            internal int[] FloorWidths = Array.Empty<int>();
            internal int[] FloorDepths = Array.Empty<int>();
            internal Vector3[] FloorOffsets = Array.Empty<Vector3>();
            internal int MinFootprintWidth;
            internal int MinFootprintDepth;

            /// <summary>
            /// Number of stair transitions to place. In skyscraper mode this is capped at
            /// the shrink start floor since stairs only exist in the full-footprint zone;
            /// upper tapered floors are decorative only.
            /// </summary>
            internal int EffectiveStairCount;
        }

        private sealed class PrefabDependencies
        {
            internal string FoundationPath;
            internal string FloorPath;
            internal string WallPath;
            internal string DoorwayPath;
            internal string ExteriorDoorPath;
            internal string InteriorDoorPath;
            internal string WindowPath;
            internal string ShopGlassFullPath;
            internal string ShopGlassCapLeftPath;
            internal string ShopGlassCapRightPath;
            internal string ShopGlassCapBothPath;
            internal string StairPath;
            internal string UpperFloorPath;
            internal string RoofPath;
            internal string RoofRidgePath;
            internal string RoofPanelPath;
            internal string RoofGablePath;
            internal string ExteriorStairsPath;
            internal string WindowClosedPath;
            internal string WindowMouldingPath;
            internal string Gutter3mPath;
            internal string GutterBracketsPath;
            internal string DownpipePath;

            internal GameObject Foundation;
            internal GameObject Floor;
            internal GameObject Wall;
            internal GameObject Doorway;
            internal GameObject ExteriorDoor;
            internal GameObject InteriorDoor;
            internal GameObject Window;
            internal GameObject ShopGlassFull;
            internal GameObject ShopGlassCapLeft;
            internal GameObject ShopGlassCapRight;
            internal GameObject ShopGlassCapBoth;
            internal GameObject Stair;
            internal GameObject UpperFloor;
            internal GameObject Roof;
            internal GameObject RoofRidge;
            internal GameObject RoofPanel;
            internal GameObject RoofGable;
            internal GameObject ExteriorStairs;
            internal GameObject WindowClosed;
            internal GameObject WindowMoulding;
            internal GameObject Gutter3m;
            internal GameObject GutterBrackets;
            internal GameObject Downpipe;
        }

        private sealed class GenerationContext
        {
            internal GeneratorSettings Settings;
            internal GenerationRandom Random;
            internal string KitFolder;
            internal string OutputFolder;
            internal readonly GenerationPlan Plan = new();
            internal readonly PrefabDependencies Prefabs = new();
            internal CityDistrictType BuildingCategory = CityDistrictType.Residential;
            internal string AssetFileName;
            internal string AssetPath;

            /// <summary>
            ///     The active <see cref="ResidentialHouseTemplate"/> resolved from the ArchetypeLibrary
            ///     and SelectedTemplateIndex. Null when using legacy manual room counts.
            /// </summary>
            internal ResidentialHouseTemplate ActiveTemplate;

            /// <summary>Cached material shared across all interior override doors for this generation.</summary>
            internal Material CachedInteriorDoorMaterial;

            /// <summary>
            ///     The resolved <see cref="RoofTypeConfig"/> for this generation.
            ///     Picked randomly from <see cref="ResidentialHouseTemplate.AllowedRoofTypes"/>
            ///     when the active template has entries; null falls back to BuildingKitConfig defaults.
            /// </summary>
            internal RoofTypeConfig ActiveRoofType;
        }

        private sealed class GenerationRandom
        {
            private readonly System.Random _random;

            internal GenerationRandom(int seed)
            {
                _random = new System.Random(seed);
            }

            internal int RangeExclusive(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive)
                    return minInclusive;

                return _random.Next(minInclusive, maxExclusive);
            }

            internal int RangeInclusive(int minInclusive, int maxInclusive)
            {
                if (maxInclusive <= minInclusive)
                    return minInclusive;

                return _random.Next(minInclusive, maxInclusive + 1);
            }

            internal float Value01()
            {
                return (float)_random.NextDouble();
            }

            internal System.Random ToSystemRandom()
            {
                return _random;
            }
        }

        private sealed class UnityRandomStateScope : IDisposable
        {
            private readonly UnityEngine.Random.State _captured;

            public UnityRandomStateScope()
            {
                _captured = UnityEngine.Random.state;
            }

            public void Dispose()
            {
                UnityEngine.Random.state = _captured;
            }
        }
    }
}
#endif
