using UnityEngine;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeHudVisualHelper
    {
        internal static Sprite ResolveSpriteFromRadialMenu(object radialMenu, int hudItemIndex)
        {
            var activeCategory = EasyBuildBridgeSelectionHelper.ResolveActiveOrFirstCategory(radialMenu, true);
            var fromActive = ResolveSpriteFromCategorySlot(activeCategory, hudItemIndex);
            if (fromActive != null) return fromActive;

            var categories = EasyBuildBridgeReflectionHelper.GetMemberValue(radialMenu, "Categories") as System.Collections.IEnumerable;
            if (categories == null) return null;

            foreach (var category in categories)
            {
                if (category == null || ReferenceEquals(category, activeCategory)) continue;

                var fromCat = ResolveSpriteFromCategorySlot(category, hudItemIndex);
                if (fromCat != null) return fromCat;
            }

            return null;
        }

        internal static bool TryResolveRadialSlotVisualData(object radialMenu, int hudIndex, out string label, out Texture2D texture)
        {
            label = null;
            texture = null;

            if (hudIndex < 0 || radialMenu == null) return false;

            var activeCategory = EasyBuildBridgeSelectionHelper.ResolveActiveOrFirstCategory(radialMenu, true);

            if (activeCategory != null && TryResolveCategorySlotVisualData(activeCategory, hudIndex, out label, out texture))
                return true;

            var categories = EasyBuildBridgeReflectionHelper.GetMemberValue(radialMenu, "Categories") as System.Collections.IEnumerable;
            if (categories == null) return false;

            foreach (var category in categories)
            {
                if (category == null) continue;
                if (TryResolveCategorySlotVisualData(category, hudIndex, out label, out texture))
                    return true;
            }

            return false;
        }

        private static Sprite ResolveSpriteFromCategorySlot(object category, int slotIndex)
        {
            if (category == null || slotIndex < 0) return null;

            if (!EasyBuildBridgeSelectionHelper.TryGetSlotObjectAtIndex(category, slotIndex, out var slot) || slot == null)
                return null;

            var action = EasyBuildBridgeReflectionHelper.GetMemberValue(slot, "Action");

            var sprite = ResolveSpriteMember(slot,
                             "IconSprite",
                             "Icon",
                             "Sprite",
                             "ThumbnailSprite",
                             "PreviewSprite")
                         ?? ResolveSpriteMember(action,
                             "IconSprite",
                             "Icon",
                             "Sprite",
                             "ThumbnailSprite",
                             "PreviewSprite");

            return sprite;
        }

        private static Sprite ResolveSpriteMember(object instance, params string[] memberNames)
        {
            if (instance == null || memberNames == null || memberNames.Length == 0) return null;

            for (var i = 0; i < memberNames.Length; i++)
            {
                var value = EasyBuildBridgeReflectionHelper.GetMemberValue(instance, memberNames[i]);
                if (value is Sprite sprite) return sprite;

                if (value is UnityEngine.UI.Image uiImage && uiImage.sprite != null) return uiImage.sprite;

                if (value is UnityEngine.UI.RawImage rawImage && rawImage.texture is Texture2D rawTexture)
                {
                    return Sprite.Create(rawTexture,
                        new Rect(0f, 0f, rawTexture.width, rawTexture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            return null;
        }

        private static bool TryResolveCategorySlotVisualData(object category, int slotIndex, out string label,
            out Texture2D texture)
        {
            label = null;
            texture = null;

            if (category == null || slotIndex < 0) return false;

            if (!EasyBuildBridgeSelectionHelper.TryGetSlotObjectAtIndex(category, slotIndex, out var slot) || slot == null)
                return false;

            var action = EasyBuildBridgeReflectionHelper.GetMemberValue(slot, "Action");
            label = ResolveSlotLabel(slot, action);
            texture = ResolveSlotTexture(slot, action);
            // Slot exists at this index even when UI-facing label/icon data is empty.
            return true;
        }

        private static string ResolveSlotLabel(object slot, object action)
        {
            var label = ResolveStringMember(slot,
                "Label",
                "Title",
                "Name",
                "DisplayName",
                "Text",
                "Header",
                "Description");
            if (!string.IsNullOrWhiteSpace(label)) return label;

            label = ResolveStringMember(action,
                "Label",
                "Title",
                "Name",
                "DisplayName",
                "Text",
                "PartReference",
                "PrefabId",
                "ID");
            return label;
        }

        private static Texture2D ResolveSlotTexture(object slot, object action)
        {
            var texture = ResolveTextureMember(slot,
                "Icon",
                "Sprite",
                "IconSprite",
                "Thumbnail",
                "Preview",
                "Image",
                "RawImage",
                "MainTexture");
            if (texture != null) return texture;

            texture = ResolveTextureMember(action,
                "Icon",
                "Sprite",
                "IconSprite",
                "Thumbnail",
                "Preview",
                "Image",
                "RawImage",
                "MainTexture");
            return texture;
        }

        private static string ResolveStringMember(object instance, params string[] memberNames)
        {
            if (instance == null || memberNames == null || memberNames.Length == 0) return null;

            for (var i = 0; i < memberNames.Length; i++)
            {
                var value = EasyBuildBridgeReflectionHelper.GetMemberValue(instance, memberNames[i]);
                if (value is string str && !string.IsNullOrWhiteSpace(str))
                    return str;

                var nestedText = EasyBuildBridgeReflectionHelper.GetMemberValue(value, "text") as string
                                 ?? EasyBuildBridgeReflectionHelper.GetMemberValue(value, "Text") as string;
                if (!string.IsNullOrWhiteSpace(nestedText))
                    return nestedText;
            }

            return null;
        }

        private static Texture2D ResolveTextureMember(object instance, params string[] memberNames)
        {
            if (instance == null || memberNames == null || memberNames.Length == 0) return null;

            for (var i = 0; i < memberNames.Length; i++)
            {
                var value = EasyBuildBridgeReflectionHelper.GetMemberValue(instance, memberNames[i]);
                var texture = ResolveTextureFromUnknown(value);
                if (texture != null) return texture;
            }

            return null;
        }

        private static Texture2D ResolveTextureFromUnknown(object value)
        {
            if (value == null) return null;

            if (value is Texture2D texture2D) return texture2D;
            if (value is Sprite sprite) return sprite.texture;
            if (value is Material material) return EasyBuildBridgePartDataHelper.ResolveMainTextureFromMaterial(material);
            if (value is GameObject gameObject)
                return EasyBuildBridgePartDataHelper.ResolveMainTextureFromPrefab(gameObject);
            if (value is Component component)
            {
                var compTexture = ResolveTextureFromComponent(component);
                if (compTexture != null) return compTexture;
            }

            return ResolveTextureFromMemberContainer(value);
        }

        private static Texture2D ResolveTextureFromComponent(Component component)
        {
            if (component == null) return null;

            var compTexture = EasyBuildBridgePartDataHelper.ResolveMainTextureFromPrefab(component.gameObject);
            if (compTexture != null) return compTexture;

            return ResolveTextureFromMemberContainer(component);
        }

        private static Texture2D ResolveTextureFromMemberContainer(object instance)
        {
            if (instance == null) return null;

            var sprite = EasyBuildBridgeReflectionHelper.GetMemberValue(instance, "Sprite") as Sprite
                         ?? EasyBuildBridgeReflectionHelper.GetMemberValue(instance, "sprite") as Sprite;
            if (sprite != null) return sprite.texture;

            var texture = EasyBuildBridgeReflectionHelper.GetMemberValue(instance, "Texture") as Texture2D
                          ?? EasyBuildBridgeReflectionHelper.GetMemberValue(instance, "texture") as Texture2D
                          ?? EasyBuildBridgeReflectionHelper.GetMemberValue(instance, "MainTexture") as Texture2D;
            if (texture != null) return texture;

            var material = EasyBuildBridgeReflectionHelper.GetMemberValue(instance, "Material") as Material
                           ?? EasyBuildBridgeReflectionHelper.GetMemberValue(instance, "material") as Material;
            if (material != null) return EasyBuildBridgePartDataHelper.ResolveMainTextureFromMaterial(material);

            return null;
        }
    }
}
