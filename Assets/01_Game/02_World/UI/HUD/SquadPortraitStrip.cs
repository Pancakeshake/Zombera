#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;

#endregion

namespace Zombera.UI
{
    // ReSharper disable InvertIf
    /// <summary>
    ///     Manages the bottom-bar portrait strip. Discovers SquadPortraitSlot children, binds live
    ///     squad units to them, and ticks HP/stamina bars every 0.1 s.
    /// </summary>
    [AddComponentMenu("Zombera/UI/Squad Portrait Strip")]
    [DisallowMultipleComponent]
    public sealed partial class SquadPortraitStrip : MonoBehaviour
    {
        private static bool _runtimeHeadshotCaptureDisabledForSession;
        private static bool _runtimeHeadshotCaptureDisableLogged;

        // ── Serialized ────────────────────────────────────────────────────────

        [Header("Colors")] [SerializeField] private Color hpFull = new(0.22f, 0.65f, 0.30f, 1f);

        [SerializeField] private Color hpLow = new(0.72f, 0.22f, 0.12f, 1f);
        [SerializeField] private Color selectTint = new(0.20f, 0.55f, 0.38f, 0.50f);

        [Header("Portrait Framing")] [SerializeField] [Range(0.10f, 1f)]
        private float faceCropScale = 0.40f;

        [SerializeField] [Range(0f, 1f)] private float faceCropCenterX = 0.50f;
        [SerializeField] [Range(0f, 1f)] private float faceCropCenterY = 0.87f;
        [SerializeField] [Range(0f, 1f)] private float alphaDetectionThreshold = 0.02f;

        [Header("Portrait Headshot Capture")] [SerializeField]
        private bool useRuntimeHeadshotCapture = false;

        [Tooltip("When enabled, scans texture alpha with GetPixels32 to auto-detect subject bounds before cropping.")]
        [SerializeField]
        private bool useAlphaBoundsForFaceCrop = true;

        [Tooltip(
            "Allows fallback RenderTexture readback for portraits. Keep disabled to avoid Gfx.UploadTexture spikes in runtime HUD.")]
        [SerializeField]
        private bool allowRuntimeTextureReadbackFallback;

        [SerializeField] [Range(64, 512)] private int runtimeHeadshotResolution = 256;
        [SerializeField] [Min(0.20f)] private float runtimeHeadshotDistance = 1.1f;
        [SerializeField] private float runtimeHeadshotVerticalOffset = 0.03f;
        [SerializeField] private float runtimeHeadshotLookOffset = 0.05f;
        [SerializeField] [Range(15f, 70f)] private float runtimeHeadshotFieldOfView = 24f;
        [SerializeField] private bool runtimeHeadshotUseFillLight = true;
        [SerializeField] [Range(0f, 4f)] private float runtimeHeadshotFillLightIntensity = 1.2f;
        [SerializeField] private Color runtimeHeadshotFillLightColor = new(1f, 0.98f, 0.94f, 1f);

        [Header("Portrait Runtime Performance")] [SerializeField] [Min(0.25f)]
        private float missingPortraitRetryIntervalSeconds = 0.5f;

        [SerializeField] [Min(1)] private int maxRuntimeHeadshotCapturesPerRetryTick = 8;
        [SerializeField] [Min(4)] private int maxCachedHeadshots = 48;
        [SerializeField] private bool enablePortraitCaptureDiagnostics;
        [SerializeField] [Min(0f)] private float portraitCaptureDiagnosticsThresholdMs = 8f;

        [Header("Squad Paging Tabs")] [SerializeField]
        private bool enableSquadTabs = true;

