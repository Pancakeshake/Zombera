#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Zombera.Editor
{
    public static partial class WeaponCleanPrefabAndWrapperBatchTool
    {
        private sealed class BatchResult
        {
            public int Scanned;
            public int Created;
            public int Updated;
            public int Skipped;
            public int Failed;
            public readonly List<string> Failures = new List<string>();
        }

        private sealed class SourceNormalizationResult
        {
            public int ScannedModelImporters;
            public int UpdatedModelImporters;
            public int ScannedFbx;
            public int RenamedFbx;
            public int RenamedItemFolders;
            public int RenamedTextures;
            public int RenamedMaterials;
            public readonly List<string> Failures = new List<string>();
        }

        private sealed class PrefabPassContext
        {
            public readonly string[] ModelGuids;
            public readonly HashSet<string> ProcessedRelativePaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly BatchResult Result = new BatchResult();

            public PrefabPassContext(string[] modelGuids)
            {
                ModelGuids = modelGuids ?? Array.Empty<string>();
            }
        }

        private sealed class WrapperPassContext
        {
            public readonly string[] PrefabGuids;
            public readonly HashSet<string> ProcessedRelativePaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly BatchResult Result = new BatchResult();
            public readonly string NormalizedLegacyWrapperRoot;
            public readonly string NormalizedLegacySystemsWrapperRoot;

            public WrapperPassContext(string[] prefabGuids)
            {
                PrefabGuids = prefabGuids ?? Array.Empty<string>();
                NormalizedLegacyWrapperRoot = NormalizePath(LegacyWrapperRoot);
                NormalizedLegacySystemsWrapperRoot = NormalizePath(LegacySystemsWrapperRoot);
            }
        }

        private sealed class BuildAllSummaryData
        {
            public SourceNormalizationResult Normalization;
            public BatchResult PrefabResult;
            public BatchResult WrapperResult;
            public string WireSummary;
            public int WrapperAssignments;
            public int WeaponItemsLoaded;
            public int SyncedSpawners;
            public int SyncedDebugSettings;
        }
    }
}
#endif
