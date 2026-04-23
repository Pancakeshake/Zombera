using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zombera.Characters;

namespace Zombera.BuildingSystem
{
    /// <summary>
    /// Step 9 placement flow: select wall, preview, left-click place, right-click cancel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildPlacementController : MonoBehaviour
    {
        private enum WallSelection
        {
            Full,
            Window,
            Door,
            Damaged
        }

        private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>(16);

        [Header("Dependencies")]
        [SerializeField] private BuildGhostPreview ghostPreview;
        [SerializeField] private Transform placedWallsRoot;
        [SerializeField] private UnitStats placerStats;

        [Header("Wall Prefabs")]
        [SerializeField] private GameObject wallFullPrefab;
        [SerializeField] private GameObject wallWindowPrefab;
        [SerializeField] private GameObject wallDoorPrefab;
        [SerializeField] private GameObject wallDamagedPrefab;

        [Header("Material Variation (Step 10)")]
        [SerializeField] private Material[] wallSkinMaterials = new Material[0];
        [SerializeField] private bool enableSkinSelection = true;
        [SerializeField] private bool allowSkinSelectOutsideBuildMode;
        [SerializeField] private Key skinOneKey = Key.Digit5;
        [SerializeField] private Key skinTwoKey = Key.Digit6;
        [SerializeField] private Key skinThreeKey = Key.Digit7;
        [SerializeField] private Key skinFourKey = Key.Digit8;
        [SerializeField] private Key skinFiveKey = Key.Digit9;

        [Header("Build Input")]
        [SerializeField] private bool enableBuildModeOnStart;
        [SerializeField] private bool allowSelectOutsideBuildMode = true;
        [SerializeField] private Key toggleBuildModeKey = Key.B;
        [SerializeField] private Key cancelBuildModeKey = Key.Escape;
        [SerializeField] private Key wallFullKey = Key.Digit1;
        [SerializeField] private Key wallWindowKey = Key.Digit2;
        [SerializeField] private Key wallDoorKey = Key.Digit3;
        [SerializeField] private Key wallDamagedKey = Key.Digit4;

        [Header("Placement Cooldown")]
        [Tooltip("Minimum seconds between wall placements. Engineering skill reduces this.")]
        [SerializeField, Min(0f)] private float basePlacementCooldownSeconds = 0.3f;

        private float lastPlacementTime = float.MinValue;

        private WallSelection selectedWall = WallSelection.Full;
        private int selectedSkinIndex;
        private bool isBuildModeActive;

        public bool IsBuildModeActive => isBuildModeActive;

        private void Awake()
        {
            if (ghostPreview == null)
            {
                ghostPreview = GetComponent<BuildGhostPreview>();
            }

            if (ghostPreview == null)
            {
                ghostPreview = gameObject.AddComponent<BuildGhostPreview>();
            }

            if (placedWallsRoot == null)
            {
                GameObject existingRoot = GameObject.Find("PlacedWalls");
                if (existingRoot != null)
                {
                    placedWallsRoot = existingRoot.transform;
                }
            }

            if (placerStats == null)
            {
                placerStats = GetComponentInParent<UnitStats>();
            }

            if (enableBuildModeOnStart)
            {
                EnterBuildMode();
            }
            else
            {
                ghostPreview?.SetPreviewActive(false);
            }
        }

        private void Update()
        {
            HandleBuildModeToggleInput();
            HandleSelectionInput();
            HandleSkinSelectionInput();

            if (!isBuildModeActive)
            {
                return;
            }

            HandlePlacementInput();
        }

        public void EnterBuildMode()
        {
            isBuildModeActive = true;
            ApplySelectedWallPreview();
            ghostPreview?.SetPreviewActive(true);
        }

        public void ExitBuildMode()
        {
            isBuildModeActive = false;
            ghostPreview?.SetPreviewActive(false);
        }

        public void ToggleBuildMode()
        {
            if (isBuildModeActive)
            {
                ExitBuildMode();
            }
            else
            {
                EnterBuildMode();
            }
        }

        public void SetWorldCamera(Camera camera)
        {
            ghostPreview?.SetWorldCamera(camera);
        }

        private void HandleBuildModeToggleInput()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current[toggleBuildModeKey].wasPressedThisFrame)
            {
                ToggleBuildMode();
                return;
            }

