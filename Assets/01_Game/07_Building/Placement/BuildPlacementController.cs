#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zombera.Characters;
using Zombera.Systems;

#endregion

// ReSharper disable Unity.PerformanceCriticalCodeInvocation
// ReSharper disable Unity.PerformanceCriticalCodeNullComparison
// ReSharper disable MemberCanBePrivate.Global

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Step 9 placement flow: select wall, preview, left-click place, right-click cancel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class BuildPlacementController : MonoBehaviour
    {
        private static readonly List<RaycastResult> UiRaycastResults = new(16);

        [Header("Dependencies")] [SerializeField]
        private BuildGhostPreview ghostPreview;

        [SerializeField] private Transform placedWallsRoot;
        [SerializeField] private UnitStats placerStats;
        [SerializeField] private RuntimePlacedStructureFixer runtimePlacedStructureFixer;

        [Header("Wall Prefabs")] [SerializeField]
        private GameObject wallFullPrefab;

        [SerializeField] private GameObject wallWindowPrefab;
        [SerializeField] private GameObject wallDoorPrefab;
        [SerializeField] private GameObject wallDamagedPrefab;

        [Header("Material Variation (Step 10)")] [SerializeField]
        private Material[] wallSkinMaterials = Array.Empty<Material>();

        [SerializeField] private bool enableSkinSelection = true;
        [SerializeField] private bool allowSkinSelectOutsideBuildMode;
        [SerializeField] private Key skinOneKey = Key.Digit5;
        [SerializeField] private Key skinTwoKey = Key.Digit6;
        [SerializeField] private Key skinThreeKey = Key.Digit7;
        [SerializeField] private Key skinFourKey = Key.Digit8;
        [SerializeField] private Key skinFiveKey = Key.Digit9;

        [Header("Build Input")] [SerializeField]
        private bool enableBuildModeOnStart;

        [Header("Legacy Build Mode")]
        [Tooltip("Legacy wall ghost builder. Disabled by default so EasyBuild radial flow is the single build path.")]
        [SerializeField]
        private bool enableLegacyBuildPlacementMode;

        [SerializeField] private bool allowSelectOutsideBuildMode = true;
        [SerializeField] private Key toggleBuildModeKey = Key.B;
        [SerializeField] private Key cancelBuildModeKey = Key.Escape;
        [SerializeField] private Key wallFullKey = Key.Digit1;
        [SerializeField] private Key wallWindowKey = Key.Digit2;
        [SerializeField] private Key wallDoorKey = Key.Digit3;
        [SerializeField] private Key wallDamagedKey = Key.Digit4;

        [Header("Build Menu Integration")] [SerializeField]
        private bool rightClickOpensRadialMenuFirst = true;

        [SerializeField] [Range(0f, 0.5f)]
        private float radialMenuVerticalScreenOffsetPercent = 0.1f;

        [SerializeField]
        private bool radialMenuUsesUnscaledTime = true;

        [Header("Placement Cooldown")]
        [Tooltip("Minimum seconds between wall placements. Engineering skill reduces this.")]
        [SerializeField]
        [Min(0f)]
        private float basePlacementCooldownSeconds = 0.3f;

        private float _lastPlacementTime = float.MinValue;
        private int _selectedSkinIndex;
        private object _cachedRadialMenu;
        private Type _cachedRadialMenuType;
        private RectTransform _cachedRadialMenuRect;
        private Vector2 _cachedRadialMenuBaseAnchoredPosition;
#pragma warning disable S2933 // Mutated in BuildPlacementController.RadialMenu.cs (partial class)
        private int _cachedRadialMenuScreenHeight = -1;
#pragma warning restore S2933
        private bool _cachedRadialMenuUnscaledConfigured;
        private bool _wasBuildModeActive;

        private WallSelection _selectedWall = WallSelection.Full;

        public bool IsBuildModeActive { get; private set; }

        private float EffectivePlacementCooldown
        {
            get
            {
                var speedMult = placerStats != null ? placerStats.GetEngineeringBuildSpeedMultiplier() : 1f;
                return Mathf.Max(0f, basePlacementCooldownSeconds / Mathf.Max(0.01f, speedMult));
            }
        }

        private bool IsPlacementOnCooldown => Time.time - _lastPlacementTime < EffectivePlacementCooldown;

        private void Awake()
        {
            if (ghostPreview == null) ghostPreview = GetComponent<BuildGhostPreview>();

            if (ghostPreview == null) ghostPreview = gameObject.AddComponent<BuildGhostPreview>();

            if (runtimePlacedStructureFixer == null)
                runtimePlacedStructureFixer = GetComponent<RuntimePlacedStructureFixer>();

            if (runtimePlacedStructureFixer == null)
                runtimePlacedStructureFixer = gameObject.AddComponent<RuntimePlacedStructureFixer>();

            if (placedWallsRoot == null)
            {
                var existingRoot = GameObject.Find("PlacedWalls");
                if (existingRoot != null) placedWallsRoot = existingRoot.transform;
            }

            if (placerStats == null) placerStats = GetComponentInParent<UnitStats>();

            if (!enableLegacyBuildPlacementMode)
            {
                IsBuildModeActive = false;
                ghostPreview?.SetPreviewActive(false);
                enabled = false;
                return;
            }

            if (enableBuildModeOnStart)
                EnterBuildMode();
            else
                ghostPreview?.SetPreviewActive(false);
        }

        private void Update()
        {
            var currentBuildModeActive = IsBuildModeActive;
            if (currentBuildModeActive != _wasBuildModeActive)
            {
                _wasBuildModeActive = currentBuildModeActive;
                Zombera.Core.CoreEventBus.PublishGlobal(new BuildModeChangedEvent(currentBuildModeActive, this));
            }

            HandleBuildModeToggleInput();
            HandleSelectionInput();
            HandleSkinSelectionInput();

            if (!IsBuildModeActive) return;

            HandlePlacementInput();
        }

        public void EnterBuildMode()
        {
            IsBuildModeActive = true;
            ApplySelectedWallPreview();
            ghostPreview?.SetPreviewActive(true);
        }

        public void ExitBuildMode()
        {
            IsBuildModeActive = false;
            ghostPreview?.SetPreviewActive(false);
        }

        public void ToggleBuildMode()
        {
            if (IsBuildModeActive)
                ExitBuildMode();
            else
                EnterBuildMode();
        }

        public void SetWorldCamera(Camera targetCamera)
        {
            ghostPreview?.SetWorldCamera(targetCamera);
        }

        private void HandleBuildModeToggleInput()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current[toggleBuildModeKey].wasPressedThisFrame)
            {
                ToggleBuildMode();
                return;
            }

            if (IsBuildModeActive && Keyboard.current[cancelBuildModeKey].wasPressedThisFrame) ExitBuildMode();
        }

        private void HandleSelectionInput()
        {
            if (Keyboard.current == null) return;

            var changed = false;

            if (Keyboard.current[wallFullKey].wasPressedThisFrame)
            {
                _selectedWall = WallSelection.Full;
                changed = true;
            }
            else if (Keyboard.current[wallWindowKey].wasPressedThisFrame)
            {
                _selectedWall = WallSelection.Window;
                changed = true;
            }
            else if (Keyboard.current[wallDoorKey].wasPressedThisFrame)
            {
                _selectedWall = WallSelection.Door;
                changed = true;
            }
            else if (Keyboard.current[wallDamagedKey].wasPressedThisFrame)
            {
                _selectedWall = WallSelection.Damaged;
                changed = true;
            }

            if (!changed) return;

            if (allowSelectOutsideBuildMode && !IsBuildModeActive)
            {
                EnterBuildMode();
                return;
            }

            ApplySelectedWallPreview();
        }

        private void HandlePlacementInput()
        {
            if (Mouse.current == null) return;

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                if (rightClickOpensRadialMenuFirst && TryOpenBuildingRadialMenu())
                    return;

                ExitBuildMode();
                return;
            }

            if (IsPointerOverUi()) return;

            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            _ = TryPlaceSelectedWall();
        }

        private bool TryPlaceSelectedWall()
        {
            if (ghostPreview == null) return false;

            if (IsPlacementOnCooldown) return false;

            var selectedPrefab = GetSelectedWallPrefab();
            if (selectedPrefab == null) return false;

            if (!ghostPreview.TryGetPlacementPose(out var position, out var rotation, out var isValid)) return false;

            if (!isValid) return false;

            var placed = Instantiate(selectedPrefab, position, rotation, placedWallsRoot);
            placed.name = selectedPrefab.name;
            ApplySelectedSkinMaterial(placed);
            runtimePlacedStructureFixer?.ProcessPlacedStructure(placed);
            placerStats?.RecordBuildPiecePlaced();
            _lastPlacementTime = Time.time;
            return true;
        }

        private void ApplySelectedWallPreview()
        {
            if (ghostPreview == null) return;

            var selectedPrefab = GetSelectedWallPrefab();
            if (selectedPrefab == null)
            {
                ghostPreview.SetPreviewActive(false);
                return;
            }

            ghostPreview.SetPreviewPrefab(selectedPrefab);

            if (IsBuildModeActive) ghostPreview.SetPreviewActive(true);
        }

        private GameObject GetSelectedWallPrefab()
        {
            return _selectedWall switch
            {
                WallSelection.Window => wallWindowPrefab,
                WallSelection.Door => wallDoorPrefab,
                WallSelection.Damaged => wallDamagedPrefab,
                _ => wallFullPrefab
            };
        }

        private static bool IsPointerOverUi()
        {
            var uiEventSystem = EventSystem.current;
            if (uiEventSystem == null) return false;

            if (CursorService.TryGetGameplayPointerScreenPosition(out var mouseScreenPosition)
                && IsScreenPositionOverUi(uiEventSystem, mouseScreenPosition))
                return true;

            if (Touchscreen.current == null) return false;

            var touches = Touchscreen.current.touches;
            return touches.Any(touch =>
                touch.press.isPressed &&
                IsScreenPositionOverUi(uiEventSystem, touch.position.ReadValue()));
        }

        private static bool IsScreenPositionOverUi(EventSystem eventSystem, Vector2 screenPosition)
        {
            var pointerData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };

            UiRaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, UiRaycastResults);
            return UiRaycastResults.Count > 0;
        }

        private enum WallSelection
        {
            Full,
            Window,
            Door,
            Damaged
        }
    }
}
