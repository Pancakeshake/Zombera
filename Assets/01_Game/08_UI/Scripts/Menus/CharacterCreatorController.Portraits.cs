#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zombera.Core;
using Zombera.UI.Menus.CharacterCreation;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Random = UnityEngine.Random;

#endregion

namespace Zombera.UI.Menus
{
    public sealed partial class CharacterCreatorController
    {

        private void ApplySavedPortraitSelection()
        {
            var catalog = ResolvePortraitCatalog();
            if (catalog == null || catalog.Count == 0)
            {
                _selectedPortraitCatalogIndex = -1;
                return;
            }

            var resolvedIndex = catalog.IndexOfPortraitId(CharacterSelectionState.SelectedPortraitId);
            if (resolvedIndex < 0 && CharacterSelectionState.SelectedPortraitSprite != null)
                resolvedIndex = catalog.IndexOfSprite(CharacterSelectionState.SelectedPortraitSprite);

            if (resolvedIndex < 0)
                resolvedIndex = useRandomPortraitWhenNoSavedSelection ? Random.Range(0, catalog.Count) : 0;

            _selectedPortraitCatalogIndex = Mathf.Clamp(resolvedIndex, 0, catalog.Count - 1);
            ApplySelectedPortraitToRuntimeState();
        }


        private CharacterPortraitCatalog ResolvePortraitCatalog()
        {
            if (portraitCatalog != null) return portraitCatalog;

            portraitCatalog = CharacterPortraitCatalog.LoadDefault();

        #if UNITY_EDITOR
            if (portraitCatalog == null && !string.IsNullOrWhiteSpace(portraitCatalogAssetPath))
                portraitCatalog = AssetDatabase.LoadAssetAtPath<CharacterPortraitCatalog>(
                    portraitCatalogAssetPath);

            if (portraitCatalog == null)
                portraitCatalog = BuildEditorFallbackPortraitCatalog();
        #endif

            return portraitCatalog;
        }


        private void EnsurePortraitSelectionInitialized(CharacterPortraitCatalog catalog)
        {
            if (catalog == null || catalog.Count == 0)
            {
                _selectedPortraitCatalogIndex = -1;
                return;
            }

            if (_selectedPortraitCatalogIndex >= 0 && _selectedPortraitCatalogIndex < catalog.Count &&
                catalog.GetPortraitSprite(_selectedPortraitCatalogIndex) != null)
                return;

            var resolvedIndex = catalog.IndexOfPortraitId(CharacterSelectionState.SelectedPortraitId);
            if (resolvedIndex < 0 && CharacterSelectionState.SelectedPortraitSprite != null)
                resolvedIndex = catalog.IndexOfSprite(CharacterSelectionState.SelectedPortraitSprite);

            if (resolvedIndex < 0)
                resolvedIndex = useRandomPortraitWhenNoSavedSelection ? Random.Range(0, catalog.Count) : 0;

            _selectedPortraitCatalogIndex = Mathf.Clamp(resolvedIndex, 0, catalog.Count - 1);
        }


        private Sprite ResolveSelectedPortraitSprite(out string portraitId)
        {
            portraitId = string.Empty;

            var catalog = ResolvePortraitCatalog();
            if (catalog == null || catalog.Count == 0)
            {
                _selectedPortraitCatalogIndex = -1;
                return null;
            }

            EnsurePortraitSelectionInitialized(catalog);
            if (_selectedPortraitCatalogIndex < 0 || _selectedPortraitCatalogIndex >= catalog.Count) return null;

            portraitId = catalog.GetPortraitId(_selectedPortraitCatalogIndex);
            return catalog.GetPortraitSprite(_selectedPortraitCatalogIndex);
        }


        private void StepPortraitSelection(int direction)
        {
            var catalog = ResolvePortraitCatalog();
            if (catalog == null || catalog.Count == 0)
            {
                _selectedPortraitCatalogIndex = -1;
                RefreshPreview();
                return;
            }

            EnsurePortraitSelectionInitialized(catalog);
            var step = direction >= 0 ? 1 : -1;
            _selectedPortraitCatalogIndex = (_selectedPortraitCatalogIndex + step + catalog.Count) % catalog.Count;

            ApplySelectedPortraitToRuntimeState();
            RefreshPreview();
        }


        private void ApplySelectedPortraitToRuntimeState()
        {
            var portraitSprite = ResolveSelectedPortraitSprite(out var portraitId);
            CharacterSelectionState.SetPortraitSprite(portraitSprite, portraitId);
        }

        private CharacterPortraitCatalog BuildEditorFallbackPortraitCatalog()
        {
            if (string.IsNullOrWhiteSpace(portraitSourceFolderPath)) return null;

            var spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { portraitSourceFolderPath });
            if (spriteGuids == null || spriteGuids.Length == 0) return null;

            var sortedPaths = spriteGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct()
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var entries = new List<CharacterPortraitCatalog.PortraitEntry>();

            for (var i = 0; i < sortedPaths.Length; i++)
            {
                var path = sortedPaths[i];
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrWhiteSpace(guid)) continue;

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .Where(sprite => sprite != null)
                    .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                    .ToArray();

                for (var j = 0; j < sprites.Length; j++)
                {
                    var sprite = sprites[j];
                    entries.Add(new CharacterPortraitCatalog.PortraitEntry
                    {
                        id = guid + ":" + sprite.name,
                        sprite = sprite
                    });
                }
            }

            if (entries.Count == 0) return null;

            if (_editorFallbackPortraitCatalog == null)
            {
                _editorFallbackPortraitCatalog = ScriptableObject.CreateInstance<CharacterPortraitCatalog>();
                _editorFallbackPortraitCatalog.hideFlags = HideFlags.HideAndDontSave;
            }

            _editorFallbackPortraitCatalog.SetPortraitEntries(entries);
            return _editorFallbackPortraitCatalog;
        }


        private void BindPortraitPreviewCycleFallback()
        {
            if (creatorRefs == null || creatorRefs.portraitPreview == null) return;

            // If dedicated portrait controls exist, respect those and avoid adding extra click behavior.
            if (creatorRefs.previousPortraitButton != null || creatorRefs.nextPortraitButton != null ||
                creatorRefs.randomPortraitButton != null)
                return;

            var previewButton = creatorRefs.portraitPreview.GetComponent<Button>();
            if (previewButton == null)
                previewButton = creatorRefs.portraitPreview.gameObject.AddComponent<Button>();

            previewButton.transition = Selectable.Transition.None;
            previewButton.targetGraphic = creatorRefs.portraitPreview;
            creatorRefs.portraitPreview.raycastTarget = true;

            BindButton(previewButton, SelectNextPortrait);
        }
    }
}
