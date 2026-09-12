#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zombera.Editor
{
    internal static partial class MeshyImportPipelineRunner
    {
        private interface IModelSubAssetExtractor
        {
            void ExtractTextures(string modelPath, string destinationFolder, MeshyImportSummary summary);
            void ExtractMaterials(string modelPath, string destinationFolder, MeshyImportSummary summary);
        }

        private sealed class ReflectionThenFallbackModelSubAssetExtractor : IModelSubAssetExtractor
        {
            public void ExtractTextures(string modelPath, string destinationFolder, MeshyImportSummary summary)
            {
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (importer == null) return;

                var extracted = TryInvokeImporterExtraction(importer, "ExtractTextures", destinationFolder);
                if (extracted)
                {
                    summary.TextureExtractOperations++;
                    return;
                }

                summary.ExtractedTexturesFromFallback += ExtractEmbeddedSubAssets<Texture2D>(
                    modelPath,
                    destinationFolder,
                    ".png");
            }

            public void ExtractMaterials(string modelPath, string destinationFolder, MeshyImportSummary summary)
            {
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (importer == null) return;

                var extracted = TryInvokeImporterExtraction(importer, "ExtractMaterials", destinationFolder);
                if (extracted)
                {
                    summary.MaterialExtractOperations++;
                    return;
                }

                summary.ExtractedMaterialsFromFallback += ExtractEmbeddedSubAssets<Material>(
                    modelPath,
                    destinationFolder,
                    ".mat");
            }

            private static bool TryInvokeImporterExtraction(ModelImporter importer, string methodName,
                string destinationFolder)
            {
                if (importer == null || string.IsNullOrWhiteSpace(methodName) ||
                    string.IsNullOrWhiteSpace(destinationFolder))
                    return false;

                var method = typeof(ModelImporter).GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);

                if (method == null)
                    return false;

                try
                {
                    var invocationResult = method.Invoke(importer, new object[] { destinationFolder });
                    return invocationResult is not bool boolResult || boolResult;
                }
                catch
                {
                    return false;
                }
            }

            private static int ExtractEmbeddedSubAssets<T>(string modelPath, string destinationFolder, string extension)
                where T : Object
            {
                var extracted = 0;
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
                if (subAssets == null || subAssets.Length == 0)
                    return extracted;

                var normalizedModelPath = MeshyAssetPathUtility.NormalizePath(modelPath);
                for (var i = 0; i < subAssets.Length; i++)
                {
                    if (subAssets[i] is not T subAsset)
                        continue;

                    var sourcePath = MeshyAssetPathUtility.NormalizePath(AssetDatabase.GetAssetPath(subAsset));
                    if (!string.Equals(sourcePath, normalizedModelPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var baseName = MeshyAssetPathUtility.SanitizeAssetName(subAsset.name);
                    if (string.IsNullOrWhiteSpace(baseName))
                        continue;

                    var targetPath = AssetDatabase.GenerateUniqueAssetPath(
                        destinationFolder.TrimEnd('/') + "/" + baseName + extension);
                    var extractError = AssetDatabase.ExtractAsset(subAsset, targetPath);
                    if (string.IsNullOrWhiteSpace(extractError))
                        extracted++;
                }

                return extracted;
            }
        }
    }
}
#endif
