#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zombera.Data;

namespace Zombera.Editor
{
    public static class BuildingSkinReskinTool
    {
        private const string BuildingPrefabsRoot = "Assets/02_Shared/Prefabs/Building";

        public const string MaterialsRoot = "Assets/02_Shared/Materials/Generic";
        public const string AssembledRoot = BuildingPrefabsRoot + "/Buildings_Modular_Complete";

        private const string MenuReskinWindow = "Tools/Build/Mod Kits/Building Generator/Reskin/Reskin Building(s)...";
        private const string MenuReskinSelectedRandom = "Tools/Build/Mod Kits/Building Generator/Reskin/Reskin Selected (Random Skin)";
        private const string MenuReskinAllRandom = "Tools/Build/Mod Kits/Building Generator/Reskin/Reskin ALL Assembled (Random Skin)";

        [MenuItem(MenuReskinWindow, priority = -500)]
        private static void OpenWindow()
        {
            BuildingSkinReskinWindow.ShowWindow();
        }

        [MenuItem(MenuReskinSelectedRandom, priority = -500)]
        private static void ReskinSelectedRandom()
        {
            var skins = BuildingSkinLibrary.Scan(MaterialsRoot);
            if (skins.Count == 0)
            {
                EditorUtility.DisplayDialog("Reskin", $"No skins found under '{MaterialsRoot}'.", "OK");
                return;
            }

            var skin = skins[UnityEngine.Random.Range(0, skins.Count)];
            var count = ApplySkinToSelection(skin, randomPerTarget: false);
            EditorUtility.DisplayDialog("Reskin", $"Applied skin '{skin.Name}' to {count} target(s).", "OK");
        }

