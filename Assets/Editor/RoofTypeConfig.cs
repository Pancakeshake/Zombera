#if UNITY_EDITOR
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Defines the roof construction method used by the building generator.
    /// </summary>
    public enum RoofStyle
    {
        /// <summary>Kit-of-parts: Ridge + two Panels + two Gables, scaled to the footprint.</summary>
        Gable,

        /// <summary>Single tile per cell laid flat (uses <see cref="RoofTypeConfig.TilePrefab"/>).
        /// Good for flat roofs, parapet buildings, or where no peaked roof is desired.</summary>
        Flat,

        /// <summary>Single-slope lean-to: high wall on one side, low wall on the opposite.
        /// Uses Panel + Gable pieces. Good for shacks, garages, and survivor shelters.</summary>
        Shed,

        /// <summary>Asymmetrical gable: ridge offset to one side, one panel longer than the other.
        /// Reuses Ridge + Panel + RightAngleGable pieces. Gables are right-triangles, not isosceles.</summary>
        Saltbox,

        /// <summary>Barn-style two-slope roof: steep lower + shallow upper on each side.
        /// Reuses Panel pieces (4 total) + custom gambrel gable + Ridge cap.</summary>
        Gambrel,
    }

    /// <summary>Which compass edge is the low side of a shed roof.</summary>
    public enum ShedDirection
    {
        North,
        South,
        East,
        West,
    }

    /// <summary>
    ///     ScriptableObject defining a roof type for the modular building generator.
    ///     Encapsulates prefab references and all scale/offset parameters that were previously
    ///     hardcoded in <c>PlaceScaledRoofAssembly</c>.
    ///
    ///     Create via Assets → Create → Zombera → Building → Roof Type Config.
    ///     Save under <c>Assets/02_Shared/ScriptableObjects/Buildings/RoofTypes/</c>.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Zombera/Building/Roof Type Config",
        fileName = "RoofTypeConfig",
        order = 101)]
    public sealed class RoofTypeConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Human-readable name shown in UI (e.g. 'Gable Roof', 'Flat Roof', 'Saltbox').")]
        public string DisplayName = "New Roof Type";

        [Tooltip("How the roof is assembled: gable (ridge+panels+gables) or flat (tile per cell).")]
        public RoofStyle Style = RoofStyle.Gable;

        // ── Prefab references ────────────────────────────────────────

        [Header("Prefabs (Gable style)")]
        [Tooltip("Ridge prefab — the peak cap running along the Z-axis. Null = no ridge.")]
        public GameObject RidgePrefab;

        [Tooltip("Panel prefab — one sloped face from wall to ridge. Two placed per assembly.")]
        public GameObject PanelPrefab;

        [Tooltip("Gable prefab — triangular end-wall piece at front/back. Two placed per assembly.")]
        public GameObject GablePrefab;

        [Header("Prefabs (Flat style)")]
        [Tooltip("Tile prefab for flat roofs. Laid once per footprint cell at the top-of-wall height.")]
        public GameObject TilePrefab;

        // ── Gable-style scale parameters ─────────────────────────────

        [Header("Gable — Geometry")]
        [Tooltip("Peak height multiplier relative to WallHeight (3m). 1.0 = 45° pitch on a square footprint.")]
        [Range(0.2f, 3f)]
        public float PeakHeightMultiplier = 1f;

        [Tooltip("Divisor for panel Y-scale. panelYScale = diag / this. 2.2 matches the kit panel mesh height.")]
        [Min(0.1f)]
        public float PanelDiagonalNorm = 2.2f;

        [Tooltip("Panel width multiplier relative to footprint half-width. 0.5 = one slope spans half the building.")]
        [Min(0.01f)]
        public float PanelWidthMultiplier = 0.5f;

        [Tooltip("Extra Z overhang for panels (metres added to building depth).")]
        public float PanelZOverhang = 0.1f;

        [Header("Gable — Gable Scaling")]
        [Tooltip("Snugness factor for gable width (0.98 = 2% narrower than footprint for a tight fit).")]
        [Range(0.5f, 1.5f)]
        public float GableXSnugness = 0.98f;

        [Tooltip("Base height of the gable mesh in metres. gableYScale = peakH / this.")]
        [Min(0.1f)]
        public float GableYBaseHeight = 1.5f;

        // ── Position offsets ─────────────────────────────────────────

        [Header("Gable — Ridge Offset")]
        [Tooltip("Vertical offset of the ridge above the theoretical peak (metres).")]
        public float RidgeYOffset = 0.04f;

        [Tooltip("Vertical scale of the ridge piece.")]
        [Min(0.1f)]
        public float RidgeYScale = 2f;

        [Tooltip("Extra Z overhang for the ridge (metres added to building depth).")]
        public float RidgeZOverhang = 0.11f;

        [Header("Gable — Panel Offset")]
        [Tooltip("Horizontal offset of panels from the half-width centre (metres).")]
        public float PanelXOffset = 0.07f;

        [Tooltip("Vertical offset of panels above the mid-peak height (metres).")]
        public float PanelYOffset = 0.04f;

        [Tooltip("Multiplier for panel X position relative to half-width. 0.5 = centred on each slope half.")]
        [Range(0.1f, 1f)]
        public float PanelXPositionMultiplier = 0.5f;

        [Tooltip("Multiplier for panel Y position relative to peak height. 0.5 = centred vertically.")]
        [Range(0.1f, 1f)]
        public float PanelYPositionMultiplier = 0.5f;

        [Header("Gable — Gable Offset")]
        [Tooltip("Multiplier for gable Y position relative to peak height. 0.5 = centred vertically.")]
        [Range(0.1f, 1f)]
        public float GableYPositionMultiplier = 0.5f;

        [Header("Flat — Tile Modifiers")]
        [Tooltip("Vertical offset added to each flat-roof tile above wall top (metres). Positive = raised cap / parapet lip.")]
        public float FlatRoofYOffset = 0f;

        [Tooltip("Rotation around the Y axis for each flat-roof tile (degrees).")]
        [Range(-90f, 90f)]
        public float FlatRoofYRotation = 0f;

        [Tooltip("Rotation around the Z axis for each flat-roof tile (degrees). Use for angled shed roofs.")]
        [Range(-90f, 90f)]
        public float FlatRoofZRotation = 0f;

        [Tooltip("Scale multiplier for the tile's thickness (X axis). 1.0 = default thickness.")]
        [Min(0.01f)]
        public float FlatRoofXScale = 1f;

        [Tooltip("Scale multiplier for the tile's length / depth (Z axis). 1.0 = exact footprint depth. 1.2 = 20% overhang.")]
        [Min(0.01f)]
        public float FlatRoofYScale = 1f;

        [Tooltip("Scale multiplier for the tile's width (X axis). 1.0 = exact footprint width. 1.2 = 20% overhang.")]
        [Min(0.01f)]
        public float FlatRoofZScale = 1f;

        [Tooltip("When true, remaps UVs so the texture tiles across the full footprint instead of stretching.")]
        public bool FlatRoofUvTiling = true;

        [Header("Flat — Parapet")]
        [Tooltip("Y scale for parapet walls. 0.33 = one-third wall height. 0 = no parapet.")]
        [Min(0f)]
        public float ParapetYScale = 0f;

        [Tooltip("Z scale for parapet walls (depth/thickness). 3 = triple wall depth.")]
        [Min(0.01f)]
        public float ParapetZScale = 1f;

        [Tooltip("Inward offset for parapet walls (metres). Pushes wall toward building interior.")]
        public float ParapetInset = 0f;

        [Header("Saltbox")]
        [Tooltip("Gable prefab for saltbox ends — must be a right-angle triangle (e.g. Gable_RightAngle_3m).")]
        public GameObject SaltboxGablePrefab;

        [Tooltip("How far the ridge is offset from center as a fraction of half-width. 0=centered (gable), 0.5=halfway, 1=at edge (shed-like).")]
        [Range(0f, 1f)]
        public float SaltboxRidgeOffset = 0.33f;

        [Tooltip("Which side the ridge is offset toward (+X or -X).")]
        public bool SaltboxRidgeTowardPositiveX = true;

        [Header("Shed")]
        [Tooltip("Which edge of the building is the low side.")]
        public ShedDirection ShedLowEdge = ShedDirection.South;

        [Tooltip("Y scale for the high-side wall stack. 0.75 = three-quarter wall height.")]
        [Range(0.1f, 1f)]
        public float ShedHighScale = 0.75f;

        [Tooltip("Y scale for the low-side wall stack. 0.25 = one-quarter wall height.")]
        [Range(0.1f, 1f)]
        public float ShedLowScale = 0.25f;

        [Tooltip("X scale for the shed panel mesh.")]
        [Min(0.01f)]
        public float ShedPanelXScale = 0.3333f;

        [Header("Gambrel")]
        [Tooltip("Gable prefab for gambrel ends — fills the two-slope profile.")]
        public GameObject GambrelGablePrefab;

        [Tooltip("Height of the slope change (break) as fraction of peak height. 0.45 = break at 45% of ridge height.")]
        [Range(0.1f, 0.8f)]
        public float GambrelBreakHeight = 0.45f;

        [Tooltip("Width at the break point as fraction of half-building-width. 0.40 = break at 40% of the way out from ridge.")]
        [Range(0.1f, 0.9f)]
        public float GambrelBreakWidth = 0.40f;

        [Tooltip("Width at the ridge as fraction of half-width. 0.10 = ridge is 20% of building width.")]
        [Range(0.01f, 0.3f)]
        public float GambrelRidgeWidth = 0.10f;

        [Header("Flat — Guttering")]
        [Tooltip("X offset for gutter segments on flat roofs (metres). Applied to both left and right sides.")]
        public float FlatRoofGutterXOffset = 0f;

        [Tooltip("Y (vertical) offset for gutter segments on flat roofs (metres). Added to wall-top height.")]
        public float FlatRoofGutterYOffset = 0f;

        [Tooltip("Z offset for gutter segments on flat roofs (metres).")]
        public float FlatRoofGutterZOffset = 0f;

        // ── Helpers ──────────────────────────────────────────────────

        /// <summary>True when all three kit-of-parts prefabs are assigned.</summary>
        public bool HasFullGableKit => RidgePrefab != null && PanelPrefab != null && GablePrefab != null;

        /// <summary>True when the flat-roof tile prefab is assigned.</summary>
        public bool HasFlatTile => TilePrefab != null;
    }
}
#endif
