using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Zombera.EditorTools
{
    /// <summary>
    /// Consolidates vendor animation packs into one normalized layout:
    /// Assets/Animations/Animation Packs/CompleteAnimationPack/<Animation_Name>/<Animation_Name>.fbx
    /// </summary>
    public static class CompleteAnimationPackBuilderTool
    {
        private const string SourceRoot = "Assets/Animations/Animation Packs";
        private const string DestinationFolderName = "CompleteAnimationPack";
        private const string ExtractedClipFolderName = "ExtractedClips";
        private const string PreviewFolderName = "Preview";
        private const string PreviewControllerName = "UAL1_ExtractedClipPreview.controller";
        private const string PreviewObjectName = "_UAL1_ExtractedClipPreview";
        private const string AvatarSourceModelPath = "Assets/Animations/Animation Packs/UAL1.fbx";
        private const string VendorPrefix = "Meshy_AI_Create_me_a_standard__biped_";
        private const string VendorSuffix = "_withSkin";
        private const string DefaultAnimationCategory = "Actions";

        private static readonly string[] FlattenCategories =
        {
            "Idle",
            "Walk",
            "Jog",
            "Sprint",
            "Crouch",
            "Crawl",
            "Bow",
            "Combat",
            "Dodge",
            "Hit Reactions",
            "Death",
            DefaultAnimationCategory
        };

        [MenuItem("Tools/Zombera/Animation/Build Complete Animation Pack (1-35)")]
        public static void BuildCompleteAnimationPack()
        {
            if (!AssetDatabase.IsValidFolder(SourceRoot))
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "Source folder was not found:\n" + SourceRoot,
                    "OK");
                return;
            }

            string destinationRoot = SourceRoot + "/" + DestinationFolderName;
            EnsureFolder(destinationRoot);

            List<PackDirectoryInfo> packs = GatherSourcePacks();
            if (packs.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "No AnimationPack1..AnimationPack35 folders were found under:\n" + SourceRoot,
                    "OK");
                return;
            }

            int copiedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;
            int createdFolderCount = 0;

            var seenNormalizedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int packIndex = 0; packIndex < packs.Count; packIndex++)
                {
                    PackDirectoryInfo pack = packs[packIndex];
                    List<string> fbxAssets = GetTopLevelFbxAssets(pack.AssetPath);

                    for (int i = 0; i < fbxAssets.Count; i++)
                    {
                        string sourceAssetPath = fbxAssets[i];
                        string sourceBaseName = Path.GetFileNameWithoutExtension(sourceAssetPath);
                        string normalizedBaseName = NormalizeAnimationName(sourceBaseName);

                        if (string.IsNullOrEmpty(normalizedBaseName))
                        {
                            normalizedBaseName = "Animation";
                        }

                        string uniqueBaseName = BuildUniqueNameForPack(normalizedBaseName, pack.Number, seenNormalizedNames);
                        string targetFolderPath = destinationRoot + "/" + uniqueBaseName;

                        if (EnsureFolder(targetFolderPath))
                        {
                            createdFolderCount++;
                        }

                        string targetAssetPath = targetFolderPath + "/" + uniqueBaseName + ".fbx";

                        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetAssetPath) != null)
                        {
                            skippedCount++;
                            continue;
                        }

                        bool copied = AssetDatabase.CopyAsset(sourceAssetPath, targetAssetPath);
                        if (copied)
                        {
                            copiedCount++;
                        }
                        else
                        {
                            failedCount++;
                            Debug.LogWarning(
                                $"[CompleteAnimationPackBuilderTool] Failed to copy '{sourceAssetPath}' to '{targetAssetPath}'.");
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            EditorUtility.DisplayDialog(
                "Complete Animation Pack",
                "Finished building CompleteAnimationPack.\n\n" +
                $"Source packs: {packs.Count}\n" +
                $"Copied FBX: {copiedCount}\n" +
                $"Skipped existing: {skippedCount}\n" +
                $"Folders created: {createdFolderCount}\n" +
                $"Failed copies: {failedCount}",
                "OK");
        }

        [MenuItem("Tools/Zombera/Animation/Build Complete Animation Pack (1-35)", true)]
        private static bool ValidateBuildCompleteAnimationPack()
        {
            return AssetDatabase.IsValidFolder(SourceRoot);
        }

        [MenuItem("Tools/Zombera/Animation/Organize Complete Animation Pack (Flat Categories)")]
        public static void OrganizeCompleteAnimationPackFlatCategories()
        {
            string completeRoot = SourceRoot + "/" + DestinationFolderName;
            if (!AssetDatabase.IsValidFolder(completeRoot))
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "CompleteAnimationPack was not found. Run the build step first.\n\nExpected folder:\n" + completeRoot,
                    "OK");
                return;
            }

            EnsureCategoryFolders(completeRoot, out int createdCategoryFolderCount);
            List<string> sourceAssets = GetAllFbxAssetsRecursive(completeRoot);

            int movedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < sourceAssets.Count; i++)
                {
                    string sourceAssetPath = sourceAssets[i];
                    if (IsFbxAlreadyFlatCategorized(sourceAssetPath, completeRoot))
                    {
                        continue;
                    }

                    string sourceBaseName = Path.GetFileNameWithoutExtension(sourceAssetPath);
                    string normalizedBaseName = NormalizeAnimationName(sourceBaseName);
                    if (string.IsNullOrEmpty(normalizedBaseName))
                    {
                        normalizedBaseName = sourceBaseName;
                    }

                    string category = ResolveAnimationCategory(normalizedBaseName);
                    string categoryFolderPath = completeRoot + "/" + category;
                    string targetAssetPath = BuildUniqueAssetPath(categoryFolderPath, normalizedBaseName, ".fbx");

                    if (string.Equals(sourceAssetPath, targetAssetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        skippedCount++;
                        continue;
                    }

                    string moveError = AssetDatabase.MoveAsset(sourceAssetPath, targetAssetPath);
                    if (string.IsNullOrEmpty(moveError))
                    {
                        movedCount++;
                    }
                    else
                    {
                        failedCount++;
                        Debug.LogWarning(
                            $"[CompleteAnimationPackBuilderTool] Failed to move '{sourceAssetPath}' to '{targetAssetPath}'. Error: {moveError}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            int removedFolderCount = RemoveEmptyNonCategoryFolders(completeRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Complete Animation Pack",
                "Finished organizing CompleteAnimationPack into flat category folders.\n\n" +
                $"Scanned FBX: {sourceAssets.Count}\n" +
                $"Moved FBX: {movedCount}\n" +
                $"Skipped unchanged: {skippedCount}\n" +
                $"Category folders created: {createdCategoryFolderCount}\n" +
                $"Removed empty folders: {removedFolderCount}\n" +
                $"Failed moves: {failedCount}\n\n" +
                "Note: Unity cannot merge many separate FBX files into one multi-clip FBX file via editor scripting. " +
                "This tool flattens and organizes existing FBX files by category.",
                "OK");
        }

        [MenuItem("Tools/Zombera/Animation/Organize Complete Animation Pack (Flat Categories)", true)]
        private static bool ValidateOrganizeCompleteAnimationPackFlatCategories()
        {
            string completeRoot = SourceRoot + "/" + DestinationFolderName;
            return AssetDatabase.IsValidFolder(completeRoot);
        }

        [MenuItem("Tools/Zombera/Animation/Configure Complete Animation Pack Rig (Humanoid From UAL1)")]
        public static void ConfigureCompleteAnimationPackRigHumanoidFromUal1()
        {
            string completeRoot = SourceRoot + "/" + DestinationFolderName;
            if (!AssetDatabase.IsValidFolder(completeRoot))
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "CompleteAnimationPack was not found. Run the build step first.\n\nExpected folder:\n" + completeRoot,
                    "OK");
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(AvatarSourceModelPath) == null)
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "UAL1 avatar source model was not found:\n" + AvatarSourceModelPath,
                    "OK");
                return;
            }

            if (!EnsureHumanoidSourceAvatarReady(AvatarSourceModelPath))
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "Could not prepare UAL1 as a valid Humanoid avatar source. Check Console for details.",
                    "OK");
                return;
            }

            Avatar sourceAvatar = LoadPrimaryAvatarFromModel(AvatarSourceModelPath);
            if (sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "UAL1 did not provide a valid Humanoid avatar after import.\n\nModel:\n" + AvatarSourceModelPath,
                    "OK");
                return;
            }

            List<string> fbxAssets = GetAllFbxAssetsRecursive(completeRoot);

            int updatedCount = 0;
            int unchangedCount = 0;
            int failedCount = 0;

            for (int i = 0; i < fbxAssets.Count; i++)
            {
                string fbxAssetPath = fbxAssets[i];
                if (fbxAssetPath.IndexOf("/" + ExtractedClipFolderName + "/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                ModelImporter importer = AssetImporter.GetAtPath(fbxAssetPath) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                bool changed = false;
                if (!importer.importAnimation)
                {
                    importer.importAnimation = true;
                    changed = true;
                }

                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    changed = true;
                }

                if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    changed = true;
                }

                if (importer.sourceAvatar != null)
                {
                    importer.sourceAvatar = null;
                    changed = true;
                }

                bool hasValidAvatar = HasValidHumanoidAvatar(fbxAssetPath);
                if (!changed && hasValidAvatar)
                {
                    unchangedCount++;
                    continue;
                }

                try
                {
                    importer.SaveAndReimport();

                    if (HasValidHumanoidAvatar(fbxAssetPath))
                    {
                        updatedCount++;
                    }
                    else
                    {
                        failedCount++;
                        Debug.LogWarning(
                            $"[CompleteAnimationPackBuilderTool] Rig import completed but avatar is not a valid Humanoid for '{fbxAssetPath}'.");
                    }
                }
                catch (Exception exception)
                {
                    failedCount++;
                    Debug.LogWarning(
                        $"[CompleteAnimationPackBuilderTool] Failed to configure rig on '{fbxAssetPath}'. Error: {exception.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Complete Animation Pack",
                "Finished configuring CompleteAnimationPack FBX rig settings.\n\n" +
                $"Scanned FBX: {fbxAssets.Count}\n" +
                $"Updated importers: {updatedCount}\n" +
                $"Already correct: {unchangedCount}\n" +
                $"Failed updates: {failedCount}\n\n" +
                "Runtime mannequin avatar:\n" + AvatarSourceModelPath + "\n\n" +
                "This command uses Humanoid + Create From This Model on each FBX to avoid copied-avatar hierarchy mismatches.\n" +
                "Tip: Re-run clip extraction after this if you want fresh .anim assets from humanoid imports.",
                "OK");
        }

        [MenuItem("Tools/Zombera/Animation/Configure Complete Animation Pack Rig (Humanoid From UAL1)", true)]
        private static bool ValidateConfigureCompleteAnimationPackRigHumanoidFromUal1()
        {
            string completeRoot = SourceRoot + "/" + DestinationFolderName;
            return AssetDatabase.IsValidFolder(completeRoot)
                   && AssetDatabase.LoadMainAssetAtPath(AvatarSourceModelPath) != null;
        }

        [MenuItem("Tools/Zombera/Animation/Extract Complete Animation Clips (.anim)")]
        public static void ExtractCompleteAnimationClips()
        {
            string completeRoot = SourceRoot + "/" + DestinationFolderName;
            if (!AssetDatabase.IsValidFolder(completeRoot))
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "CompleteAnimationPack was not found. Run the build step first.\n\nExpected folder:\n" + completeRoot,
                    "OK");
                return;
            }

            string clipsRoot = completeRoot + "/" + ExtractedClipFolderName;
            EnsureFolder(clipsRoot);
            EnsureCategoryFolders(clipsRoot, out int createdCategoryFolderCount);

            List<string> fbxAssets = GetAllFbxAssetsRecursive(completeRoot);
            int extractedCount = 0;
            int skippedCount = 0;
            int failedCount = 0;

            for (int i = 0; i < fbxAssets.Count; i++)
            {
                string fbxAssetPath = fbxAssets[i];
                if (fbxAssetPath.IndexOf("/" + ExtractedClipFolderName + "/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                string fbxBaseName = NormalizeAnimationName(Path.GetFileNameWithoutExtension(fbxAssetPath));
                if (string.IsNullOrEmpty(fbxBaseName))
                {
                    fbxBaseName = "Animation";
                }

                List<AnimationClip> sourceClips = LoadModelAnimationClips(fbxAssetPath);
                if (sourceClips.Count == 0)
                {
                    continue;
                }

                bool hasSingleClip = sourceClips.Count == 1;
                for (int clipIndex = 0; clipIndex < sourceClips.Count; clipIndex++)
                {
                    AnimationClip sourceClip = sourceClips[clipIndex];
                    if (sourceClip == null)
                    {
                        continue;
                    }

                    string clipName = NormalizeAnimationName(sourceClip.name);
                    if (string.IsNullOrEmpty(clipName))
                    {
                        clipName = "Clip";
                    }

                    string targetBaseName = hasSingleClip ? fbxBaseName : (fbxBaseName + "__" + clipName);
                    string category = ResolveAnimationCategory(targetBaseName);
                    string categoryFolderPath = clipsRoot + "/" + category;
                    EnsureFolder(categoryFolderPath);

                    string targetAssetPath = categoryFolderPath + "/" + targetBaseName + ".anim";
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(targetAssetPath) != null)
                    {
                        skippedCount++;
                        continue;
                    }

                    AnimationClip extractedClip = new AnimationClip
                    {
                        name = targetBaseName
                    };

                    try
                    {
                        EditorUtility.CopySerialized(sourceClip, extractedClip);
                        extractedClip.name = targetBaseName;
                        AssetDatabase.CreateAsset(extractedClip, targetAssetPath);
                        extractedCount++;
                    }
                    catch (Exception exception)
                    {
                        failedCount++;
                        UnityEngine.Object.DestroyImmediate(extractedClip);
                        Debug.LogWarning(
                            $"[CompleteAnimationPackBuilderTool] Failed to extract clip '{sourceClip.name}' from '{fbxAssetPath}' to '{targetAssetPath}'. Error: {exception.Message}");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Complete Animation Pack",
                "Finished extracting .anim clips from CompleteAnimationPack.\n\n" +
                $"Scanned FBX: {fbxAssets.Count}\n" +
                $"Extracted .anim: {extractedCount}\n" +
                $"Skipped existing: {skippedCount}\n" +
                $"Category folders created: {createdCategoryFolderCount}\n" +
                $"Failed extractions: {failedCount}\n\n" +
                "Output root:\n" + clipsRoot,
                "OK");
        }

        [MenuItem("Tools/Zombera/Animation/Rebuild Extracted Animation Clips (.anim)")]
        public static void RebuildExtractedAnimationClips()
        {
            string completeRoot = SourceRoot + "/" + DestinationFolderName;
            if (!AssetDatabase.IsValidFolder(completeRoot))
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "CompleteAnimationPack was not found. Run the build step first.\n\nExpected folder:\n" + completeRoot,
                    "OK");
                return;
            }

            string clipsRoot = completeRoot + "/" + ExtractedClipFolderName;
            if (AssetDatabase.IsValidFolder(clipsRoot))
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "This will delete and rebuild all extracted .anim clips under:\n\n" + clipsRoot,
                    "Rebuild",
                    "Cancel");

                if (!confirm)
                {
                    return;
                }

                AssetDatabase.DeleteAsset(clipsRoot);
                AssetDatabase.Refresh();
            }

            ExtractCompleteAnimationClips();
        }

        [MenuItem("Tools/Zombera/Animation/Rebuild Extracted Animation Clips (.anim)", true)]
        private static bool ValidateRebuildExtractedAnimationClips()
        {
            string completeRoot = SourceRoot + "/" + DestinationFolderName;
            return AssetDatabase.IsValidFolder(completeRoot);
        }

        [MenuItem("Tools/Zombera/Animation/Validate Extracted Clips (Humanoid)")]
        public static void ValidateExtractedClipsHumanoid()
        {
            string extractedRoot = SourceRoot + "/" + DestinationFolderName + "/" + ExtractedClipFolderName;
            if (!AssetDatabase.IsValidFolder(extractedRoot))
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "ExtractedClips was not found. Run clip extraction first.\n\nExpected folder:\n" + extractedRoot,
                    "OK");
                return;
            }

            List<string> animAssets = GetAllAnimAssetsRecursive(extractedRoot);
            int humanoidCount = 0;
            int nonHumanoidCount = 0;

            var nonHumanoidSamples = new List<string>(16);
            for (int i = 0; i < animAssets.Count; i++)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animAssets[i]);
                if (clip == null)
                {
                    continue;
                }

                if (clip.humanMotion)
                {
                    humanoidCount++;
                }
                else
                {
                    nonHumanoidCount++;
                    if (nonHumanoidSamples.Count < 16)
                    {
                        nonHumanoidSamples.Add(animAssets[i]);
                    }
                }
            }

            if (nonHumanoidSamples.Count > 0)
            {
                for (int i = 0; i < nonHumanoidSamples.Count; i++)
                {
                    Debug.LogWarning("[CompleteAnimationPackBuilderTool] Non-humanoid extracted clip: " + nonHumanoidSamples[i]);
                }
            }

            EditorUtility.DisplayDialog(
                "Complete Animation Pack",
                "Finished validating extracted .anim clips.\n\n" +
                $"Scanned .anim: {animAssets.Count}\n" +
                $"Humanoid clips: {humanoidCount}\n" +
                $"Non-humanoid clips: {nonHumanoidCount}\n\n" +
                "If non-humanoid clips are present, run rig configuration first and then rebuild extracted clips.",
                "OK");
        }

        [MenuItem("Tools/Zombera/Animation/Validate Extracted Clips (Humanoid)", true)]
        private static bool ValidateValidateExtractedClipsHumanoid()
        {
            string extractedRoot = SourceRoot + "/" + DestinationFolderName + "/" + ExtractedClipFolderName;
            return AssetDatabase.IsValidFolder(extractedRoot);
        }

        [MenuItem("Tools/Zombera/Animation/Preview Selected .anim On UAL1 Mannequin")]
        public static void PreviewSelectedAnimOnUal1Mannequin()
        {
            AnimationClip selectedClip = Selection.activeObject as AnimationClip;
            if (selectedClip == null)
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "Select a .anim clip first and run this command again.",
                    "OK");
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(AvatarSourceModelPath) == null)
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "UAL1 model was not found:\n" + AvatarSourceModelPath,
                    "OK");
                return;
            }

            if (!EnsureHumanoidSourceAvatarReady(AvatarSourceModelPath))
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "Could not prepare UAL1 as a valid Humanoid avatar source. Check Console for details.",
                    "OK");
                return;
            }

            Avatar avatar = LoadPrimaryAvatarFromModel(AvatarSourceModelPath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "UAL1 does not currently provide a valid humanoid avatar.",
                    "OK");
                return;
            }

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarSourceModelPath);
            if (modelPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "Could not load UAL1 model prefab from:\n" + AvatarSourceModelPath,
                    "OK");
                return;
            }

            string controllerPath = EnsurePreviewControllerWithClip(selectedClip);
            AnimatorController previewController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (previewController == null)
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "Could not create preview controller at:\n" + controllerPath,
                    "OK");
                return;
            }

            GameObject existingPreview = GameObject.Find(PreviewObjectName);
            if (existingPreview != null)
            {
                UnityEngine.Object.DestroyImmediate(existingPreview);
            }

            GameObject previewObject = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            if (previewObject == null)
            {
                previewObject = UnityEngine.Object.Instantiate(modelPrefab);
            }

            previewObject.name = PreviewObjectName;
            previewObject.transform.position = Vector3.zero;
            previewObject.transform.rotation = Quaternion.identity;
            previewObject.transform.localScale = Vector3.one;

            Animator animator = previewObject.GetComponent<Animator>();
            if (animator == null)
            {
                animator = previewObject.AddComponent<Animator>();
            }

            animator.avatar = avatar;
            animator.runtimeAnimatorController = previewController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Selection.activeGameObject = previewObject;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            EditorApplication.ExecuteMenuItem("Window/Animation/Animation");

            EditorUtility.DisplayDialog(
                "Complete Animation Pack",
                "Preview mannequin created in scene with UAL1 and selected clip.\n\n" +
                "Clip humanoid: " + (selectedClip.humanMotion ? "Yes" : "No") + "\n" +
                "Scene object: " + PreviewObjectName,
                "OK");
        }

        [MenuItem("Tools/Zombera/Animation/Preview Selected .anim On UAL1 Mannequin", true)]
        private static bool ValidatePreviewSelectedAnimOnUal1Mannequin()
        {
            if (!(Selection.activeObject is AnimationClip clip))
            {
                return false;
            }

            string clipPath = AssetDatabase.GetAssetPath(clip);
            return !string.IsNullOrWhiteSpace(clipPath)
                   && clipPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)
                   && AssetDatabase.LoadMainAssetAtPath(AvatarSourceModelPath) != null;
        }

        [MenuItem("Tools/Zombera/Animation/Delete Complete Pack Source FBX (Keep Extracted .anim)")]
        public static void DeleteCompletePackSourceFbxKeepExtractedClips()
        {
            string completeRoot = SourceRoot + "/" + DestinationFolderName;
            string extractedRoot = completeRoot + "/" + ExtractedClipFolderName;

            if (!AssetDatabase.IsValidFolder(completeRoot))
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "CompleteAnimationPack was not found.\n\nExpected folder:\n" + completeRoot,
                    "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(extractedRoot))
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "ExtractedClips folder was not found. Aborting to avoid data loss.\n\nExpected folder:\n" + extractedRoot,
                    "OK");
                return;
            }

            List<string> allFbx = GetAllFbxAssetsRecursive(completeRoot);
            var deleteCandidates = new List<string>(allFbx.Count);
            for (int i = 0; i < allFbx.Count; i++)
            {
                string path = allFbx[i];
                if (path.IndexOf("/" + ExtractedClipFolderName + "/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                deleteCandidates.Add(path);
            }

            if (deleteCandidates.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Complete Animation Pack",
                    "No source FBX files found to delete.",
                    "OK");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Complete Animation Pack",
                "Delete source FBX files and keep extracted .anim clips?\n\n" +
                $"FBX to delete: {deleteCandidates.Count}\n" +
                "Folder: " + completeRoot,
                "Delete FBX",
                "Cancel");

            if (!confirm)
            {
                return;
            }

            int deletedCount = 0;
            int failedCount = 0;

            for (int i = 0; i < deleteCandidates.Count; i++)
            {
                if (AssetDatabase.DeleteAsset(deleteCandidates[i]))
                {
                    deletedCount++;
                }
                else
                {
                    failedCount++;
                    Debug.LogWarning("[CompleteAnimationPackBuilderTool] Failed to delete FBX: " + deleteCandidates[i]);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Complete Animation Pack",
                "Finished deleting source FBX files.\n\n" +
                $"Deleted FBX: {deletedCount}\n" +
                $"Failed deletions: {failedCount}",
                "OK");
        }

        [MenuItem("Tools/Zombera/Animation/Delete Complete Pack Source FBX (Keep Extracted .anim)", true)]
        private static bool ValidateDeleteCompletePackSourceFbxKeepExtractedClips()
        {
            string completeRoot = SourceRoot + "/" + DestinationFolderName;
            string extractedRoot = completeRoot + "/" + ExtractedClipFolderName;
            return AssetDatabase.IsValidFolder(completeRoot) && AssetDatabase.IsValidFolder(extractedRoot);
        }

        [MenuItem("Tools/Zombera/Animation/Extract Complete Animation Clips (.anim)", true)]
        private static bool ValidateExtractCompleteAnimationClips()
        {
            string completeRoot = SourceRoot + "/" + DestinationFolderName;
            return AssetDatabase.IsValidFolder(completeRoot);
        }

        private static List<PackDirectoryInfo> GatherSourcePacks()
        {
            var packs = new List<PackDirectoryInfo>(35);
            string absoluteSourceRoot = ToAbsolutePath(SourceRoot);

            if (!Directory.Exists(absoluteSourceRoot))
            {
                return packs;
            }

            string[] directories = Directory.GetDirectories(absoluteSourceRoot, "AnimationPack*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < directories.Length; i++)
            {
                string absoluteDir = directories[i];
                string dirName = Path.GetFileName(absoluteDir);
                if (!TryParsePackNumber(dirName, out int packNumber))
                {
                    continue;
                }

                if (packNumber < 1 || packNumber > 35)
                {
                    continue;
                }

                string assetPath = ToAssetPath(absoluteDir);
                if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
                {
                    continue;
                }

                packs.Add(new PackDirectoryInfo(packNumber, assetPath));
            }

            packs.Sort((a, b) => a.Number.CompareTo(b.Number));
            return packs;
        }

        private static List<string> GetTopLevelFbxAssets(string packAssetPath)
        {
            var results = new List<string>(64);
            string absolutePackPath = ToAbsolutePath(packAssetPath);

            if (!Directory.Exists(absolutePackPath))
            {
                return results;
            }

            string[] files = Directory.GetFiles(absolutePackPath, "*.fbx", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string assetPath = ToAssetPath(files[i]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                results.Add(assetPath);
            }

            return results;
        }

        private static List<string> GetAllFbxAssetsRecursive(string rootAssetPath)
        {
            var results = new List<string>(2048);
            string absoluteRootPath = ToAbsolutePath(rootAssetPath);
            if (!Directory.Exists(absoluteRootPath))
            {
                return results;
            }

            string[] files = Directory.GetFiles(absoluteRootPath, "*.fbx", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string assetPath = ToAssetPath(files[i]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                results.Add(assetPath);
            }

            return results;
        }

        private static List<AnimationClip> LoadModelAnimationClips(string modelAssetPath)
        {
            var clips = new List<AnimationClip>(4);
            if (string.IsNullOrWhiteSpace(modelAssetPath))
            {
                return clips;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelAssetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is AnimationClip clip))
                {
                    continue;
                }

                if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                clips.Add(clip);
            }

            clips.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return clips;
        }

        private static List<string> GetAllAnimAssetsRecursive(string rootAssetPath)
        {
            var results = new List<string>(2048);
            string absoluteRootPath = ToAbsolutePath(rootAssetPath);
            if (!Directory.Exists(absoluteRootPath))
            {
                return results;
            }

            string[] files = Directory.GetFiles(absoluteRootPath, "*.anim", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string assetPath = ToAssetPath(files[i]);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                results.Add(assetPath);
            }

            return results;
        }

        private static string EnsurePreviewControllerWithClip(AnimationClip clip)
        {
            string previewRoot = SourceRoot + "/" + DestinationFolderName + "/" + ExtractedClipFolderName + "/" + PreviewFolderName;
            EnsureFolder(previewRoot);

            string controllerPath = previewRoot + "/" + PreviewControllerName;
            if (AssetDatabase.LoadMainAssetAtPath(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return controllerPath;
        }

        private static bool EnsureHumanoidSourceAvatarReady(string sourceModelAssetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(sourceModelAssetPath) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            bool changed = false;

            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                changed = true;
            }

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            if (!changed)
            {
                return true;
            }

            try
            {
                importer.SaveAndReimport();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[CompleteAnimationPackBuilderTool] Failed to configure source avatar model '{sourceModelAssetPath}'. Error: {exception.Message}");
                return false;
            }
        }

        private static Avatar LoadPrimaryAvatarFromModel(string modelAssetPath)
        {
            if (string.IsNullOrWhiteSpace(modelAssetPath))
            {
                return null;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelAssetPath);
            Avatar fallback = null;

            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is Avatar avatar))
                {
                    continue;
                }

                if (avatar.isValid && avatar.isHuman)
                {
                    return avatar;
                }

                if (fallback == null)
                {
                    fallback = avatar;
                }
            }

            return fallback;
        }

        private static bool HasValidHumanoidAvatar(string modelAssetPath)
        {
            Avatar avatar = LoadPrimaryAvatarFromModel(modelAssetPath);
            return avatar != null && avatar.isValid && avatar.isHuman;
        }

        private static bool TryParsePackNumber(string folderName, out int packNumber)
        {
            packNumber = 0;
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return false;
            }

            const string prefix = "AnimationPack";
            if (!folderName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string numberPart = folderName.Substring(prefix.Length);
            return int.TryParse(numberPart, out packNumber);
        }

        private static string BuildUniqueNameForPack(
            string normalizedBaseName,
            int packNumber,
            Dictionary<string, int> seenNormalizedNames)
        {
            if (!seenNormalizedNames.TryGetValue(normalizedBaseName, out int existingCount))
            {
                seenNormalizedNames[normalizedBaseName] = 1;
                return normalizedBaseName;
            }

            seenNormalizedNames[normalizedBaseName] = existingCount + 1;
            return normalizedBaseName + "_Pack" + packNumber.ToString("00");
        }

        private static void EnsureCategoryFolders(string completeRootAssetPath, out int createdFolderCount)
        {
            createdFolderCount = 0;
            for (int i = 0; i < FlattenCategories.Length; i++)
            {
                string categoryFolder = completeRootAssetPath + "/" + FlattenCategories[i];
                if (EnsureFolder(categoryFolder))
                {
                    createdFolderCount++;
                }
            }
        }

        private static bool IsFbxAlreadyFlatCategorized(string fbxAssetPath, string completeRootAssetPath)
        {
            if (string.IsNullOrWhiteSpace(fbxAssetPath) || string.IsNullOrWhiteSpace(completeRootAssetPath))
            {
                return false;
            }

            string normalizedAsset = fbxAssetPath.Replace("\\", "/");
            string normalizedRoot = completeRootAssetPath.Replace("\\", "/").TrimEnd('/');

            if (!normalizedAsset.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relative = normalizedAsset.Substring(normalizedRoot.Length + 1);
            string[] parts = relative.Split('/');
            if (parts.Length != 2)
            {
                return false;
            }

            return IsCategoryFolder(parts[0]);
        }

        private static bool IsCategoryFolder(string folderName)
        {
            for (int i = 0; i < FlattenCategories.Length; i++)
            {
                if (string.Equals(folderName, FlattenCategories[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveAnimationCategory(string normalizedAnimationName)
        {
            string key = normalizedAnimationName.ToLowerInvariant();

            if (ContainsAny(key, "death", "dead", "die", "fatal", "knockdown", "knockout"))
            {
                return "Death";
            }

            if (ContainsAny(key, "reaction", "react", "hit", "impact", "stagger", "flinch", "behit"))
            {
                return "Hit Reactions";
            }

            if (ContainsAny(key, "dodge", "evade", "roll"))
            {
                return "Dodge";
            }

            if (ContainsAny(key, "archery", "bow", "arrow", "shoot", "shot", "aim"))
            {
                return "Bow";
            }

            if (ContainsAny(key, "crouch", "squat"))
            {
                return "Crouch";
            }

            if (ContainsAny(key, "crawl"))
            {
                return "Crawl";
            }

            if (ContainsAny(key, "sprint"))
            {
                return "Sprint";
            }

            if (ContainsAny(key, "jog", "run"))
            {
                return "Jog";
            }

            if (ContainsAny(key, "walk", "stride"))
            {
                return "Walk";
            }

            if (ContainsAny(key, "attack", "kick", "punch", "slash", "strike", "combat", "fight", "axe", "sword"))
            {
                return "Combat";
            }

            if (ContainsAny(key, "idle", "stance", "breathe", "look_around", "lookaround", "wait"))
            {
                return "Idle";
            }

            return DefaultAnimationCategory;
        }

        private static bool ContainsAny(string source, params string[] tokens)
        {
            if (string.IsNullOrEmpty(source) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                if (source.Contains(token))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildUniqueAssetPath(string folderAssetPath, string baseName, string extension)
        {
            string safeBaseName = string.IsNullOrWhiteSpace(baseName) ? "Animation" : baseName;
            string candidate = folderAssetPath + "/" + safeBaseName + extension;
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(candidate) == null)
            {
                return candidate;
            }

            int suffix = 2;
            while (true)
            {
                string numbered = folderAssetPath + "/" + safeBaseName + "_" + suffix.ToString("00") + extension;
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(numbered) == null)
                {
                    return numbered;
                }

                suffix++;
            }
        }

        private static int RemoveEmptyNonCategoryFolders(string completeRootAssetPath)
        {
            int removedCount = 0;
            string absoluteRootPath = ToAbsolutePath(completeRootAssetPath);
            if (!Directory.Exists(absoluteRootPath))
            {
                return removedCount;
            }

            string[] directories = Directory.GetDirectories(absoluteRootPath, "*", SearchOption.AllDirectories);
            Array.Sort(directories, (a, b) => b.Length.CompareTo(a.Length));

            for (int i = 0; i < directories.Length; i++)
            {
                string absoluteDirectory = directories[i];
                string assetPath = ToAssetPath(absoluteDirectory);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                string normalizedRoot = completeRootAssetPath.Replace("\\", "/").TrimEnd('/');
                string normalizedAsset = assetPath.Replace("\\", "/");
                if (!normalizedAsset.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative = normalizedAsset.Substring(normalizedRoot.Length + 1);
                string[] parts = relative.Split('/');
                if (parts.Length == 1 && IsCategoryFolder(parts[0]))
                {
                    continue;
                }

                if (!IsDirectoryEmpty(absoluteDirectory))
                {
                    continue;
                }

                if (AssetDatabase.DeleteAsset(assetPath))
                {
                    removedCount++;
                }
            }

            return removedCount;
        }

        private static bool IsDirectoryEmpty(string absoluteDirectory)
        {
            if (string.IsNullOrWhiteSpace(absoluteDirectory) || !Directory.Exists(absoluteDirectory))
            {
                return true;
            }

            string[] entries = Directory.GetFileSystemEntries(absoluteDirectory);
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i];
                if (entry.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static string NormalizeAnimationName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            string name = rawName.Trim();

            if (name.StartsWith(VendorPrefix, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(VendorPrefix.Length);
            }

            if (name.EndsWith(VendorSuffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - VendorSuffix.Length);
            }

            name = name.Trim();

            var sanitized = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char character = name[i];
                if (char.IsLetterOrDigit(character) || character == '_' || character == '-')
                {
                    sanitized.Append(character);
                }
                else if (char.IsWhiteSpace(character))
                {
                    sanitized.Append('_');
                }
            }

            return CollapseUnderscores(sanitized.ToString()).Trim('_', '-', ' ');
        }

        private static string CollapseUnderscores(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            bool previousUnderscore = false;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool isUnderscore = character == '_';

                if (isUnderscore && previousUnderscore)
                {
                    continue;
                }

                builder.Append(character);
                previousUnderscore = isUnderscore;
            }

            return builder.ToString();
        }

        private static bool EnsureFolder(string folderAssetPath)
        {
            if (AssetDatabase.IsValidFolder(folderAssetPath))
            {
                return false;
            }

            string normalizedPath = folderAssetPath.Replace("\\", "/");
            int slashIndex = normalizedPath.LastIndexOf('/');
            if (slashIndex <= 0)
            {
                return false;
            }

            string parentPath = normalizedPath.Substring(0, slashIndex);
            string folderName = normalizedPath.Substring(slashIndex + 1);

            EnsureFolder(parentPath);

            if (!AssetDatabase.IsValidFolder(parentPath))
            {
                return false;
            }

            AssetDatabase.CreateFolder(parentPath, folderName);
            return true;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            string normalizedAssetPath = assetPath.Replace("\\", "/");
            if (!normalizedAssetPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string relative = normalizedAssetPath.Length > 6
                ? normalizedAssetPath.Substring("Assets/".Length)
                : string.Empty;

            return Path.GetFullPath(Path.Combine(Application.dataPath, relative));
        }

        private static string ToAssetPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            string fullPath = Path.GetFullPath(absolutePath).Replace("\\", "/");
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace("\\", "/").TrimEnd('/');

            if (!fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string relative = fullPath.Substring(projectRoot.Length + 1);
            return relative.Replace("\\", "/");
        }

        private readonly struct PackDirectoryInfo
        {
            public readonly int Number;
            public readonly string AssetPath;

            public PackDirectoryInfo(int number, string assetPath)
            {
                Number = number;
                AssetPath = assetPath;
            }
        }
    }
}