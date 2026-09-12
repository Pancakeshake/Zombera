using UnityEngine;

namespace Zombera.BuildingSystem
{
    public sealed partial class BuildPlacementController
    {
        public bool TryActivateHudItem(int hudItemIndex)
        {
            if (hudItemIndex < 0) return false;

            // 0..3 = wall variants
            if (hudItemIndex <= 3)
            {
                _selectedWall = hudItemIndex switch
                {
                    1 => WallSelection.Window,
                    2 => WallSelection.Door,
                    3 => WallSelection.Damaged,
                    _ => WallSelection.Full
                };

                EnterBuildMode();
                ApplySelectedWallPreview();
                return true;
            }

            // 4..8 = skins 1..5
            if (hudItemIndex <= 8)
            {
                _selectedSkinIndex = Mathf.Clamp(hudItemIndex - 4, 0, 4);

                if (allowSkinSelectOutsideBuildMode && !IsBuildModeActive) EnterBuildMode();

                ApplySelectedWallPreview();
                return true;
            }

            // 9 = cancel
            if (hudItemIndex == 9)
            {
                ExitBuildMode();
                return true;
            }

            return false;
        }

        public Texture2D GetHudItemTexture(int hudItemIndex)
        {
            if (hudItemIndex < 0) return null;

            if (hudItemIndex <= 3)
            {
                var prefab = hudItemIndex switch
                {
                    1 => wallWindowPrefab,
                    2 => wallDoorPrefab,
                    3 => wallDamagedPrefab,
                    _ => wallFullPrefab
                };

                return ResolveMainTextureFromPrefab(prefab);
            }

            if (hudItemIndex <= 8)
            {
                var skinIndex = Mathf.Clamp(hudItemIndex - 4, 0, 4);
                if (wallSkinMaterials == null || wallSkinMaterials.Length == 0) return null;

                var clampedSkin = Mathf.Clamp(skinIndex, 0, wallSkinMaterials.Length - 1);
                return ResolveMainTextureFromMaterial(wallSkinMaterials[clampedSkin]);
            }

            return null;
        }

        private static Texture2D ResolveMainTextureFromPrefab(GameObject prefab)
        {
            if (prefab == null) return null;

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;

                var materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0) continue;

                for (var m = 0; m < materials.Length; m++)
                {
                    var tex = ResolveMainTextureFromMaterial(materials[m]);
                    if (tex != null) return tex;
                }
            }

            return null;
        }

        private static Texture2D ResolveMainTextureFromMaterial(Material material)
        {
            if (material == null) return null;

            if (material.mainTexture is Texture2D mainTexture) return mainTexture;

            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") is Texture2D baseMap)
                return baseMap;

            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") is Texture2D legacyMain)
                return legacyMain;

            return null;
        }

        public int SelectedWallSlotIndex => (int)_selectedWall;

        public int SelectedSkinSlotIndex => Mathf.Clamp(_selectedSkinIndex, 0, 4);

        public bool IsSkinSelectionEnabled => enableSkinSelection;
    }
}
