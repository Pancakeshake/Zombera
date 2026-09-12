#region

using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zombera.UI.Menus;
using Zombera.UI.Menus.CharacterCreation;

#endregion

namespace Zombera.Editor
{
    /// <summary>
    ///     Rebuilds Assets/Prefabs/UI/Menus/MainMenu.prefab into a deterministic,
    ///     self-contained main menu with a full-screen background image, four top-level buttons,
    ///     a lightweight settings panel, and a nested CharacterCreatorPanel prefab.
    /// </summary>
    public static class MainMenuQuickDevBuildTool
    {
        private const string OutputPrefabPath = "Assets/Prefabs/UI/Menus/MainMenu.prefab";
        private const string CharacterCreatorPrefabPath = "Assets/Prefabs/UI/Menus/CharacterCreatorPanel.prefab";
        private const string TitleTexturePath = "Assets/Art/Title_LoadingImages/ZomberaTitle.png";
        private const string RoundedButtonSpritePath = "Assets/Art/UI/Generated/MainMenuButtonRounded.png";
        private const int RoundedButtonSpriteSize = 128;
        private const int RoundedButtonCornerRadius = 24;

        private static readonly Color ScreenTint = new(0.03f, 0.04f, 0.05f, 0.42f);
        private static readonly Color AreaTint = new(0.08f, 0.09f, 0.11f, 0.96f);
        private static readonly Color AreaBorderTint = new(0.23f, 0.25f, 0.29f, 0.65f);
        private static readonly Color ButtonTint = new(0.05f, 0.06f, 0.08f, 0.92f);
        private static readonly Color ButtonHighlightTint = new(0.48f, 0.22f, 0.08f, 0.98f);
        private static readonly Color ButtonPressedTint = new(0.67f, 0.31f, 0.11f, 1f);
        private static readonly Color ButtonDisabledTint = new(0.1f, 0.1f, 0.1f, 0.45f);
        private static readonly Color TextTint = new(0.95f, 0.92f, 0.84f, 1f);
        private static readonly Color SubtleTextTint = new(0.78f, 0.76f, 0.7f, 1f);
        private static readonly Color ModalBackdropTint = new(0f, 0f, 0f, 0.72f);
        private static readonly Color ModalCardTint = new(0.08f, 0.09f, 0.11f, 0.98f);

        [MenuItem("Tools/Scenes/Build Main Menu", priority = -500)]
        private static void BuildMainMenuFromQuickDevMenu()
        {
            RebuildMainMenuPrefab();
        }

        private static void RebuildMainMenuPrefab()
        {
            if (!EnsureRequiredAssets(out var characterCreatorPrefab, out var titleTexture)) return;

            var buttonSprite = EnsureRoundedButtonSprite();
            if (buttonSprite == null)
            {
                Debug.LogError("[MainMenuQuickDevBuildTool] Failed to create rounded menu button sprite.");
                return;
            }

            GameObject prefabRoot = null;

            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(OutputPrefabPath);
                if (prefabRoot == null)
                {
                    Debug.LogError("[MainMenuQuickDevBuildTool] Failed to load MainMenu prefab contents.");
                    return;
                }

                Undo.RegisterFullObjectHierarchyUndo(prefabRoot, "Build Main Menu");

                prefabRoot.name = "MainMenuRoot";
                prefabRoot.layer = 0;

                ResetRootTransform(prefabRoot.transform);

                var controller = GetOrAddComponent<MainMenuController>(prefabRoot);
                var removedChildren = DestroyChildrenImmediate(prefabRoot.transform);

                Debug.Log("[MainMenuQuickDevBuildTool] Removed " + removedChildren +
                          " existing MainMenu child object(s); rebuilding deterministic hierarchy.");

                var build = BuildHierarchy(prefabRoot, characterCreatorPrefab, titleTexture, buttonSprite);
                BindController(controller, prefabRoot, build);

                EditorUtility.SetDirty(prefabRoot);
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(build.SettingsPanel);

                if (build.CharacterCreatorPanel != null) EditorUtility.SetDirty(build.CharacterCreatorPanel.gameObject);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, OutputPrefabPath, out var saveSucceeded);
                if (!saveSucceeded)
                {
                    Debug.LogError("[MainMenuQuickDevBuildTool] Failed to save rebuilt MainMenu prefab.", prefabRoot);
                    return;
                }
            }
            finally
            {
                if (prefabRoot != null) PrefabUtility.UnloadPrefabContents(prefabRoot);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log("[MainMenuQuickDevBuildTool] Updated: " + OutputPrefabPath);
        }

