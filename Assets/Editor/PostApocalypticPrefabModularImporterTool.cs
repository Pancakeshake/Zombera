using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zombera.BuildingSystem;

namespace Zombera.Editor
{
    /// <summary>
    /// Converts Post Apocalyptic pack prefabs into modular-style build prefabs.
    /// Output is flattened into Assets/BuildingSystem/Prefab_Modular using deterministic names.
    /// Safe to rerun: output paths are stable and assets are overwritten in-place.
    /// </summary>
    public static class PostApocalypticPrefabModularImporterTool
    {
        private const string SourcePrefabFolder = "Assets/ThirdParty/Post_Apocalyptic_Asset_Pack/Prefabs";
        private const string DestinationPrefabFolder = "Assets/BuildingSystem/Prefab_Modular";
        private const string DestinationPrefix = "PA_";
        private const float DefaultStructureHealth = 150f;

        [MenuItem("Tools/Zombera/Building/Import Post Apocalyptic Prefabs To Modular Folder")]
        private static void ImportPostApocalypticPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(SourcePrefabFolder))
            {
                EditorUtility.DisplayDialog(
                    "Post Apocalyptic Import",
                    $"Source folder not found:\n{SourcePrefabFolder}",
                    "OK");
                return;
            }

            EnsureFolderExists(DestinationPrefabFolder);

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { SourcePrefabFolder });
            if (prefabGuids == null || prefabGuids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Post Apocalyptic Import",
                    $"No prefabs found under:\n{SourcePrefabFolder}",
                    "OK");
                return;
            }

            int createdCount = 0;
            int updatedCount = 0;
            int failedCount = 0;

            try
            {
                for (int i = 0; i < prefabGuids.Length; i++)
                {
                    string sourcePath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
                    EditorUtility.DisplayProgressBar(
                        "Import Post Apocalyptic Prefabs",
                        $"{i + 1}/{prefabGuids.Length}: {sourceName}",
                        (i + 1f) / prefabGuids.Length);

                    if (!TryConvertSinglePrefab(sourcePath, out bool existedBefore))
                    {
                        failedCount++;
                        continue;
                    }

                    if (existedBefore)
                    {
                        updatedCount++;
                    }
                    else
                    {
                        createdCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary =
                "Import complete.\n\n" +
                $"Source prefabs scanned: {prefabGuids.Length}\n" +
                $"Created: {createdCount}\n" +
                $"Updated: {updatedCount}\n" +
                $"Failed: {failedCount}\n\n" +
                $"Output folder:\n{DestinationPrefabFolder}";

            Debug.Log($"[PostApocalypticPrefabModularImporter] {summary}");
            EditorUtility.DisplayDialog("Post Apocalyptic Import", summary, "OK");
        }

        [MenuItem("Tools/Zombera/Building/Import Post Apocalyptic Prefabs To Modular Folder", true)]
        private static bool ValidateImportPostApocalypticPrefabs()
        {
            return AssetDatabase.IsValidFolder(SourcePrefabFolder);
        }

        private static bool TryConvertSinglePrefab(string sourcePrefabPath, out bool existedBefore)
        {
            existedBefore = false;

            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            if (sourcePrefab == null)
            {
                Debug.LogWarning($"[PostApocalypticPrefabModularImporter] Skipped null prefab asset: {sourcePrefabPath}");
                return false;
            }

            string outputPrefabPath = BuildOutputPrefabPath(sourcePrefabPath);
            existedBefore = File.Exists(outputPrefabPath);

            GameObject wrapperRoot = null;
            try
            {
                wrapperRoot = BuildWrappedPrefabRoot(sourcePrefab, sourcePrefabPath);
                if (wrapperRoot == null)
                {
                    Debug.LogWarning($"[PostApocalypticPrefabModularImporter] Failed to build wrapped prefab root: {sourcePrefabPath}");
                    return false;
                }

                bool success;
                PrefabUtility.SaveAsPrefabAsset(wrapperRoot, outputPrefabPath, out success);

                if (!success)
                {
                    Debug.LogWarning($"[PostApocalypticPrefabModularImporter] Unity reported save failure for: {outputPrefabPath}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[PostApocalypticPrefabModularImporter] Exception while converting '{sourcePrefabPath}'.\n" +
                    $"{ex}");
                return false;
            }
            finally
            {
                if (wrapperRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(wrapperRoot);
                }
            }
        }

        private static GameObject BuildWrappedPrefabRoot(GameObject sourcePrefab, string sourcePrefabPath)
        {
            string rootName = Path.GetFileNameWithoutExtension(sourcePrefabPath);
            GameObject wrapperRoot = new GameObject(rootName);

            GameObject sourceInstance = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (sourceInstance == null)
            {
                sourceInstance = UnityEngine.Object.Instantiate(sourcePrefab);
            }

            sourceInstance.name = rootName;
            sourceInstance.transform.SetParent(wrapperRoot.transform, worldPositionStays: false);
            sourceInstance.transform.localPosition = Vector3.zero;
            sourceInstance.transform.localRotation = Quaternion.identity;
            sourceInstance.transform.localScale = Vector3.one;

            RemoveChildColliders(sourceInstance);

            StructureHealth structureHealth = wrapperRoot.AddComponent<StructureHealth>();
            structureHealth.SetMaxHealth(DefaultStructureHealth, refillCurrentHealth: true);

            BuildPiece buildPiece = wrapperRoot.AddComponent<BuildPiece>();
            buildPiece.SetCategory(BuildPieceCategory.Wall);
            buildPiece.SetWallType(WallPieceType.Full);

            SerializedObject serializedBuildPiece = new SerializedObject(buildPiece);
            SerializedProperty healthProperty = serializedBuildPiece.FindProperty("structureHealth");
            if (healthProperty != null)
            {
                healthProperty.objectReferenceValue = structureHealth;
                serializedBuildPiece.ApplyModifiedPropertiesWithoutUndo();
            }

            if (TryGetSingleMesh(sourceInstance, out Mesh sharedMesh))
            {
                MeshCollider meshCollider = wrapperRoot.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = sharedMesh;
            }
            else
            {
                BoxCollider boxCollider = wrapperRoot.AddComponent<BoxCollider>();
                if (TryGetCombinedLocalRendererBounds(sourceInstance, wrapperRoot.transform, out Bounds bounds))
                {
                    boxCollider.center = bounds.center;
                    boxCollider.size = ClampBoundsSize(bounds.size);
                }
                else
                {
                    boxCollider.center = Vector3.zero;
                    boxCollider.size = Vector3.one;
                }
            }

            return wrapperRoot;
        }

        private static void RemoveChildColliders(GameObject sourceInstance)
        {
            if (sourceInstance == null)
            {
                return;
            }

            Collider[] colliders = sourceInstance.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(colliders[i]);
                }
            }
        }

        private static bool TryGetSingleMesh(GameObject root, out Mesh sharedMesh)
        {
            sharedMesh = null;
            if (root == null)
            {
                return false;
            }

            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            int validCount = 0;
            Mesh onlyMesh = null;

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                validCount++;
                onlyMesh = meshFilter.sharedMesh;

                if (validCount > 1)
                {
                    break;
                }
            }

            if (validCount == 1)
            {
                sharedMesh = onlyMesh;
                return true;
            }

            return false;
        }

        private static bool TryGetCombinedLocalRendererBounds(GameObject root, Transform localSpace, out Bounds bounds)
        {
            bounds = default;
            if (root == null || localSpace == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            bool hasPoint = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Bounds worldBounds = renderer.bounds;
                Vector3[] corners = GetBoundsCorners(worldBounds);

                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 localCorner = localSpace.InverseTransformPoint(corners[cornerIndex]);
                    if (!hasPoint)
                    {
                        min = localCorner;
                        max = localCorner;
                        hasPoint = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, localCorner);
                        max = Vector3.Max(max, localCorner);
                    }
                }
            }

            if (!hasPoint)
            {
                return false;
            }

            bounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        private static Vector3[] GetBoundsCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            return new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };
        }

        private static Vector3 ClampBoundsSize(Vector3 size)
        {
            const float minimumSize = 0.05f;
            return new Vector3(
                Mathf.Max(minimumSize, size.x),
                Mathf.Max(minimumSize, size.y),
                Mathf.Max(minimumSize, size.z));
        }

        private static string BuildOutputPrefabPath(string sourcePrefabPath)
        {
            string relative = sourcePrefabPath;
            string sourcePrefix = SourcePrefabFolder + "/";

            if (relative.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                relative = relative.Substring(sourcePrefix.Length);
            }

            relative = Path.ChangeExtension(relative, null) ?? relative;
            relative = relative.Replace('\\', '/');

            string flattened = relative.Replace("/", "__");
            string safeName = SanitizeAssetFileName(flattened);
            return $"{DestinationPrefabFolder}/{DestinationPrefix}{safeName}.prefab";
        }

        private static string SanitizeAssetFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unnamed";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool invalid = Array.IndexOf(invalidChars, c) >= 0;

                if (invalid || c == '.')
                {
                    builder.Append('_');
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] segments = folderPath.Split('/');
            if (segments.Length == 0)
            {
                return;
            }

            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
