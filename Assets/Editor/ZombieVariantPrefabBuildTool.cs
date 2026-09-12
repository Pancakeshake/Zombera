#region

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zombera.AI;
using Zombera.Data;

#endregion

namespace Zombera.Editor
{
    /// <summary>
    ///     Builds zombie variant prefabs from model FBX assets and wires type-to-prefab overrides.
    /// </summary>
    public static class ZombieVariantPrefabBuildTool
    {
        private const string ModelsRootPath = "Assets/Models";
        private const string ZombieTypeAssetsFolder = "Assets/ScriptableObjects/ZombieTypes";
        private const string TemplatePrefabPath = "Assets/Prefabs/Zombies/Zombie Type1.prefab";
        private const string OutputFolderPath = "Assets/Prefabs/Zombies";
        private const string GeneratedVisualRootName = "__GeneratedModelVisual__";
        private const string DefaultZombieControllerPath = "Assets/Animations/Zombies/Zombie_Default.controller";

        private sealed class VariantBuildSpec
        {
            public VariantBuildSpec(string outputPrefabName, string modelAssetPath, string zombieTypeAssetPath)
            {
                OutputPrefabName = outputPrefabName;
                ModelAssetPath = modelAssetPath;
                ZombieTypeAssetPath = zombieTypeAssetPath;
            }

            public string OutputPrefabName { get; }
            public string ModelAssetPath { get; }
            public string ZombieTypeAssetPath { get; }
        }

        [MenuItem("Tools/World/Zombies/Build Variant Prefabs From Model Set", priority = -500)]
        private static void BuildVariantPrefabsFromModelSetMenu()
        {
            BuildVariantPrefabsFromModelSet();
        }

        [MenuItem("Tools/World/Zombies/Build Variant Prefabs And Wire Selected ZombieSpawner", priority = -500)]
        private static void BuildVariantPrefabsAndWireSelectedSpawnerMenu()
        {
            BuildVariantPrefabsFromModelSet();
            WireSelectedZombieSpawnerOverrides();
        }

        [MenuItem("Tools/World/Zombies/Build Variant Prefabs And Wire Selected ZombieSpawner", true, priority = -500)]
        private static bool CanBuildVariantPrefabsAndWireSelectedSpawnerMenu()
        {
            return CanWireSelectedZombieSpawnerOverrides();
        }

        public static void BuildVariantPrefabsFromModelSetBatch()
        {
            BuildVariantPrefabsFromModelSet();
        }

        private static void BuildVariantPrefabsFromModelSet()
        {
            if (!EnsureTemplatePrefab()) return;
            if (!AssetDatabase.IsValidFolder(ModelsRootPath))
            {
                Debug.LogError("[ZombieVariantPrefabBuildTool] Missing models folder: " + ModelsRootPath);
                return;
            }

            EnsureFolderPath(OutputFolderPath);

            var variantSpecs = DiscoverVariantSpecsFromModelFolders();

            if (variantSpecs.Count <= 0)
            {
                Debug.LogWarning("[ZombieVariantPrefabBuildTool] No model assets found under " + ModelsRootPath +
                                 ". Place a prefab or FBX directly in each model subfolder.");
                return;
            }

            var built = 0;
            var failed = 0;

            foreach (var spec in variantSpecs)
            {
                var outputPath = OutputFolderPath + "/" + spec.OutputPrefabName + ".prefab";

                if (BuildVariantPrefab(spec.ModelAssetPath, outputPath, spec.OutputPrefabName))
                    built++;
                else
                    failed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (failed <= 0)
                Debug.Log("[ZombieVariantPrefabBuildTool] Built " + built + " zombie variant prefab(s).");
            else
                Debug.LogWarning("[ZombieVariantPrefabBuildTool] Built " + built + " prefab(s), failed " + failed +
                                 " variant(s). See Console for details.");
        }

        [MenuItem("Tools/World/Zombies/Build Variant Prefab From Selected FBX", priority = -500)]
        private static void BuildVariantPrefabFromSelectedFbx()
        {
            if (!EnsureTemplatePrefab()) return;
            if (!TryGetSelectedModelAssetPath(out var modelAssetPath))
            {
                Debug.LogError("[ZombieVariantPrefabBuildTool] Select a prefab or FBX model asset under Assets/Models first.");
                return;
            }

            EnsureFolderPath(OutputFolderPath);

            var outputName = "Zombie_" + SanitizeName(Path.GetFileNameWithoutExtension(modelAssetPath));
            var outputPath = OutputFolderPath + "/" + outputName + ".prefab";

            if (!BuildVariantPrefab(modelAssetPath, outputPath, outputName)) return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ZombieVariantPrefabBuildTool] Built prefab: " + outputPath);
        }

