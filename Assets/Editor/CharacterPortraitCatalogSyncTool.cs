#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zombera.Core;

#endregion

namespace Zombera.Editor
{
    /// <summary>
    ///     Builds a runtime portrait catalog from Art/Character_Portraits.
    /// </summary>
    public static class CharacterPortraitCatalogSyncTool
    {
        private const string SourceFolderPath = "Assets/Art/Character_Portraits";
        private const string ResourcesFolderPath = "Assets/Resources";
        private const string ZomberaResourcesFolderPath = "Assets/Resources/Zombera";
        private const string CatalogAssetPath = "Assets/Resources/Zombera/CharacterPortraitCatalog.asset";

        [MenuItem("Tools/Items/Character Creation/Sync Portrait Catalog", priority = -500)]
        private static void SyncPortraitCatalogFromMenu()
        {
            SyncPortraitCatalog(showDialogOnFailure: true);
        }

        [InitializeOnLoadMethod]
        private static void EnsureCatalogExistsOnEditorLoad()
        {
            if (Application.isBatchMode) return;
            if (!AssetDatabase.IsValidFolder(SourceFolderPath)) return;
            if (AssetDatabase.LoadAssetAtPath<CharacterPortraitCatalog>(CatalogAssetPath) != null) return;

            SyncPortraitCatalog(showDialogOnFailure: false);
        }

        [MenuItem("CONTEXT/CharacterCreatorController/Sync Portrait Catalog")]
        private static void SyncPortraitCatalogFromContext(MenuCommand _)
        {
            SyncPortraitCatalog(showDialogOnFailure: true);
        }

        public static bool SyncPortraitCatalog(bool showDialogOnFailure)
        {
            if (!AssetDatabase.IsValidFolder(SourceFolderPath))
            {
                var message = "Source portrait folder is missing: " + SourceFolderPath;
                Debug.LogWarning("[CharacterPortraitCatalogSyncTool] " + message);
                if (showDialogOnFailure)
                    EditorUtility.DisplayDialog("Portrait Catalog Sync", message, "OK");

                return false;
            }

            EnsureFolder("Assets", "Resources");
            EnsureFolder(ResourcesFolderPath, "Zombera");

            var textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { SourceFolderPath });
            var sortedPaths = textureGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct()
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var importerUpdates = 0;
            for (var i = 0; i < sortedPaths.Length; i++)
                if (EnsureSpriteImporter(sortedPaths[i]))
                    importerUpdates++;

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
                        id = BuildPortraitId(guid, sprite.name),
                        sprite = sprite
                    });
                }
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterPortraitCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterPortraitCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            catalog.SetPortraitEntries(entries);
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[CharacterPortraitCatalogSyncTool] Synced " + entries.Count + " portrait sprite(s) from " +
                SourceFolderPath + " to " + CatalogAssetPath +
                (importerUpdates > 0 ? " (updated " + importerUpdates + " importer(s) to Sprite)." : "."));

            return true;
        }

        private static bool EnsureSpriteImporter(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return false;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;

            var changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode == SpriteImportMode.None)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!changed) return false;

            importer.SaveAndReimport();
            return true;
        }

        private static void EnsureFolder(string parentPath, string folderName)
        {
            var childPath = parentPath + "/" + folderName;
            if (AssetDatabase.IsValidFolder(childPath)) return;

            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static string BuildPortraitId(string guid, string spriteName)
        {
            var normalizedName = string.IsNullOrWhiteSpace(spriteName) ? "Portrait" : spriteName.Trim();
            return guid + ":" + normalizedName;
        }
    }
}