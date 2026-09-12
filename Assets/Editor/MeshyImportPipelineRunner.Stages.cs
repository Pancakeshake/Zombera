#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    internal static partial class MeshyImportPipelineRunner
    {
        private static readonly IModelSubAssetExtractor ModelSubAssetExtractor =
            new ReflectionThenFallbackModelSubAssetExtractor();

        private static readonly IMaterialTexturePolicy MaterialTexturePolicy = new MeshyMaterialTexturePolicy();

        private static readonly string[] MaterialFileExtensions = { ".mat" };

        private static readonly string[] TextureFileExtensions =
        {
            ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".bmp", ".exr", ".psd", ".hdr"
        };

        private enum MeshyPipelineStage
        {
            ScaleModelImport,
            ExtractTextures,
            ExtractMaterials,
            DeleteLegacyAssets,
            RewireMaterialsAndNormals,
            NormalizeAndRename,
            MoveToDestination
        }

        private sealed class MeshyPipelineStageContext
        {
            internal readonly string SourceFolder;
            internal readonly string DestinationParent;
            internal string CurrentFolder;
            internal string ActiveStage;
            internal System.Collections.Generic.List<string> ModelPaths;
            internal System.Collections.Generic.List<string> OldTexturePaths;
            internal System.Collections.Generic.List<string> OldMaterialPaths;

            internal MeshyPipelineStageContext(string sourceFolder, string destinationParent)
            {
                SourceFolder = sourceFolder;
                DestinationParent = destinationParent;
                CurrentFolder = sourceFolder;
                ActiveStage = "initialize";
            }
        }

        private readonly struct MeshyPipelineStageResult
        {
            internal readonly bool Success;
            internal readonly bool IsSkipped;
            internal readonly string FailureReason;

            private MeshyPipelineStageResult(bool success, bool isSkipped, string failureReason)
            {
                Success = success;
                IsSkipped = isSkipped;
                FailureReason = failureReason;
            }

            internal static MeshyPipelineStageResult Ok()
            {
                return new MeshyPipelineStageResult(true, false, string.Empty);
            }

            internal static MeshyPipelineStageResult Fail(string reason)
            {
                return new MeshyPipelineStageResult(false, false, reason);
            }

            internal static MeshyPipelineStageResult Skip(string reason)
            {
                return new MeshyPipelineStageResult(false, true, reason);
            }
        }

        private static void ProcessSingleImportFolder(string sourceFolder, string destinationParent, MeshyImportSummary summary)
        {
            summary.SourceFoldersProcessed++;

            var context = new MeshyPipelineStageContext(sourceFolder, destinationParent);
            try
            {
                var initializeResult = InitializeStageContext(context);
                if (initializeResult.IsSkipped)
                {
                    summary.SourceFoldersSkipped++;
                    return;
                }
                if (!initializeResult.Success)
                    throw new InvalidOperationException(initializeResult.FailureReason);

                RunStage(context, summary, MeshyPipelineStage.ScaleModelImport, StageScaleModelImport);
                RunStage(context, summary, MeshyPipelineStage.ExtractTextures, StageExtractTextures);
                RunStage(context, summary, MeshyPipelineStage.ExtractMaterials, StageExtractMaterials);
                RunStage(context, summary, MeshyPipelineStage.DeleteLegacyAssets, StageDeleteLegacyAssets);
                RunStage(context, summary, MeshyPipelineStage.RewireMaterialsAndNormals,
                    StageRewireMaterialsAndNormals);
                RunStage(context, summary, MeshyPipelineStage.NormalizeAndRename, StageNormalizeAndRename);
                RunStage(context, summary, MeshyPipelineStage.MoveToDestination, StageMoveToDestination);

                summary.MovedFolders++;
                summary.SourceFoldersSucceeded++;
            }
            catch (Exception ex)
            {
                summary.SourceFoldersFailed++;
                summary.AddStageFailure(sourceFolder, context.ActiveStage, ex.Message);
            }
        }

        private static MeshyPipelineStageResult InitializeStageContext(MeshyPipelineStageContext context)
        {
            context.ModelPaths = MeshyAssetPathUtility.FindDirectModelPaths(context.CurrentFolder);
            if (context.ModelPaths.Count == 0)
                return MeshyPipelineStageResult.Skip("No supported model files found.");

            context.OldTexturePaths = MeshyAssetPathUtility.FindDirectAssetPaths(
                context.CurrentFolder,
                "t:Texture2D",
                TextureFileExtensions);
            context.OldMaterialPaths = MeshyAssetPathUtility.FindDirectAssetPaths(
                context.CurrentFolder,
                "t:Material",
                MaterialFileExtensions);

            return MeshyPipelineStageResult.Ok();
        }

        private static void RunStage(
            MeshyPipelineStageContext context,
            MeshyImportSummary summary,
            MeshyPipelineStage stage,
            Func<MeshyPipelineStageContext, MeshyImportSummary, MeshyPipelineStageResult> stageRunner)
        {
            context.ActiveStage = GetStageDisplayName(stage);
            var result = stageRunner(context, summary);
            if (result.Success) return;

            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.FailureReason)
                ? "Stage failed."
                : result.FailureReason);
        }

        private static string GetStageDisplayName(MeshyPipelineStage stage)
        {
            return stage switch
            {
                MeshyPipelineStage.ScaleModelImport => "scale-model-import",
                MeshyPipelineStage.ExtractTextures => "extract-textures",
                MeshyPipelineStage.ExtractMaterials => "extract-materials",
                MeshyPipelineStage.DeleteLegacyAssets => "delete-legacy-assets",
                MeshyPipelineStage.RewireMaterialsAndNormals => "rewire-materials-and-normals",
                MeshyPipelineStage.NormalizeAndRename => "normalize-and-rename",
                MeshyPipelineStage.MoveToDestination => "move-to-destination",
                _ => "unknown"
            };
        }

        private static MeshyPipelineStageResult StageScaleModelImport(
            MeshyPipelineStageContext context,
            MeshyImportSummary _)
        {
            for (var i = 0; i < context.ModelPaths.Count; i++)
                SetModelImportScaleToHundred(context.ModelPaths[i]);

            return MeshyPipelineStageResult.Ok();
        }

        private static MeshyPipelineStageResult StageExtractTextures(
            MeshyPipelineStageContext context,
            MeshyImportSummary summary)
        {
            for (var i = 0; i < context.ModelPaths.Count; i++)
            {
                var modelPath = context.ModelPaths[i];
                summary.ModelsScanned++;
                ModelSubAssetExtractor.ExtractTextures(modelPath, context.CurrentFolder, summary);
            }

            return MeshyPipelineStageResult.Ok();
        }

        private static MeshyPipelineStageResult StageExtractMaterials(
            MeshyPipelineStageContext context,
            MeshyImportSummary summary)
        {
            for (var i = 0; i < context.ModelPaths.Count; i++)
            {
                var modelPath = context.ModelPaths[i];
                ModelSubAssetExtractor.ExtractMaterials(modelPath, context.CurrentFolder, summary);
            }

            return MeshyPipelineStageResult.Ok();
        }

        private static MeshyPipelineStageResult StageDeleteLegacyAssets(
            MeshyPipelineStageContext context,
            MeshyImportSummary summary)
        {
            DeleteOldAssets(context.OldTexturePaths, summary, isTexture: true);
            DeleteOldAssets(context.OldMaterialPaths, summary, isTexture: false);
            CommitAssetDatabaseCheckpoint();
            return MeshyPipelineStageResult.Ok();
        }

        private static MeshyPipelineStageResult StageRewireMaterialsAndNormals(
            MeshyPipelineStageContext context,
            MeshyImportSummary summary)
        {
            MaterialTexturePolicy.WireTexturesToMaterialsAndFixNormals(context.CurrentFolder, summary);
            return MeshyPipelineStageResult.Ok();
        }

        private static MeshyPipelineStageResult StageNormalizeAndRename(
            MeshyPipelineStageContext context,
            MeshyImportSummary summary)
        {
            if (!NormalizeFolderAssets(context.CurrentFolder, summary, out var normalizedFolderPath, out var normalizeFailure))
            {
                return MeshyPipelineStageResult.Fail(string.IsNullOrWhiteSpace(normalizeFailure)
                    ? "Normalization failed."
                    : normalizeFailure);
            }

            context.CurrentFolder = normalizedFolderPath;
            return MeshyPipelineStageResult.Ok();
        }

        private static MeshyPipelineStageResult StageMoveToDestination(
            MeshyPipelineStageContext context,
            MeshyImportSummary _)
        {
            if (!MoveFolderToDestination(context.CurrentFolder, context.DestinationParent, out var movedFolderPath,
                    out var moveFailure))
            {
                return MeshyPipelineStageResult.Fail(string.IsNullOrWhiteSpace(moveFailure)
                    ? "Folder move failed."
                    : moveFailure);
            }

            context.CurrentFolder = movedFolderPath;
            return MeshyPipelineStageResult.Ok();
        }

        private static void SetModelImportScaleToHundred(string modelPath)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
                return;

            if (Mathf.Approximately(importer.globalScale, 100f))
                return;

            importer.globalScale = 100f;
            importer.SaveAndReimport();
        }

        private static void DeleteOldAssets(System.Collections.Generic.IReadOnlyList<string> paths, MeshyImportSummary summary, bool isTexture)
        {
            if (paths == null) return;

            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (string.IsNullOrWhiteSpace(path)) continue;
                if (AssetDatabase.LoadMainAssetAtPath(path) == null) continue;
                if (!ShouldDeleteLegacyAsset(path, isTexture)) continue;

                if (!AssetDatabase.DeleteAsset(path))
                    continue;

                if (isTexture) summary.LegacyTexturesDeleted++;
                else summary.LegacyMaterialsDeleted++;
            }
        }

        private static bool ShouldDeleteLegacyAsset(string assetPath, bool isTexture)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return false;

            var name = Path.GetFileNameWithoutExtension(assetPath) ?? string.Empty;
            var key = MeshyAssetPathUtility.NormalizeNameKey(name);
            if (string.IsNullOrWhiteSpace(key)) return false;

            if (isTexture)
            {
                if (key == "image0" || key == "image3" || key == "normal") return false;
                if (key == "basemap" || key == "emissionmap" || key == "metallicmap") return false;
                if (key.Contains("roughness", StringComparison.Ordinal)) return false;

                if (key.Contains("meshyai", StringComparison.Ordinal) && key.Contains("texture", StringComparison.Ordinal))
                    return true;

                if (key.Contains("texture", StringComparison.Ordinal) &&
                    !key.Contains("normal", StringComparison.Ordinal) &&
                    !key.Contains("image0", StringComparison.Ordinal) &&
                    !key.Contains("image3", StringComparison.Ordinal))
                    return true;

                return false;
            }

            return true;
        }

        private static bool NormalizeFolderAssets(
            string folderPath,
            MeshyImportSummary summary,
            out string updatedFolderPath,
            out string failureReason)
        {
            updatedFolderPath = folderPath;
            failureReason = string.Empty;

            var modelPaths = MeshyAssetPathUtility.FindDirectModelPaths(folderPath);
            if (modelPaths.Count == 0)
                return true;

            modelPaths.Sort(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < modelPaths.Count; i++)
            {
                var modelPath = modelPaths[i];
                var sourceFileName = Path.GetFileNameWithoutExtension(modelPath);
                var strippedName = MeshyAssetPathUtility.StripMeshyPrefix(sourceFileName);

                if (string.IsNullOrWhiteSpace(strippedName) ||
                    string.Equals(strippedName, sourceFileName, StringComparison.Ordinal))
                    continue;

                var renameError = AssetDatabase.RenameAsset(modelPath, strippedName);
                if (!string.IsNullOrWhiteSpace(renameError))
                {
                    failureReason = "Model rename failed: " + renameError;
                    return false;
                }

                summary.RenamedModels++;
            }

            modelPaths = MeshyAssetPathUtility.FindDirectModelPaths(folderPath);
            for (var i = 0; i < modelPaths.Count; i++)
            {
                var modelPath = modelPaths[i];
                var modelSubfolder = folderPath + "/Model";
                MeshyAssetPathUtility.EnsureFolderHierarchy(modelSubfolder);
                var targetPath = modelSubfolder + "/" + Path.GetFileName(modelPath);
                if (MeshyAssetPathUtility.TryMoveAssetIfNeeded(modelPath, targetPath, out _))
                {
                    // moved
                }
            }

            modelPaths = MeshyAssetPathUtility.FindDirectAssetPaths(folderPath, "t:GameObject", ".fbx", ".obj", ".dae", ".blend");
            if (modelPaths.Count == 0)
                return true;

            modelPaths.Sort(StringComparer.OrdinalIgnoreCase);
            var primaryModelPath = modelPaths[0];
            var cleanedName = MeshyAssetPathUtility.StripMeshyPrefix(Path.GetFileNameWithoutExtension(primaryModelPath));

            if (!MeshyAssetPathUtility.TryRenameModelFolder(
                    primaryModelPath,
                    cleanedName,
                    ImportRoot,
                    out var renamedModelPath,
                    out var folderRenamed,
                    out var folderRenameFailure))
            {
                failureReason = folderRenameFailure;
                return false;
            }

            if (folderRenamed)
                summary.RenamedFolders++;

            primaryModelPath = renamedModelPath;
            var normalizedFolder = Path.GetDirectoryName(primaryModelPath)?.Replace('\\', '/');
            if (normalizedFolder != null && normalizedFolder.EndsWith("/Model", StringComparison.OrdinalIgnoreCase))
                normalizedFolder = Path.GetDirectoryName(normalizedFolder)?.Replace('\\', '/');

            if (!string.IsNullOrWhiteSpace(normalizedFolder))
                updatedFolderPath = normalizedFolder;

            NormalizeTexturesInFolder(updatedFolderPath, summary);
            RenameMaterialAssetsForModel(primaryModelPath, cleanedName, summary);

            return true;
        }

        private static void NormalizeTexturesInFolder(string folderPath, MeshyImportSummary summary)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return;

            var materialFolderPath = folderPath + "/Material";

            var texturePaths = MeshyAssetPathUtility.FindDirectAssetPaths(folderPath, "t:Texture2D", TextureFileExtensions);
            for (var i = 0; i < texturePaths.Count; i++)
            {
                var texturePath = texturePaths[i];
                var nameNoExt = Path.GetFileNameWithoutExtension(texturePath);
                var ext = Path.GetExtension(texturePath);
                if (string.IsNullOrWhiteSpace(nameNoExt) || string.IsNullOrWhiteSpace(ext))
                    continue;

                MeshyAssetPathUtility.EnsureFolderHierarchy(materialFolderPath);
                var targetPath = materialFolderPath + "/" + Path.GetFileName(texturePath);
                if (MeshyAssetPathUtility.TryMoveAssetIfNeeded(texturePath, targetPath, out _))
                    texturePath = targetPath;

                if (string.Equals(nameNoExt, "Image_3", StringComparison.OrdinalIgnoreCase))
                {
                    if (MeshyAssetPathUtility.TryRenameAssetIfNeeded(texturePath, "Emission_Map", out _))
                        summary.RenamedTextures++;
                    continue;
                }

                if (string.Equals(nameNoExt, "Image_0", StringComparison.OrdinalIgnoreCase))
                {
                    if (MeshyAssetPathUtility.TryRenameAssetIfNeeded(texturePath, "Base_Map", out _))
                        summary.RenamedTextures++;
                    continue;
                }

                if (nameNoExt.IndexOf("roughness", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (MeshyAssetPathUtility.TryRenameAssetIfNeeded(texturePath, "Metallic_Map", out _))
                    summary.RenamedTextures++;
            }
        }

        private static void RenameMaterialAssetsForModel(string modelPath, string strippedName, MeshyImportSummary summary)
        {
            if (string.IsNullOrWhiteSpace(modelPath)) return;

            var baseName = string.IsNullOrWhiteSpace(strippedName)
                ? MeshyAssetPathUtility.StripMeshyPrefix(Path.GetFileNameWithoutExtension(modelPath))
                : strippedName;

            if (string.IsNullOrWhiteSpace(baseName))
                baseName = Path.GetFileNameWithoutExtension(modelPath);

            var targetMaterialName = baseName + "_material";
            var folderPath = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');

            if (!string.IsNullOrWhiteSpace(folderPath) && AssetDatabase.IsValidFolder(folderPath))
            {
                var materialFolderPath = folderPath + "/Material";
                var materialPaths = MeshyAssetPathUtility.FindDirectAssetPaths(folderPath, "t:Material", MaterialFileExtensions);
                var renameIndex = 0;

                for (var i = 0; i < materialPaths.Count; i++)
                {
                    var materialPath = materialPaths[i];

                    MeshyAssetPathUtility.EnsureFolderHierarchy(materialFolderPath);
                    var targetPath = materialFolderPath + "/" + Path.GetFileName(materialPath);
                    if (MeshyAssetPathUtility.TryMoveAssetIfNeeded(materialPath, targetPath, out _))
                        materialPath = targetPath;

                    var expectedName = renameIndex == 0 ? targetMaterialName : targetMaterialName + "_" + renameIndex;
                    if (MeshyAssetPathUtility.TryRenameAssetIfNeeded(materialPath, expectedName, out _))
                        summary.RenamedMaterials++;

                    renameIndex++;
                }
            }

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            for (var i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is not Material material) continue;
                if (string.Equals(material.name, targetMaterialName, StringComparison.Ordinal)) continue;

                material.name = targetMaterialName;
                EditorUtility.SetDirty(material);
                summary.RenamedMaterials++;
            }
        }

        private static bool MoveFolderToDestination(
            string sourceFolder,
            string destinationParent,
            out string movedFolderPath,
            out string failureReason)
        {
            movedFolderPath = sourceFolder;
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(sourceFolder) || string.IsNullOrWhiteSpace(destinationParent))
            {
                failureReason = "Source or destination path was empty.";
                return false;
            }

            if (!AssetDatabase.IsValidFolder(sourceFolder))
            {
                failureReason = "Source folder does not exist: " + sourceFolder;
                return false;
            }

            MeshyAssetPathUtility.EnsureFolderHierarchy(destinationParent);

            var folderName = Path.GetFileName(sourceFolder.TrimEnd('/'));
            if (string.IsNullOrWhiteSpace(folderName))
            {
                failureReason = "Could not resolve source folder name.";
                return false;
            }

            var destinationFolder = MeshyAssetPathUtility.BuildUniqueFolderPath(destinationParent, folderName);
            var moveError = AssetDatabase.MoveAsset(sourceFolder, destinationFolder);
            if (!string.IsNullOrWhiteSpace(moveError))
            {
                failureReason = "MoveAsset failed: " + moveError;
                return false;
            }

            movedFolderPath = destinationFolder;
            return true;
        }

        private static void CommitAssetDatabaseCheckpoint()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