        [SerializeField] private bool autoEnableTabsForBottomStrip = true;
        [SerializeField] [Min(1)] private int squadTabCount = 4;
        [SerializeField] [Min(1)] private int slotsPerSquadTab = 10;
        [SerializeField] [Min(80f)] private float portraitSlotWidth = 200f;
        [SerializeField] [Min(60f)] private float portraitSlotHeight = 140f;
        [SerializeField] [Min(60f)] private float squadTabWidth = 100f;
        [SerializeField] [Min(24f)] private float squadTabHeight = 38f;
        [SerializeField] private Button[] squadTabButtons = Array.Empty<Button>();
        [SerializeField] private Color squadTabActiveColor = new(0.20f, 0.55f, 0.38f, 1f);
        [SerializeField] private Color squadTabInactiveColor = new(0.11f, 0.13f, 0.17f, 1f);
        [SerializeField] private Color squadTabActiveLabelColor = new(0.95f, 0.97f, 0.98f, 1f);
        [SerializeField] private Color squadTabInactiveLabelColor = new(0.82f, 0.85f, 0.88f, 1f);
        private readonly List<int> _cacheRemovalBuffer = new(24);
        private readonly List<Sprite> _capturedPortraitSprites = new(24);
        private readonly List<Texture2D> _capturedPortraitTextures = new(24);
        private readonly Dictionary<int, Color32[]> _readablePixelCacheByTextureId = new(24);
        private readonly Dictionary<int, Sprite> _headshotCacheByUnitId = new(24);
        private readonly Dictionary<int, Texture2D> _headshotCacheTexturesByUnitId = new(24);
        private readonly HashSet<int> _portraitReadbackBlockedUnitIds = new();
        private readonly HashSet<int> _rosterUnitIds = new();
        private readonly Dictionary<Button, TextMeshProUGUI> _squadTabLabelByButton = new(8);
        private readonly List<Unit> _rosterUnits = new(24);
        private readonly List<Unit> _unitManagerQueryBuffer = new(24);
        private readonly LinkedList<int> _headshotCacheOrder = new();
        private readonly Dictionary<int, LinkedListNode<int>> _headshotCacheNodesByUnitId = new(24);
        private int _activeSquadTabIndex;
        private readonly HashSet<int> _squadTabIndicesWithSelection = new(8);
        private int _nextPortraitRetryStartIndex;
        private float _portraitRetryTicker;
        private int _selected = -1;
        private int _portraitSelectionAnchorRosterIndex = -1;
        private bool _preserveManualSquadLayout;

        // ── State ─────────────────────────────────────────────────────────────

        private SquadPortraitSlot[] _slots;
        private float _ticker;

        /// <summary>Current selected unit for this strip, or null if nothing is selected.</summary>
        public Unit SelectedUnit =>
            _slots != null && _selected >= 0 && _selected < _slots.Length
                ? _slots[_selected].BoundUnit
                : null;

        public int ActiveSquadTabIndex => _activeSquadTabIndex;

        // ── Unity ─────────────────────────────────────────────────────────────

        /// <summary>Fired when the player clicks a portrait. Unit may be null if slot is empty.</summary>
        public event Action<Unit> OnPortraitClicked;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Re-scan the scene for squad units and rebind portrait slots.</summary>
        public void RefreshBindings()
        {
            TouchSplitMembersForAnalysis();
            RefreshBindingsCore();
        }

        public void SetActiveSquadTab(int tabIndex)
        {
            TouchSplitMembersForAnalysis();
            SetActiveSquadTabCore(tabIndex);
        }

        /// <summary>Select the first active slot that has a bound unit.</summary>
        public bool SelectFirstBoundUnit()
        {
            TouchSplitMembersForAnalysis();
            return SelectFirstBoundUnitCore();
        }

        /// <summary>Select player unit if present; otherwise select the first bound unit.</summary>
        public bool SelectPlayerOrFirstBoundUnit()
        {
            TouchSplitMembersForAnalysis();
            return SelectPlayerOrFirstBoundUnitCore();
        }

        /// <summary>Select a specific bound unit if present in this strip.</summary>
        public bool TrySelectUnit(Unit unit)
        {
            TouchSplitMembersForAnalysis();
            return TrySelectUnitCore(unit);
        }

        public bool TrySelectUnit(Unit unit, bool allowTabSwitch)
        {
            TouchSplitMembersForAnalysis();
            return TrySelectUnitCore(unit, allowTabSwitch);
        }

        /// <summary>Select a visible bound slot by zero-based index within the current strip page.</summary>
        public bool TrySelectVisibleSlotByIndex(int slotIndex)
        {
            TouchSplitMembersForAnalysis();
            return TrySelectVisibleSlotByIndexCore(slotIndex);
        }


        private static void KeepMutableForSplitAnalysis<T>(ref T value)
        {
            // Intentionally empty. Passing by ref keeps split-host fields visible to analyzers.
        }


        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TouchSplitMembersForAnalysis()
        {
            KeepMutableForSplitAnalysis(ref _activeSquadTabIndex);
            KeepMutableForSplitAnalysis(ref _selected);
            KeepMutableForSplitAnalysis(ref _slots);
            var portraitClicked = OnPortraitClicked;
            _ = portraitClicked;
        }
    }
}