        [MenuItem("Tools/World/Zombies/Build Variant Prefab From Selected FBX", true, priority = -500)]
        private static bool CanBuildVariantPrefabFromSelectedFbx()
        {
            return TryGetSelectedModelAssetPath(out _);
        }

        [MenuItem("Tools/World/Zombies/Wire Selected ZombieSpawner Overrides", priority = -500)]
        private static void WireSelectedZombieSpawnerOverrides()
        {
            var selectedGo = Selection.activeGameObject;

            if (selectedGo == null)
            {
                Debug.LogError("[ZombieVariantPrefabBuildTool] Select a GameObject with ZombieSpawner first.");
                return;
            }

            var spawner = selectedGo.GetComponent<ZombieSpawner>();

            if (spawner == null)
            {
                Debug.LogError("[ZombieVariantPrefabBuildTool] Selected GameObject has no ZombieSpawner component.");
                return;
            }

            var serializedSpawner = new SerializedObject(spawner);
            var overridesProperty = serializedSpawner.FindProperty("zombieTypePrefabOverrides");

            if (overridesProperty == null)
            {
                Debug.LogError(
                    "[ZombieVariantPrefabBuildTool] Could not find zombieTypePrefabOverrides on selected ZombieSpawner.");
                return;
            }

            overridesProperty.ClearArray();

            var added = 0;
            var variantSpecs = DiscoverVariantSpecsFromModelFolders();

            foreach (var spec in variantSpecs)
            {
                if (string.IsNullOrWhiteSpace(spec.ZombieTypeAssetPath)) continue;

                var zombieType = AssetDatabase.LoadAssetAtPath<ZombieType>(spec.ZombieTypeAssetPath);
                var prefabPath = OutputFolderPath + "/" + spec.OutputPrefabName + ".prefab";
                var zombiePrefab = AssetDatabase.LoadAssetAtPath<ZombieController>(prefabPath);

                if (zombieType == null || zombiePrefab == null) continue;

                overridesProperty.InsertArrayElementAtIndex(overridesProperty.arraySize);
                var entry = overridesProperty.GetArrayElementAtIndex(overridesProperty.arraySize - 1);
                entry.FindPropertyRelative("zombieType").objectReferenceValue = zombieType;
                entry.FindPropertyRelative("prefab").objectReferenceValue = zombiePrefab;
                added++;
            }

            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
            AssetDatabase.SaveAssets();

            Debug.Log("[ZombieVariantPrefabBuildTool] Wired " + added + " type override(s) on selected ZombieSpawner.");
        }

        [MenuItem("Tools/World/Zombies/Wire Selected ZombieSpawner Overrides", true, priority = -500)]
        private static bool CanWireSelectedZombieSpawnerOverrides()
        {
            return Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<ZombieSpawner>() != null;
        }

        [MenuItem("Tools/World/Zombies/Repair Animator Controller Wiring On Built Prefabs", priority = -500)]
        private static void RepairAnimatorControllerWiringOnBuiltPrefabs()
        {
            var defaultController = LoadDefaultZombieController();
            if (defaultController == null)
            {
                Debug.LogError("[ZombieVariantPrefabBuildTool] Missing default zombie controller: " +
                               DefaultZombieControllerPath);
                return;
            }

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { OutputFolderPath });
            if (prefabGuids == null || prefabGuids.Length == 0)
            {
                Debug.LogWarning("[ZombieVariantPrefabBuildTool] No prefabs found under " + OutputFolderPath + ".");
                return;
            }

            var updated = 0;
            var scanned = 0;

            foreach (var guid in prefabGuids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(prefabPath)) continue;

                GameObject prefabContents = null;

