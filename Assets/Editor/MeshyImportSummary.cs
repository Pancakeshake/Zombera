#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

namespace Zombera.Editor
{
    internal sealed class MeshyImportSummary
    {
        public string DestinationRoot = string.Empty;
        public string DestinationSubfolder = string.Empty;
        public string ResolvedDestination = string.Empty;

        public int SourceFoldersScanned;
        public int SourceFoldersProcessed;
        public int SourceFoldersSucceeded;
        public int SourceFoldersSkipped;
        public int SourceFoldersFailed;

        public int ModelsScanned;
        public int TextureExtractOperations;
        public int MaterialExtractOperations;
        public int ExtractedTexturesFromFallback;
        public int ExtractedMaterialsFromFallback;

        public int LegacyTexturesDeleted;
        public int LegacyMaterialsDeleted;
        public int MaterialsRewired;
        public int NormalImportersFixed;

        public int RenamedModels;
        public int RenamedFolders;
        public int RenamedTextures;
        public int RenamedMaterials;
        public int MovedFolders;

        public readonly List<string> Failures = new();

        public readonly Dictionary<string, List<string>> StageFailures = new(StringComparer.OrdinalIgnoreCase);

        public void AddStageFailure(string sourceFolder, string stage, string error)
        {
            var stageLabel = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage;
            var reason = string.IsNullOrWhiteSpace(error) ? "Stage failed." : error;
            Failures.Add(sourceFolder + " [stage=" + stageLabel + "] -> " + reason);

            if (!StageFailures.TryGetValue(stageLabel, out var errors))
            {
                errors = new List<string>();
                StageFailures[stageLabel] = errors;
            }

            var entry = sourceFolder + ": " + reason;
            if (!errors.Contains(entry))
                errors.Add(entry);
        }

        public string ToSummaryText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Meshy import pipeline complete.");
            sb.AppendLine();

            sb.AppendLine("Source root: " + MeshyImportPipelineRunner.ImportRoot);
            sb.AppendLine("Destination root: " + DestinationRoot);
            sb.AppendLine("Destination subfolder: " +
                          (string.IsNullOrWhiteSpace(DestinationSubfolder) ? "<root>" : DestinationSubfolder));
            sb.AppendLine("Resolved destination: " + ResolvedDestination);
            sb.AppendLine();

            sb.AppendLine("Source folders scanned: " + SourceFoldersScanned);
            sb.AppendLine("Source folders processed: " + SourceFoldersProcessed);
            sb.AppendLine("Source folders succeeded: " + SourceFoldersSucceeded);
            sb.AppendLine("Source folders skipped: " + SourceFoldersSkipped);
            sb.AppendLine("Source folders failed: " + SourceFoldersFailed);
            sb.AppendLine();

            sb.AppendLine("Models scanned: " + ModelsScanned);
            sb.AppendLine("Texture extraction operations: " + TextureExtractOperations);
            sb.AppendLine("Material extraction operations: " + MaterialExtractOperations);
            sb.AppendLine("Fallback extracted textures: " + ExtractedTexturesFromFallback);
            sb.AppendLine("Fallback extracted materials: " + ExtractedMaterialsFromFallback);
            sb.AppendLine();

            sb.AppendLine("Legacy textures deleted: " + LegacyTexturesDeleted);
            sb.AppendLine("Legacy materials deleted: " + LegacyMaterialsDeleted);
            sb.AppendLine("Materials rewired: " + MaterialsRewired);
            sb.AppendLine("Normal importers fixed: " + NormalImportersFixed);
            sb.AppendLine();

            sb.AppendLine("Models renamed: " + RenamedModels);
            sb.AppendLine("Folders renamed: " + RenamedFolders);
            sb.AppendLine("Textures renamed: " + RenamedTextures);
            sb.AppendLine("Materials renamed: " + RenamedMaterials);
            sb.AppendLine("Folders moved to destination: " + MovedFolders);

            if (Failures.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Failures:");
                for (var i = 0; i < Failures.Count; i++)
                    sb.AppendLine("- " + Failures[i]);
            }

            return sb.ToString();
        }
    }
}
#endif