        [MenuItem(MenuReskinAllRandom, priority = -500)]
        private static void ReskinAllAssembledRandom()
        {
            var skins = BuildingSkinLibrary.Scan(MaterialsRoot);
            if (skins.Count == 0)
            {
                EditorUtility.DisplayDialog("Reskin", $"No skins found under '{MaterialsRoot}'.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(AssembledRoot))
            {
                EditorUtility.DisplayDialog("Reskin", $"Folder not found: '{AssembledRoot}'", "OK");
                return;
            }

            var skin = skins[UnityEngine.Random.Range(0, skins.Count)];
            var changed = ApplySkinToAllAssembledPrefabs(skin, randomPerTarget: false);
            EditorUtility.DisplayDialog("Reskin", $"Applied skin '{skin.Name}' to {changed} prefab(s).", "OK");
        }

        public static int ApplySkinToSelection(BuildingSkin skin, bool randomPerTarget)
        {
            if (skin == null)
                return 0;

            var targets = CollectTargetsFromSelection();
            var changed = 0;
            foreach (var t in targets)
            {
                if (TryApplySkinToTarget(t, skin, randomPerTarget))
                    changed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return changed;
        }

        private static bool TryApplySkinToTarget(Target t, BuildingSkin skin, bool randomPerTarget)
        {
            var chosen = randomPerTarget ? PickRandomSkin() : skin;
            if (chosen == null)
                return false;

            if (t.Kind == TargetKind.PrefabAsset)
                return ReskinPrefabAsset(t.PrefabPath, chosen);

            if (t.Kind == TargetKind.SceneObject && t.SceneRoot != null)
                return ReskinSceneObject(t.SceneRoot, chosen);

            return false;
        }

        public static int ApplySkinToAllAssembledPrefabs(BuildingSkin skin, bool randomPerTarget)
        {
            if (!AssetDatabase.IsValidFolder(AssembledRoot))
                return 0;

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { AssembledRoot });
            var changed = 0;
            for (var i = 0; i < prefabGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var chosen = randomPerTarget ? PickRandomSkin() : skin;
                if (chosen == null)
                    continue;

                if (!ReskinPrefabAsset(path, chosen))
                    continue;
                changed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return changed;
        }

        /// <summary>
        /// Builds a <see cref="BuildingSkin"/> from a <see cref="SkinSet"/> and applies it
        /// to the current selection. Each call to BuildBuildingSkin() re-rolls the weighted
        /// material picks, so randomPerTarget=true produces different results per target.
        /// </summary>
        public static int ApplySkinSetToSelection(SkinSet skinSet, bool randomPerTarget)
        {
            if (skinSet == null)
                return 0;

            var baseSkin = skinSet.BuildBuildingSkin();
            return ApplySkinToSelection(baseSkin, randomPerTarget);
        }

        /// <summary>
        /// Builds a <see cref="BuildingSkin"/> from a <see cref="SkinSet"/> and applies it
        /// to all assembled prefabs under <see cref="AssembledRoot"/>.
        /// </summary>
        public static int ApplySkinSetToAllAssembledPrefabs(SkinSet skinSet, bool randomPerTarget)
        {
            if (skinSet == null)
                return 0;

            var baseSkin = skinSet.BuildBuildingSkin();
            return ApplySkinToAllAssembledPrefabs(baseSkin, randomPerTarget);
        }

        /// <summary>
        /// Picks a skin from a <see cref="BuildingSkinTable"/> for the given zone,
        /// builds a <see cref="BuildingSkin"/>, and applies it to a single prefab asset path.
        /// Returns true if the prefab was modified.
        /// </summary>
        public static bool ApplySkinFromTableToPrefab(
            BuildingSkinTable table,
            string zoneName,
            string prefabPath,
            string condition = "")
        {
            if (table == null || string.IsNullOrWhiteSpace(prefabPath))
                return false;

            var skinSet = table.PickSkin(zoneName, condition);
            if (skinSet == null)
                return false;

            var skin = skinSet.BuildBuildingSkin();
            return ReskinPrefabAsset(prefabPath, skin);
        }

        private static BuildingSkin PickRandomSkin()
        {
            var skins = BuildingSkinLibrary.Scan(MaterialsRoot);
            if (skins.Count == 0)
                return null;
            return skins[UnityEngine.Random.Range(0, skins.Count)];
        }

        private static bool ReskinPrefabAsset(string prefabPath, BuildingSkin skin)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var changed = ReskinRoot(root, skin);
                if (!changed)
                    return false;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool ReskinSceneObject(GameObject root, BuildingSkin skin)
        {
            var changed = ReskinRoot(root, skin, recordUndo: true);
            if (!changed)
                return false;

            EditorUtility.SetDirty(root);
            return true;
        }

        internal static bool ReskinRoot(GameObject root, BuildingSkin skin, bool recordUndo = false)
        {
            if (root == null || skin == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var changed = false;
            var skippedUnknown = 0;
            var skippedNoMats = 0;
            var skippedSame = 0;
            var applied = 0;

            foreach (var r in renderers)
            {
                // Shop-front pieces keep their glass pane (slot 2); exterior/interior
                // slots take the wall skin. Handled before category checks so the
                // "glass" name match doesn't overwrite the pane with window materials.
                if (TryResolveShopGlassMaterials(r, skin, out var shopMats))
                {
                    if (!MaterialsEqual(r.sharedMaterials, shopMats))
                    {
                        if (recordUndo)
                            Undo.RecordObject(r, "Reskin Building");

                        r.sharedMaterials = shopMats;
                        EditorUtility.SetDirty(r);
                        changed = true;
                        applied++;
                    }
                    continue;
                }

                var category = CategorizeRenderer(r);
                if (category == PieceCategory.Unknown)
                {
                    skippedUnknown++;
                    continue;
                }

                var newMats = skin.ResolveMaterials(category, r.sharedMaterials?.Length ?? 0);
                if (newMats == null || newMats.Length == 0)
                {
                    skippedNoMats++;
                    continue;
                }

                if (MaterialsEqual(r.sharedMaterials, newMats))
                {
                    skippedSame++;
                    continue;
                }

                if (recordUndo)
                    Undo.RecordObject(r, "Reskin Building");

                r.sharedMaterials = newMats;
                EditorUtility.SetDirty(r);
                changed = true;
                applied++;
            }

            if (!changed)
                Debug.LogWarning(
                    $"[BuildingSkinReskinTool] ReskinRoot '{root.name}': no material changes applied. " +
                    $"Renderers={renderers.Length}, Unknown={skippedUnknown}, NoMats={skippedNoMats}, Same={skippedSame}, Applied={applied}.");

            return changed;
        }

        /// <summary>
        ///     Shop-glass storefront pieces keep their glass pane. Exterior (slot 0) and
        ///     interior (slot 1) faces take the wall skin, matching the kit's slot order.
        /// </summary>
        private static bool TryResolveShopGlassMaterials(Renderer r, BuildingSkin skin, out Material[] materials)
        {
            materials = null;
            if (skin == null || r == null)
                return false;
            if (r.sharedMaterials == null || r.sharedMaterials.Length < 3)
                return false;
            if (r.name.IndexOf("ShopGlass", System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            var glass = r.sharedMaterials[2];
            if (glass == null)
                return false;

            materials = new[]
            {
                skin.WallExterior ?? skin.Default,
                skin.WallInterior ?? skin.WallExterior ?? skin.Default,
                glass
            };
            return materials[0] != null;
        }

        private static bool MaterialsEqual(Material[] a, Material[] b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;
            if (a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (!ReferenceEquals(a[i], b[i]))
                    return false;
            }
            return true;
        }

        private static PieceCategory CategorizeRenderer(Renderer r)
        {
            var t = r.transform;

            // Window_Modular_*, Window_Moulding, and FuseBox meshes keep their original materials.
            var rendererName = t.name;
            if (rendererName.IndexOf("Window_Modular", System.StringComparison.OrdinalIgnoreCase) >= 0
                || rendererName.IndexOf("Window_Moulding", System.StringComparison.OrdinalIgnoreCase) >= 0
                || rendererName.IndexOf("FuseBox", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return PieceCategory.Unknown;

            var byName = CategorizeByName(t.name, t);
            if (byName != PieceCategory.Unknown)
                return byName;

            return CategorizeByParent(t);
        }

        private static PieceCategory CategorizeByName(string name, Transform t)
        {
            if (name.IndexOf("foundation", StringComparison.OrdinalIgnoreCase) >= 0)
                return PieceCategory.Foundation;
            if (name.IndexOf("stair", StringComparison.OrdinalIgnoreCase) >= 0)
                return PieceCategory.Stairs;
            if (name.IndexOf("gable", StringComparison.OrdinalIgnoreCase) >= 0)
                return PieceCategory.Gable;
            if (name.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0)
                return PieceCategory.Roof;
            // Window_Modular_* meshes (glass/frame interior) keep their original materials.
            if (name.IndexOf("window_modular", StringComparison.OrdinalIgnoreCase) >= 0)
                return PieceCategory.Unknown;
            if (name.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("pane", StringComparison.OrdinalIgnoreCase) >= 0)
                return PieceCategory.Windows;
            // Actual override door prefabs — keep their own materials.
            // Must come before IntWall check since interior override doors are named IntWall_Door_*.
            // Exclude DoorFrame and Doorway (kit doorway pieces) so they get wall materials.
            if ((name.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0
                 && name.IndexOf("DoorFrame", StringComparison.OrdinalIgnoreCase) < 0
                 && name.IndexOf("Doorway", StringComparison.OrdinalIgnoreCase) < 0)
                || HasAncestorWithDoorName(t))
                return PieceCategory.Unknown;
            if (name.StartsWith("IntWall", StringComparison.OrdinalIgnoreCase)
                || HasAncestorStartingWith(t, "IntWall"))
                return PieceCategory.InteriorWalls;
            if (name.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0)
                return PieceCategory.Walls;
            if (name.IndexOf("ceiling", StringComparison.OrdinalIgnoreCase) >= 0
                || HasAncestorWithName(t, "ceiling"))
                return PieceCategory.InteriorWalls;
            if (name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0)
                return PieceCategory.Floors;

            return PieceCategory.Unknown;
        }

        private static PieceCategory CategorizeByParent(Transform t)
        {
            for (var i = 0; i < 4 && t != null; i++, t = t.parent)
            {
                // Skip actual door prefabs but NOT door frames or doorway pieces
                if (t.name.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0
                    && t.name.IndexOf("DoorFrame", StringComparison.OrdinalIgnoreCase) < 0
                    && t.name.IndexOf("Doorway", StringComparison.OrdinalIgnoreCase) < 0)
                    return PieceCategory.Unknown;
                if (string.Equals(t.name, "Walls", StringComparison.OrdinalIgnoreCase))
                    return PieceCategory.Walls;
                if (string.Equals(t.name, "Floors", StringComparison.OrdinalIgnoreCase))
                    return PieceCategory.Floors;
                if (string.Equals(t.name, "Foundation", StringComparison.OrdinalIgnoreCase))
                    return PieceCategory.Foundation;
                if (string.Equals(t.name, "Stairs", StringComparison.OrdinalIgnoreCase))
                    return PieceCategory.Stairs;
                if (string.Equals(t.name, "Roof", StringComparison.OrdinalIgnoreCase))
                    return PieceCategory.Roof;
            }

            return PieceCategory.Unknown;
        }

        /// <summary>
        ///     Walks up the transform hierarchy and returns true if any ancestor's name
        ///     contains <paramref name="fragment"/> (case-insensitive).
        /// </summary>
        private static bool HasAncestorWithName(Transform t, string fragment)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                if (p.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        ///     Returns true if any ancestor contains "Door" but NOT "DoorFrame" or "Doorway".
        ///     This prevents door frame/way kit pieces from being skipped during reskin.
        /// </summary>
        private static bool HasAncestorWithDoorName(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                var n = p.name;
                if (n.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0
                    && n.IndexOf("DoorFrame", StringComparison.OrdinalIgnoreCase) < 0
                    && n.IndexOf("Doorway", StringComparison.OrdinalIgnoreCase) < 0)
                    return true;
            }
            return false;
        }

        private static bool HasAncestorStartingWith(Transform t, string prefix)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                if (p.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static List<Target> CollectTargetsFromSelection()
        {
            var results = GatherSelectionTargets();
            NormalizeSceneRoots(results);
            return DeduplicateTargets(results);
        }

        private static List<Target> GatherSelectionTargets()
        {
            var results = new List<Target>();
            foreach (var obj in Selection.objects)
            {
                if (obj == null) continue;

                var path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrWhiteSpace(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(Target.Prefab(path));
                    continue;
                }

                if (obj is GameObject go)
                    results.Add(Target.Scene(go));
            }

            return results;
        }

        private static void NormalizeSceneRoots(List<Target> results)
        {
            for (var i = 0; i < results.Count; i++)
            {
                if (results[i].Kind != TargetKind.SceneObject || results[i].SceneRoot == null)
                    continue;
                results[i] = Target.Scene(results[i].SceneRoot.transform.root.gameObject);
            }
        }

        private static List<Target> DeduplicateTargets(List<Target> results)
        {
            var seenPrefab = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenGo = new HashSet<int>();
            var dedup = new List<Target>();
            foreach (var t in results)
            {
                if (t.Kind == TargetKind.PrefabAsset)
                {
                    if (seenPrefab.Add(t.PrefabPath))
                        dedup.Add(t);
                    continue;
                }

                if (t.Kind == TargetKind.SceneObject && t.SceneRoot != null)
                {
                    var id = t.SceneRoot.GetInstanceID();
                    if (seenGo.Add(id))
                        dedup.Add(t);
                }
            }

            return dedup;
        }

        private enum TargetKind
        {
            PrefabAsset,
            SceneObject
        }

        private readonly struct Target
        {
            public readonly TargetKind Kind;
            public readonly string PrefabPath;
            public readonly GameObject SceneRoot;

            private Target(TargetKind kind, string prefabPath, GameObject sceneRoot)
            {
                Kind = kind;
                PrefabPath = prefabPath;
                SceneRoot = sceneRoot;
            }

            public static Target Prefab(string path) => new(TargetKind.PrefabAsset, path, null);
            public static Target Scene(GameObject go) => new(TargetKind.SceneObject, null, go);
        }
    }

    internal static class BuildingSkinLibrary
    {
        // Folder convention — two supported layouts:
        //
        // A) Flat category layout (all categories directly under MaterialsRoot):
        //   Materials/Walls_external/*.mat
        //   Materials/Walls_Internal/*.mat
        //   Materials/Floors/*.mat
        //   Materials/Foundations/*.mat
        //   Materials/Roof/*.mat  (also: Decks)
        //   Materials/Stairs/*.mat
        //   → Treated as a single skin named after the root folder.
        //
        // B) Per-skin layout (each subfolder is one skin):
        //   Materials/MySkin/Walls/Exterior/*.mat  (or Walls_external)
        //   Materials/MySkin/Walls/Interior/*.mat  (or Walls_Internal)
        //   Materials/MySkin/Floors/*.mat
        //   etc.
        //
        // Fallback: any *.mat directly under a folder is treated as Default.

        private static readonly HashSet<string> KnownCategoryFolderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "floors", "floor",
            "foundations", "foundation",
            "roof", "roofs",
            "stairs", "stair",
            "decks", "deck",
            "walls_external", "walls_ext", "walls_exterior",
            "walls_internal", "walls_int", "walls_interior",
        };

        private static readonly HashSet<string> GenericMaterialTypeNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "brick", "concrete", "fabric", "glass", "graffiti",
            "metal", "plaster", "terrain", "tile", "wood"
        };

        public static List<BuildingSkin> Scan(string materialsRoot)
        {
            if (!AssetDatabase.IsValidFolder(materialsRoot))
                return new List<BuildingSkin>();

            var subFolders = AssetDatabase.GetSubFolders(materialsRoot);
            var skins = new List<BuildingSkin>();

            // If there are no subfolders, treat the root as a single skin (legacy flat Materials folder).
            if (subFolders == null || subFolders.Length == 0)
            {
                var legacy = LoadLegacy(materialsRoot);
                if (legacy != null)
                    skins.Add(legacy);
                return skins;
            }

            // Detect flat category layout: subfolders are category names, not skin names.
            if (IsCategoryFolderLayout(subFolders))
            {
                var skin = LoadAsSingleSkinFromCategoryLayout(materialsRoot);
                if (skin != null)
                    skins.Add(skin);
                return skins;
            }

            // Detect Generic material-type layout: subfolders are material types (Brick, Concrete, Wood, etc.)
            if (IsGenericLayout(subFolders))
            {
                var skin = LoadFromGenericLayout(materialsRoot);
                if (skin != null)
                    skins.Add(skin);
                return skins;
            }

            foreach (var skinFolder in subFolders)
            {
                var skin = LoadFromSkinFolder(skinFolder);
                if (skin != null)
                    skins.Add(skin);
            }

            // If the root also contains mats, offer it as "Default"
            var rootLegacy = LoadLegacy(materialsRoot);
            if (rootLegacy != null)
            {
                rootLegacy.Name = "Default";
                skins.Insert(0, rootLegacy);
            }

            return skins;
        }

        private static bool IsGenericLayout(string[] subFolders)
        {
            var matchCount = 0;
            foreach (var f in subFolders)
            {
                var name = Path.GetFileName(f.TrimEnd('/'));
                if (GenericMaterialTypeNames.Contains(name))
                    matchCount++;
            }
            return matchCount > 0 && matchCount * 2 >= subFolders.Length;
        }

        private static BuildingSkin LoadFromGenericLayout(string root)
        {
            var skin = new BuildingSkin { Name = Path.GetFileName(root.TrimEnd('/')) };

            // Map material-type folders to building-piece categories.
            // Each folder contributes one random material to its primary category.
            skin.WallExterior = PickRandomMatInFolder(root, "Brick")
                ?? PickRandomMatInFolder(root, "Plaster")
                ?? PickRandomMatInFolder(root, "Concrete");
            skin.WallInterior = PickRandomMatInFolder(root, "Plaster")
                ?? PickRandomMatInFolder(root, "Fabric")
                ?? skin.WallExterior;
            skin.Floor = PickRandomMatInFolder(root, "Wood")
                ?? PickRandomMatInFolder(root, "Tile")
                ?? PickRandomMatInFolder(root, "Concrete");
            skin.Foundation = PickRandomMatInFolder(root, "Concrete")
                ?? skin.Floor;
            skin.Roof = PickRandomMatInFolder(root, "Metal")
                ?? PickRandomMatInFolder(root, "Tile")
                ?? skin.Floor;
            skin.Stairs = PickRandomMatInFolder(root, "Wood")
                ?? skin.Floor;
            skin.Windows = PickRandomMatInFolder(root, "Glass")
                ?? skin.Default;
            skin.Default = skin.WallExterior ?? skin.Floor ?? skin.Foundation;

            return skin.Default != null ? skin : null;
        }

        private static Material PickRandomMatInFolder(string root, string folderName)
        {
            var path = $"{root.TrimEnd('/')}/{folderName}".Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(path))
                return null;
            var mats = LoadMaterialsInFolder(path);
            return mats.Count > 0 ? mats[UnityEngine.Random.Range(0, mats.Count)] : null;
        }

        private static bool IsCategoryFolderLayout(string[] subFolders)
        {
            // If at least half (and at least one) subfolder matches a known category name,
            // treat as flat category layout rather than per-skin layout.
            var matchCount = 0;
            foreach (var f in subFolders)
            {
                var name = Path.GetFileName(f.TrimEnd('/'));
                if (KnownCategoryFolderNames.Contains(name))
                    matchCount++;
            }
            return matchCount > 0 && matchCount * 2 >= subFolders.Length;
        }

        private static BuildingSkin LoadAsSingleSkinFromCategoryLayout(string root)
        {
            var skin = new BuildingSkin { Name = Path.GetFileName(root.TrimEnd('/')) };

            skin.Floor        = LoadFirstMatchingSubfolder(root, "Floors", "Floor");
            skin.Foundation   = LoadFirstMatchingSubfolder(root, "Foundations", "Foundation");
            skin.Roof         = LoadFirstMatchingSubfolder(root, "Roof", "Roofs", "Decks", "Deck");
            skin.Stairs       = LoadFirstMatchingSubfolder(root, "Stairs", "Stair");
            skin.WallExterior = LoadFirstMatchingSubfolder(root, "Walls_external", "Walls_Exterior", "Walls_ext");
            skin.WallInterior = LoadFirstMatchingSubfolder(root, "Walls_Internal", "Walls_Interior", "Walls_int");

            skin.Default = skin.WallExterior ?? skin.Floor ?? skin.Foundation ?? skin.Roof ?? skin.Stairs;
            return skin.Default != null ? skin : null;
        }

        private static Material LoadFirstMatchingSubfolder(string root, params string[] subfolderNames)
        {
            foreach (var name in subfolderNames)
            {
                var mat = LoadFirstMat(root, name);
                if (mat != null)
                    return mat;
            }
            return null;
        }

        private static BuildingSkin LoadLegacy(string folder)
        {
            var mats = LoadMaterialsInFolder(folder);
            if (mats.Count == 0)
                return null;

            var skin = new BuildingSkin
            {
                Name = Path.GetFileName(folder.TrimEnd('/')),
                Default = mats[0]
            };

            // Heuristic mapping by name
            foreach (var m in mats)
            {
                var n = m.name;
                if (n.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("plaster", StringComparison.OrdinalIgnoreCase) >= 0)
                    skin.WallExterior ??= m;
                else if (n.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0
                         || n.IndexOf("plank", StringComparison.OrdinalIgnoreCase) >= 0
                         || n.IndexOf("laminate", StringComparison.OrdinalIgnoreCase) >= 0)
                    skin.Floor ??= m;
                else if (n.IndexOf("metal", StringComparison.OrdinalIgnoreCase) >= 0)
                    skin.WallExterior ??= m;
                else if (n.IndexOf("concrete", StringComparison.OrdinalIgnoreCase) >= 0)
                    skin.Foundation ??= m;
            }

            skin.Default = skin.Default ?? skin.WallExterior ?? skin.Floor ?? skin.Foundation;
            return skin;
        }

        private static BuildingSkin LoadFromSkinFolder(string skinFolder)
        {
            var skinName = Path.GetFileName(skinFolder.TrimEnd('/'));

            var allDirect = LoadMaterialsInFolder(skinFolder);
            var skin = new BuildingSkin { Name = skinName, Default = allDirect.FirstOrDefault() };

            skin.Floor       = LoadFirstMatchingSubfolder(skinFolder, "Floors", "Floor");
            skin.Foundation  = LoadFirstMatchingSubfolder(skinFolder, "Foundation", "Foundations");
            skin.Roof        = LoadFirstMatchingSubfolder(skinFolder, "Roof", "Roofs", "Decks", "Deck");
            skin.Stairs      = LoadFirstMatchingSubfolder(skinFolder, "Stairs", "Stair");
            skin.WallExterior = LoadFirstMatchingSubfolder(skinFolder, "Walls/Exterior", "Walls_external", "Walls_Exterior", "Walls_ext");
            skin.WallInterior = LoadFirstMatchingSubfolder(skinFolder, "Walls/Interior", "Walls_Internal", "Walls_Interior", "Walls_int");

            skin.Default ??= skin.WallExterior ?? skin.Floor ?? skin.Foundation ?? skin.Roof ?? skin.Stairs;
            if (skin.Default == null)
                return null;
            return skin;
        }

        private static Material LoadFirstMat(string skinFolder, string relative)
        {
            var path = $"{skinFolder.TrimEnd('/')}/{relative}".Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(path))
                return null;
            var mats = LoadMaterialsInFolder(path);
            return mats.Count > 0 ? mats[UnityEngine.Random.Range(0, mats.Count)] : null;
        }

        private static List<Material> LoadMaterialsInFolder(string folder)
        {
            var results = new List<Material>();
            var guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrWhiteSpace(path)) continue;
                if (!string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), folder.TrimEnd('/'),
                        StringComparison.OrdinalIgnoreCase))
                    continue; // only direct children
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                    results.Add(mat);
            }

            return results;
        }
    }