        private static bool EnsureRequiredAssets(out GameObject characterCreatorPrefab, out Texture titleTexture)
        {
            characterCreatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterCreatorPrefabPath);
            titleTexture = AssetDatabase.LoadAssetAtPath<Texture>(TitleTexturePath);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath) == null)
            {
                Debug.LogError("[MainMenuQuickDevBuildTool] Target prefab missing: " + OutputPrefabPath);
                return false;
            }

            if (characterCreatorPrefab == null)
            {
                Debug.LogError("[MainMenuQuickDevBuildTool] Character Creator prefab missing: " +
                               CharacterCreatorPrefabPath);
                return false;
            }

            if (titleTexture == null)
            {
                Debug.LogError("[MainMenuQuickDevBuildTool] Title texture missing: " + TitleTexturePath);
                return false;
            }

            return true;
        }

        private static BuildContext BuildHierarchy(GameObject prefabRoot, GameObject characterCreatorPrefab,
            Texture titleTexture, Sprite buttonSprite)
        {
            BuildContext build = new();

            var canvasRect = CreateRect("MainMenuCanvas", prefabRoot.transform);
            Stretch(canvasRect);
            SetLayerRecursively(canvasRect.gameObject, 5);

            var canvas = GetOrAddComponent<Canvas>(canvasRect.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = GetOrAddComponent<CanvasScaler>(canvasRect.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GetOrAddComponent<GraphicRaycaster>(canvasRect.gameObject);

            var mainTitleImage = CreateRawImage("MainTitleImage", canvasRect, titleTexture, Color.white);
            Stretch(mainTitleImage.rectTransform);
            var aspectRatio = GetOrAddComponent<AspectRatioFitter>(mainTitleImage.gameObject);
            aspectRatio.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspectRatio.aspectRatio = titleTexture.height > 0
                ? (float)titleTexture.width / titleTexture.height
                : 1f;

            var screenTint = CreateImage("ScreenTint", canvasRect, ScreenTint);
            Stretch(screenTint.rectTransform);

            var buttonStack = CreateRect("ButtonStack", canvasRect);
            buttonStack.anchorMin = new Vector2(0.34f, 0.12f);
            buttonStack.anchorMax = new Vector2(0.66f, 0.56f);
            buttonStack.offsetMin = Vector2.zero;
            buttonStack.offsetMax = Vector2.zero;
            buttonStack.localScale = Vector3.one;
            var layout = GetOrAddComponent<VerticalLayoutGroup>(buttonStack.gameObject);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 18f;

            build.StartGameButton = CreateMenuButton(buttonStack, "StartGame", "NEW GAME", buttonSprite, 88f);
            build.LoadGameButton = CreateMenuButton(buttonStack, "LoadGameButton", "LOAD GAME", buttonSprite, 88f);
            build.SettingsButton = CreateMenuButton(buttonStack, "SettingsButton", "SETTINGS", buttonSprite, 88f);
            build.QuitButton = CreateMenuButton(buttonStack, "QuitButton", "QUIT", buttonSprite, 88f);
            build.MainTitleGraphic = mainTitleImage;

            build.SettingsPanel = BuildSettingsPanel(canvasRect, buttonSprite);
            build.CharacterCreatorPanel = InstantiateCharacterCreatorPanel(canvasRect, characterCreatorPrefab);

            if (build.SettingsPanel != null) build.SettingsPanel.gameObject.SetActive(false);

            if (build.CharacterCreatorPanel != null) build.CharacterCreatorPanel.gameObject.SetActive(false);

            return build;
        }

        private static SettingsMenuController BuildSettingsPanel(Transform parent, Sprite buttonSprite)
        {
            var panelRoot = CreateRect("SettingsPanel", parent);
            Stretch(panelRoot);

            var controller = GetOrAddComponent<SettingsMenuController>(panelRoot.gameObject);

            var backdrop = CreateImage("Backdrop", panelRoot, ModalBackdropTint);
            Stretch(backdrop.rectTransform);

            var card = CreateImage("SettingsCard", panelRoot, ModalCardTint);
            SetAnchors(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            card.rectTransform.sizeDelta = new Vector2(640f, 320f);

            var content = CreateRect("Content", card.transform);
            Stretch(content, 32f, 28f, 32f, 28f);
            var vertical = GetOrAddComponent<VerticalLayoutGroup>(content.gameObject);
            vertical.childAlignment = TextAnchor.UpperLeft;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            vertical.spacing = 16f;

            CreateText("Title", content, "SETTINGS", 36f, FontStyles.Bold, TextTint, TextAlignmentOptions.Left);
            CreateText(
                "Body",
                content,
                "Quick Dev Tools created a lightweight settings shell here. The Settings button is wired and the panel can be expanded later with more controls.",
                20f,
                FontStyles.Normal,
                SubtleTextTint,
                TextAlignmentOptions.Left);

            var closeButton = CreateMenuButton(content, "CloseButton", "CLOSE", buttonSprite, 68f);

            var settingsSo = new SerializedObject(controller);
            SetObjectReferenceIfPresent(settingsSo, "panelRoot", panelRoot.gameObject);
            SetObjectReferenceIfPresent(settingsSo, "masterVolumeSlider", null);
            SetObjectReferenceIfPresent(settingsSo, "qualityDropdown", null);
            SetObjectReferenceIfPresent(settingsSo, "closeButton", closeButton);
            settingsSo.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        private static CharacterCreatorController InstantiateCharacterCreatorPanel(Transform parent,
            GameObject characterCreatorPrefab)
        {
            var nestedInstance = PrefabUtility.InstantiatePrefab(characterCreatorPrefab, parent.gameObject.scene) as
                                 GameObject;
            if (nestedInstance == null)
            {
                Debug.LogError("[MainMenuQuickDevBuildTool] Failed to instantiate CharacterCreatorPanel nested prefab.");
                return null;
            }

            nestedInstance.name = "CharacterCreatorPanel";
            nestedInstance.transform.SetParent(parent, false);
            nestedInstance.transform.SetAsLastSibling();

            if (nestedInstance.transform is RectTransform nestedRect) Stretch(nestedRect);

            return nestedInstance.GetComponent<CharacterCreatorController>();
        }

        private static void BindController(MainMenuController controller, GameObject prefabRoot, BuildContext build)
        {
            var controllerSo = new SerializedObject(controller);
            var preservedLoadSlotId = GetStringIfPresent(controllerSo, "loadGameSlotId");
            var preservedSaveFolderName = GetStringIfPresent(controllerSo, "loadGameSaveFolderName");
            var preservedDisableMissingLoad = GetBoolIfPresent(controllerSo, "disableLoadButtonWhenSaveMissing", true);

            SetObjectReferenceIfPresent(controllerSo, "menuRoot", prefabRoot);
            SetObjectReferenceIfPresent(controllerSo, "startGameButton", build.StartGameButton);
            SetObjectReferenceIfPresent(controllerSo, "loadGameButton", build.LoadGameButton);
            SetObjectReferenceIfPresent(controllerSo, "settingsButton", build.SettingsButton);
            SetObjectReferenceIfPresent(controllerSo, "quitButton", build.QuitButton);
            SetObjectReferenceIfPresent(controllerSo, "characterCreatorPanel", build.CharacterCreatorPanel);
            SetObjectReferenceIfPresent(controllerSo, "settingsPanel", build.SettingsPanel);
            SetObjectReferenceIfPresent(controllerSo, "mainTitleImage", build.MainTitleGraphic);
            SetStringIfPresent(controllerSo, "loadGameSlotId", preservedLoadSlotId);
            SetBoolIfPresent(controllerSo, "disableLoadButtonWhenSaveMissing", preservedDisableMissingLoad);
            SetStringIfPresent(controllerSo, "loadGameSaveFolderName",
                string.IsNullOrWhiteSpace(preservedSaveFolderName) ? "Saves" : preservedSaveFolderName);
            controllerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button CreateMenuButton(Transform parent, string name, string label, Sprite backgroundSprite,
            float height = 72f)
        {
            var buttonRect = CreateRect(name, parent);
            var layoutElement = GetOrAddComponent<LayoutElement>(buttonRect.gameObject);
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0f;

            var image = GetOrAddComponent<Image>(buttonRect.gameObject);
            image.sprite = backgroundSprite;
            image.type = Image.Type.Sliced;
            image.color = ButtonTint;

            var shadow = GetOrAddComponent<Shadow>(buttonRect.gameObject);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;

            var outline = GetOrAddComponent<Outline>(buttonRect.gameObject);
            outline.effectColor = new Color(AreaBorderTint.r, AreaBorderTint.g, AreaBorderTint.b, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            var button = GetOrAddComponent<Button>(buttonRect.gameObject);
            var colors = button.colors;
            colors.normalColor = ButtonTint;
            colors.highlightedColor = ButtonHighlightTint;
            colors.pressedColor = ButtonPressedTint;
            colors.selectedColor = ButtonHighlightTint;
            colors.disabledColor = ButtonDisabledTint;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var labelText = CreateText("Label", buttonRect, label, 30f, FontStyles.Bold, TextTint,
                TextAlignmentOptions.Center);
            Stretch(labelText.rectTransform, 28f, 10f, 28f, 10f);

            return button;
        }

        private static Sprite EnsureRoundedButtonSprite()
        {
            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(),
                RoundedButtonSpritePath.Replace('/', Path.DirectorySeparatorChar));
            var absoluteDirectory = Path.GetDirectoryName(absolutePath);

            if (!string.IsNullOrWhiteSpace(absoluteDirectory) && !Directory.Exists(absoluteDirectory))
                Directory.CreateDirectory(absoluteDirectory);

            if (!File.Exists(absolutePath))
            {
                var texture = new Texture2D(RoundedButtonSpriteSize, RoundedButtonSpriteSize, TextureFormat.RGBA32,
                    false);
                var colors = new Color32[RoundedButtonSpriteSize * RoundedButtonSpriteSize];
                var halfExtents = new Vector2((RoundedButtonSpriteSize - 1) * 0.5f,
                    (RoundedButtonSpriteSize - 1) * 0.5f);
                var innerHalf = halfExtents - new Vector2(RoundedButtonCornerRadius, RoundedButtonCornerRadius);
                var edgeSoftness = 1.5f;

                for (var y = 0; y < RoundedButtonSpriteSize; y++)
                for (var x = 0; x < RoundedButtonSpriteSize; x++)
                {
                    var point = new Vector2(Mathf.Abs(x - halfExtents.x), Mathf.Abs(y - halfExtents.y)) - innerHalf;
                    var outside = new Vector2(Mathf.Max(point.x, 0f), Mathf.Max(point.y, 0f));
                    var signedDistance = outside.magnitude + Mathf.Min(Mathf.Max(point.x, point.y), 0f) -
                                         RoundedButtonCornerRadius;
                    var alpha = 1f - Mathf.InverseLerp(-edgeSoftness, edgeSoftness, signedDistance);
                    colors[y * RoundedButtonSpriteSize + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }

                texture.SetPixels32(colors);
                texture.Apply(false, false);
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(RoundedButtonSpritePath, ImportAssetOptions.ForceSynchronousImport);

            if (AssetImporter.GetAtPath(RoundedButtonSpritePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = RoundedButtonSpriteSize;
                importer.spriteBorder = new Vector4(
                    RoundedButtonCornerRadius,
                    RoundedButtonCornerRadius,
                    RoundedButtonCornerRadius,
                    RoundedButtonCornerRadius);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedButtonSpritePath);
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize,
            FontStyles fontStyle, Color color, TextAlignmentOptions alignment)
        {
            var rect = CreateRect(name, parent);
            var textComponent = GetOrAddComponent<TextMeshProUGUI>(rect.gameObject);
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.color = color;
            textComponent.alignment = alignment;
            // TMP deprecated enableWordWrapping; use wrapping mode instead.
            textComponent.textWrappingMode = TextWrappingModes.Normal;
            textComponent.raycastTarget = false;

            if (TMP_Settings.defaultFontAsset != null) textComponent.font = TMP_Settings.defaultFontAsset;

            return textComponent;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = GetOrAddComponent<Image>(rect.gameObject);
            image.color = color;
            return image;
        }

        private static RawImage CreateRawImage(string name, Transform parent, Texture texture, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = GetOrAddComponent<RawImage>(rect.gameObject);
            image.texture = texture;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = 5;
            return go.GetComponent<RectTransform>();
        }

        private static void ResetRootTransform(Transform root)
        {
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        private static int DestroyChildrenImmediate(Transform parent)
        {
            var removed = 0;
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(parent.GetChild(index).gameObject);
                removed++;
            }

            return removed;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f,
            float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;

            root.layer = layer;

            for (var index = 0; index < root.transform.childCount; index++)
                SetLayerRecursively(root.transform.GetChild(index).gameObject, layer);
        }

        private static void SetObjectReferenceIfPresent(SerializedObject so, string propertyName, Object value)
        {
            var property = so.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetStringIfPresent(SerializedObject so, string propertyName, string value)
        {
            var property = so.FindProperty(propertyName);
            if (property != null) property.stringValue = value ?? string.Empty;
        }

        private static string GetStringIfPresent(SerializedObject so, string propertyName)
        {
            var property = so.FindProperty(propertyName);
            return property != null ? property.stringValue : string.Empty;
        }

        private static void SetBoolIfPresent(SerializedObject so, string propertyName, bool value)
        {
            var property = so.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
        }

        private static bool GetBoolIfPresent(SerializedObject so, string propertyName, bool fallbackValue)
        {
            var property = so.FindProperty(propertyName);
            return property != null ? property.boolValue : fallbackValue;
        }

        private sealed class BuildContext
        {
            public Button StartGameButton;
            public Button LoadGameButton;
            public Button SettingsButton;
            public Button QuitButton;
            public RawImage MainTitleGraphic;
            public CharacterCreatorController CharacterCreatorPanel;
            public SettingsMenuController SettingsPanel;
        }
    }
}