                try
                {
                    prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (prefabContents == null) continue;

                    var changed = EnsureAnimatorWiring(prefabContents, defaultController);
                    scanned++;

                    if (!changed) continue;

                    PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath, out var success);
                    if (success) updated++;
                }
                finally
                {
                    if (prefabContents != null) PrefabUtility.UnloadPrefabContents(prefabContents);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ZombieVariantPrefabBuildTool] Repaired animator-controller wiring on " + updated +
                      " of " + scanned + " zombie prefab(s).");
        }

        private static bool EnsureTemplatePrefab()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePrefabPath);

            if (template != null) return true;

            Debug.LogError("[ZombieVariantPrefabBuildTool] Missing template prefab: " + TemplatePrefabPath);
            return false;
        }

        private static bool TryGetSelectedModelAssetPath(out string modelAssetPath)
        {
            modelAssetPath = null;

            var selectedObject = Selection.activeObject;
            if (selectedObject == null) return false;

            var candidatePath = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrWhiteSpace(candidatePath)) return false;
            if (!candidatePath.StartsWith("Assets/Models/")) return false;
            if (!candidatePath.EndsWith(".fbx") && !candidatePath.EndsWith(".prefab")) return false;

            modelAssetPath = candidatePath;
            return true;
        }

        private static List<VariantBuildSpec> DiscoverVariantSpecsFromModelFolders()
        {
            var specs = new List<VariantBuildSpec>();

            if (!AssetDatabase.IsValidFolder(ModelsRootPath)) return specs;

            var projectRoot = Directory.GetCurrentDirectory().Replace("\\", "/");
            var modelsAbsPath = (projectRoot + "/" + ModelsRootPath).Replace("\\", "/");

            if (!Directory.Exists(modelsAbsPath)) return specs;

            var modelFolders = Directory.GetDirectories(modelsAbsPath);

            foreach (var folderAbsPath in modelFolders)
            {
                var folderPath = folderAbsPath.Replace("\\", "/");
                var folderName = Path.GetFileName(folderPath);

                if (string.IsNullOrWhiteSpace(folderName)) continue;

                var modelAssetPath = FindPreferredModelAssetPath(folderPath, projectRoot);

                if (string.IsNullOrWhiteSpace(modelAssetPath))
                {
                    Debug.LogWarning("[ZombieVariantPrefabBuildTool] No direct prefab/fbx found in model folder: " +
                                     ModelsRootPath + "/" + folderName);
                    continue;
                }

                var outputPrefabName = "Zombie_" + SanitizeName(folderName);
                var typeAssetPath = ResolveZombieTypeAssetPathForFolder(folderName);

                specs.Add(new VariantBuildSpec(outputPrefabName, modelAssetPath, typeAssetPath));
            }

            return specs;
        }

        private static string FindPreferredModelAssetPath(string folderAbsPath, string projectRoot)
        {
            var directPrefabs = Directory.GetFiles(folderAbsPath, "*.prefab", SearchOption.TopDirectoryOnly);
            if (directPrefabs.Length > 0)
                return ToAssetPath(directPrefabs[0], projectRoot);

            var directFbx = Directory.GetFiles(folderAbsPath, "*.fbx", SearchOption.TopDirectoryOnly);
            if (directFbx.Length > 0)
                return ToAssetPath(directFbx[0], projectRoot);

            return null;
        }

        private static string ToAssetPath(string absolutePath, string projectRoot)
        {
            var normalizedPath = absolutePath.Replace("\\", "/");
            var normalizedRoot = projectRoot.Replace("\\", "/");

            if (!normalizedPath.StartsWith(normalizedRoot)) return null;

            var relativePath = normalizedPath.Substring(normalizedRoot.Length).TrimStart('/');
            return relativePath;
        }

        private static string ResolveZombieTypeAssetPathForFolder(string folderName)
        {
            var key = folderName.ToLowerInvariant();
            var candidateTypeName = "";

            if (key.Contains("civilian")) candidateTypeName = "ZT_Civilian_Shambler";
            else if (key.Contains("construction")) candidateTypeName = "ZT_Construction_Brute";
            else if (key.Contains("farmer")) candidateTypeName = "ZT_Farmer_Reaper";
            else if (key.Contains("firefighter")) candidateTypeName = "ZT_Firefighter_Rusher";
            else if (key.Contains("hazmat")) candidateTypeName = "ZT_Hazmat_Tank";
            else if (key.Contains("hospital") || key.Contains("patient")) candidateTypeName = "ZT_Patient_Sprinter";
            else if (key.Contains("mechanic")) candidateTypeName = "ZT_Mechanic_Brawler";
            else if (key.Contains("military") || key.Contains("soldier")) candidateTypeName = "ZT_Military_Hunter";
            else if (key.Contains("nurse")) candidateTypeName = "ZT_Nurse_Stalker";
            else if (key.Contains("swat")) candidateTypeName = "ZT_SWAT_Juggernaut";

            if (string.IsNullOrWhiteSpace(candidateTypeName)) return null;

            var assetPath = ZombieTypeAssetsFolder + "/" + candidateTypeName + ".asset";
            return AssetDatabase.LoadAssetAtPath<ZombieType>(assetPath) != null ? assetPath : null;
        }

        private static bool BuildVariantPrefab(string modelAssetPath, string outputPrefabPath, string prefabName)
        {
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            var defaultController = LoadDefaultZombieController();

            if (modelPrefab == null)
            {
                Debug.LogError("[ZombieVariantPrefabBuildTool] Missing model asset: " + modelAssetPath);
                return false;
            }

            if (defaultController == null)
            {
                Debug.LogError("[ZombieVariantPrefabBuildTool] Missing default zombie controller: " +
                               DefaultZombieControllerPath);
                return false;
            }

            GameObject prefabContents = null;

            try
            {
                prefabContents = PrefabUtility.LoadPrefabContents(TemplatePrefabPath);

                if (prefabContents == null)
                {
                    Debug.LogError("[ZombieVariantPrefabBuildTool] Failed to load template prefab contents.");
                    return false;
                }

                RemoveLegacyVisualRoots(prefabContents.transform);

                var visualRoot = prefabContents.transform.Find(GeneratedVisualRootName);
                if (visualRoot != null) Object.DestroyImmediate(visualRoot.gameObject);

                visualRoot = new GameObject(GeneratedVisualRootName).transform;
                visualRoot.SetParent(prefabContents.transform, false);

                var modelInstance = Object.Instantiate(modelPrefab, visualRoot);
                modelInstance.name = modelPrefab.name;
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                SetLayerRecursively(modelInstance, prefabContents.layer);

                var modelAnimator = modelInstance.GetComponentInChildren<Animator>(true);
                _ = AssignAnimatorReference(prefabContents, modelAnimator, defaultController);

                prefabContents.name = prefabName;

                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabContents, outputPrefabPath, out var success);
                if (success && savedPrefab != null) return true;

                Debug.LogError("[ZombieVariantPrefabBuildTool] Failed to save prefab: " + outputPrefabPath);
                return false;
            }
            finally
            {
                if (prefabContents != null) PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static RuntimeAnimatorController LoadDefaultZombieController()
        {
            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DefaultZombieControllerPath);
        }

        private static bool EnsureAnimatorWiring(GameObject prefabRoot, RuntimeAnimatorController defaultController)
        {
            if (prefabRoot == null || defaultController == null) return false;

            var animationController = prefabRoot.GetComponent<ZombieAnimationController>();
            if (animationController == null) return false;

            var serializedController = new SerializedObject(animationController);
            var animatorProperty = serializedController.FindProperty("animator");
            if (animatorProperty == null) return false;

            var assignedAnimator = animatorProperty.objectReferenceValue as Animator;
            if (assignedAnimator == null) assignedAnimator = prefabRoot.GetComponentInChildren<Animator>(true);

            return AssignAnimatorReference(prefabRoot, assignedAnimator, defaultController);
        }

        private static bool AssignAnimatorReference(GameObject prefabRoot, Animator animator,
            RuntimeAnimatorController defaultController)
        {
            if (prefabRoot == null || animator == null) return false;

            var animationController = prefabRoot.GetComponent<ZombieAnimationController>();
            if (animationController == null) return false;

            var changed = false;

            if (defaultController != null && animator.runtimeAnimatorController != defaultController)
            {
                animator.runtimeAnimatorController = defaultController;
                changed = true;
            }

            if (animator.applyRootMotion)
            {
                animator.applyRootMotion = false;
                changed = true;
            }

            if (animator.cullingMode != AnimatorCullingMode.CullCompletely)
            {
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                changed = true;
            }

            var serializedController = new SerializedObject(animationController);
            var animatorProperty = serializedController.FindProperty("animator");
            if (animatorProperty == null) return changed;

            if (animatorProperty.objectReferenceValue != animator)
            {
                animatorProperty.objectReferenceValue = animator;
                changed = true;
            }

            if (changed)
            {
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(animator);
                EditorUtility.SetDirty(animationController);
            }

            return changed;
        }

        private static void RemoveLegacyVisualRoots(Transform prefabRoot)
        {
            var removalBuffer = new List<GameObject>();

            foreach (Transform child in prefabRoot)
            {
                if (child == null) continue;

                if (child.name == GeneratedVisualRootName)
                {
                    removalBuffer.Add(child.gameObject);
                    continue;
                }

                var hasRenderer = child.GetComponentInChildren<Renderer>(true) != null;
                var hasAnimator = child.GetComponentInChildren<Animator>(true) != null;

                if (hasRenderer || hasAnimator) removalBuffer.Add(child.gameObject);
            }

            foreach (var go in removalBuffer)
                Object.DestroyImmediate(go);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;

            root.layer = layer;

            foreach (Transform child in root.transform)
                if (child != null)
                    SetLayerRecursively(child.gameObject, layer);
        }

        private static void EnsureFolderPath(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            if (parts.Length <= 0 || parts[0] != "Assets") return;

            var currentPath = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var nextPath = currentPath + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(nextPath)) AssetDatabase.CreateFolder(currentPath, parts[i]);

                currentPath = nextPath;
            }
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Variant";

            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '_';

            return new string(chars);
        }
    }
}