    internal sealed class BuildingSkinReskinWindow : EditorWindow
    {
        private readonly List<BuildingSkin> _skins = new();
        private string[] _skinNames = Array.Empty<string>();
        private int _skinIndex;
        private bool _randomPerTarget = true;

        public static void ShowWindow()
        {
            var w = GetWindow<BuildingSkinReskinWindow>();
            w.titleContent = new GUIContent("Reskin Buildings");
            w.minSize = new Vector2(520f, 240f);
            w.Refresh();
        }

        private void Refresh()
        {
            _skins.Clear();
            _skins.AddRange(BuildingSkinLibrary.Scan(BuildingSkinReskinTool.MaterialsRoot));
            _skinNames = _skins.Select(s => s.Name).ToArray();
            _skinIndex = Mathf.Clamp(_skinIndex, 0, Mathf.Max(0, _skins.Count - 1));
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Reskin assembled buildings using materials under " + BuildingSkinReskinTool.MaterialsRoot + ".",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rescan", GUILayout.Width(120f)))
                    Refresh();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Skins found: {_skins.Count}", GUILayout.Width(140f));
            }

            EditorGUILayout.Space(8f);

            if (_skins.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No skins found. Create skin folders under " + BuildingSkinReskinTool.MaterialsRoot + ", e.g.:\n" +
                    BuildingSkinReskinTool.MaterialsRoot + "/MySkin/Floors/*.mat\n" +
                    BuildingSkinReskinTool.MaterialsRoot + "/MySkin/Walls/Exterior/*.mat\n" +
                    BuildingSkinReskinTool.MaterialsRoot + "/MySkin/Walls/Interior/*.mat\n",
                    MessageType.Info);
                return;
            }

            _skinIndex = EditorGUILayout.Popup("Skin", _skinIndex, _skinNames);
            _randomPerTarget = EditorGUILayout.ToggleLeft(
                new GUIContent("Random per target", "If enabled, each selected target gets a random skin. If disabled, uses the chosen skin for all."),
                _randomPerTarget);

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply to Selected", GUILayout.Height(32f)))
                {
                    var skin = _skins[Mathf.Clamp(_skinIndex, 0, _skins.Count - 1)];
                    var count = BuildingSkinReskinTool.ApplySkinToSelection(skin, randomPerTarget: _randomPerTarget);
                    Debug.Log($"[BuildingSkinReskin] Reskinned {count} selected target(s).");
                }
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply to ALL Assembled Prefabs", GUILayout.Height(28f)))
                {
                    var skin = _skins[Mathf.Clamp(_skinIndex, 0, _skins.Count - 1)];
                    var count = BuildingSkinReskinTool.ApplySkinToAllAssembledPrefabs(skin, randomPerTarget: _randomPerTarget);
                    Debug.Log($"[BuildingSkinReskin] Reskinned {count} assembled prefab(s).");
                }
            }
        }
    }
}
#endif

