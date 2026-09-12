#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.World.City;

namespace Zombera.Editor
{
    public static partial class StreamedCityProxySwapTool
    {
        private sealed class StreamedCityProxySwapRunContext
        {
            public readonly List<GameObject> SourcePrefabs = new List<GameObject>();
            public readonly HashSet<string> ExpectedProxyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> ExpectedProxyMeshAssetPaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, GameObject> ProxyBySourcePath =
                new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            public readonly StreamedCityProxySwapRunSummary Summary = new StreamedCityProxySwapRunSummary();

            public StreamedCityCatalog Catalog;
        }

        private sealed class StreamedCityProxySwapRunSummary
        {
            public int ProcessedSourcePrefabs;
            public int GeneratedOrUpdatedProxies;
            public int FailedProxyBuilds;
            public int DetectedOrphanProxyPrefabs;
            public int DetectedOrphanProxyMeshAssets;
            public int RemovedOrphanProxyPrefabs;
            public int RemovedOrphanProxyMeshAssets;
            public bool SkippedOrphanDeletionByConfirmation;
            public int CatalogWiredEntries;
            public int SceneBuildersUpdated;

            public readonly List<string> FailureLines = new List<string>();
            public readonly HashSet<string> GpuInstancingEnabledMaterialPaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class StreamedCityProxySwapPreflightResult
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();

            public bool IsValid => Errors.Count == 0;
        }
    }
}
#endif