            if (isBuildModeActive && Keyboard.current[cancelBuildModeKey].wasPressedThisFrame)
            {
                ExitBuildMode();
            }
        }

        private void HandleSelectionInput()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            bool changed = false;

            if (Keyboard.current[wallFullKey].wasPressedThisFrame)
            {
                selectedWall = WallSelection.Full;
                changed = true;
            }
            else if (Keyboard.current[wallWindowKey].wasPressedThisFrame)
            {
                selectedWall = WallSelection.Window;
                changed = true;
            }
            else if (Keyboard.current[wallDoorKey].wasPressedThisFrame)
            {
                selectedWall = WallSelection.Door;
                changed = true;
            }
            else if (Keyboard.current[wallDamagedKey].wasPressedThisFrame)
            {
                selectedWall = WallSelection.Damaged;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            if (allowSelectOutsideBuildMode && !isBuildModeActive)
            {
                EnterBuildMode();
                return;
            }

            ApplySelectedWallPreview();
        }

        private void HandlePlacementInput()
        {
            if (Mouse.current == null)
            {
                return;
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                ExitBuildMode();
                return;
            }

            if (IsPointerOverUi())
            {
                return;
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            TryPlaceSelectedWall();
        }

        private bool TryPlaceSelectedWall()
        {
            if (ghostPreview == null)
            {
                return false;
            }

            if (IsPlacementOnCooldown)
            {
                return false;
            }

            GameObject selectedPrefab = GetSelectedWallPrefab();
            if (selectedPrefab == null)
            {
                return false;
            }

            if (!ghostPreview.TryGetPlacementPose(out Vector3 position, out Quaternion rotation, out bool isValid))
            {
                return false;
            }

            if (!isValid)
            {
                return false;
            }

            GameObject placed = Instantiate(selectedPrefab, position, rotation, placedWallsRoot);
            placed.name = selectedPrefab.name;
            ApplySelectedSkinMaterial(placed);
            placerStats?.RecordBuildPiecePlaced();
            lastPlacementTime = Time.time;
            return true;
        }

        private float EffectivePlacementCooldown
        {
            get
            {
                float speedMult = placerStats != null ? placerStats.GetEngineeringBuildSpeedMultiplier() : 1f;
                return Mathf.Max(0f, basePlacementCooldownSeconds / Mathf.Max(0.01f, speedMult));
            }
        }

        private bool IsPlacementOnCooldown => Time.time - lastPlacementTime < EffectivePlacementCooldown;

        private void ApplySelectedWallPreview()
        {
            if (ghostPreview == null)
            {
                return;
            }

            GameObject selectedPrefab = GetSelectedWallPrefab();
            if (selectedPrefab == null)
            {
                ghostPreview.SetPreviewActive(false);
                return;
            }

            ghostPreview.SetPreviewPrefab(selectedPrefab);

            if (isBuildModeActive)
            {
                ghostPreview.SetPreviewActive(true);
            }
        }

        private GameObject GetSelectedWallPrefab()
        {
            switch (selectedWall)
            {
                case WallSelection.Window:
                    return wallWindowPrefab;
                case WallSelection.Door:
                    return wallDoorPrefab;
                case WallSelection.Damaged:
                    return wallDamagedPrefab;
                default:
                    return wallFullPrefab;
            }
        }

        private void HandleSkinSelectionInput()
        {
            if (!enableSkinSelection || Keyboard.current == null)
            {
                return;
            }

            int requestedSkinIndex = -1;

            if (Keyboard.current[skinOneKey].wasPressedThisFrame)
            {
                requestedSkinIndex = 0;
            }
            else if (Keyboard.current[skinTwoKey].wasPressedThisFrame)
            {
                requestedSkinIndex = 1;
            }
            else if (Keyboard.current[skinThreeKey].wasPressedThisFrame)
            {
                requestedSkinIndex = 2;
            }
            else if (Keyboard.current[skinFourKey].wasPressedThisFrame)
            {
                requestedSkinIndex = 3;
            }
            else if (Keyboard.current[skinFiveKey].wasPressedThisFrame)
            {
                requestedSkinIndex = 4;
            }

            if (requestedSkinIndex < 0)
            {
                return;
            }

            selectedSkinIndex = requestedSkinIndex;

            if (allowSkinSelectOutsideBuildMode && !isBuildModeActive)
            {
                EnterBuildMode();
            }
        }

        private void ApplySelectedSkinMaterial(GameObject placedRoot)
        {
            if (placedRoot == null)
            {
                return;
            }

            Material selectedMaterial = GetSelectedSkinMaterial();
            if (selectedMaterial == null)
            {
                return;
            }

            Renderer[] renderers = placedRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                Material[] materials = renderer.materials;

                if (materials == null || materials.Length == 0)
                {
                    renderer.material = selectedMaterial;
                    continue;
                }

                for (int m = 0; m < materials.Length; m++)
                {
                    materials[m] = selectedMaterial;
                }

                renderer.materials = materials;
            }
        }

        private Material GetSelectedSkinMaterial()
        {
            if (wallSkinMaterials == null || wallSkinMaterials.Length == 0)
            {
                return null;
            }

            int clampedIndex = Mathf.Clamp(selectedSkinIndex, 0, wallSkinMaterials.Length - 1);
            selectedSkinIndex = clampedIndex;
            return wallSkinMaterials[clampedIndex];
        }

        private static bool IsPointerOverUi()
        {
            EventSystem uiEventSystem = EventSystem.current;
            if (uiEventSystem == null)
            {
                return false;
            }

            if (Mouse.current != null && IsScreenPositionOverUi(uiEventSystem, Mouse.current.position.ReadValue()))
            {
                return true;
            }

            if (Touchscreen.current == null)
            {
                return false;
            }

            for (int i = 0; i < Touchscreen.current.touches.Count; i++)
            {
                var touch = Touchscreen.current.touches[i];
                if (!touch.press.isPressed)
                {
                    continue;
                }

                if (IsScreenPositionOverUi(uiEventSystem, touch.position.ReadValue()))
                {
                    return true;
                }
            }

            return false;
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
    }
}
