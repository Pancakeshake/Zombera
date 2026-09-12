using UnityEngine;
using UnityEngine.InputSystem;

namespace Zombera.BuildingSystem
{
    public sealed partial class BuildPlacementController
    {
        private void HandleSkinSelectionInput()
        {
            if (!enableSkinSelection || Keyboard.current == null) return;

            var requestedSkinIndex = -1;

            if (Keyboard.current[skinOneKey].wasPressedThisFrame)
                requestedSkinIndex = 0;
            else if (Keyboard.current[skinTwoKey].wasPressedThisFrame)
                requestedSkinIndex = 1;
            else if (Keyboard.current[skinThreeKey].wasPressedThisFrame)
                requestedSkinIndex = 2;
            else if (Keyboard.current[skinFourKey].wasPressedThisFrame)
                requestedSkinIndex = 3;
            else if (Keyboard.current[skinFiveKey].wasPressedThisFrame) requestedSkinIndex = 4;

            if (requestedSkinIndex < 0) return;

            _selectedSkinIndex = requestedSkinIndex;

            if (allowSkinSelectOutsideBuildMode && !IsBuildModeActive) EnterBuildMode();
        }

        private void ApplySelectedSkinMaterial(GameObject placedRoot)
        {
            if (placedRoot == null) return;

            var selectedMaterial = GetSelectedSkinMaterial();
            if (selectedMaterial == null) return;

            var renderers = placedRoot.GetComponentsInChildren<Renderer>(true);
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var rend in renderers)
            {
                // Skip Window_Modular_* meshes — they keep their original glass/mesh materials.
                if (rend.name.IndexOf("Window_Modular", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || rend.name.IndexOf("Window_Moulding", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                var materials = rend.materials;

                if (materials == null || materials.Length == 0)
                {
                    rend.material = selectedMaterial;
                    continue;
                }

                for (var m = 0; m < materials.Length; m++) materials[m] = selectedMaterial;

                rend.materials = materials;
            }
        }

        private Material GetSelectedSkinMaterial()
        {
            if (wallSkinMaterials == null || wallSkinMaterials.Length == 0) return null;

            var clampedIndex = Mathf.Clamp(_selectedSkinIndex, 0, wallSkinMaterials.Length - 1);
            _selectedSkinIndex = clampedIndex;
            return wallSkinMaterials[clampedIndex];
        }
    }
}