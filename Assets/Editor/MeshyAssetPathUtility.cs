#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Zombera.Editor
{
    internal static class MeshyAssetPathUtility
    {
        internal static List<string> FindDirectModelPaths(string folderPath)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                return results;

            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { folderPath });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsSupportedModelPath(path))
                    continue;

                var directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.Equals(directory, folderPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                results.Add(path);
            }

            return results;
        }

        internal static List<string> FindDirectAssetPaths(string folderPath, string assetFilter, params string[] allowedExtensions)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(assetFilter) ||
                !AssetDatabase.IsValidFolder(folderPath))
                return results;

            var guids = AssetDatabase.FindAssets(assetFilter, new[] { folderPath });
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasExtensionFilter = allowedExtensions != null && allowedExtensions.Length > 0;
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (!string.Equals(directory, folderPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (hasExtensionFilter)
                {
                    var extension = Path.GetExtension(path)?.ToLowerInvariant();
                    var extensionAllowed = false;
                    for (var extIndex = 0; extIndex < allowedExtensions.Length; extIndex++)
                    {
                        if (string.Equals(extension, allowedExtensions[extIndex], StringComparison.OrdinalIgnoreCase))
                        {
                            extensionAllowed = true;
                            break;
                        }
                    }

                    if (!extensionAllowed)
                        continue;
                }

                if (!seen.Add(path))
                    continue;

                results.Add(path);
            }

            return results;
        }

        internal static bool TryRenameAssetIfNeeded(string assetPath, string newNameWithoutExtension, out string failureReason)
        {
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(newNameWithoutExtension))
                return false;

            var currentName = Path.GetFileNameWithoutExtension(assetPath);
            if (string.Equals(currentName, newNameWithoutExtension, StringComparison.Ordinal))
                return false;

            var extension = Path.GetExtension(assetPath);
            var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var destinationPath = (directory?.TrimEnd('/') ?? string.Empty) + "/" + newNameWithoutExtension + extension;

            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
                return false;

            var renameError = AssetDatabase.RenameAsset(assetPath, newNameWithoutExtension);
            if (!string.IsNullOrWhiteSpace(renameError))
            {
                failureReason = renameError;
                return false;
            }

            return true;
        }

        internal static bool TryMoveAssetIfNeeded(string sourcePath, string destinationPath, out string failureReason)
        {
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            {
                failureReason = "Source or destination path was empty.";
                return false;
            }

            if (NormalizePath(sourcePath).Equals(NormalizePath(destinationPath), StringComparison.OrdinalIgnoreCase))
                return true;

            if (AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
                return true;

            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null)
                AssetDatabase.DeleteAsset(destinationPath);

            EnsureFolderHierarchy(Path.GetDirectoryName(destinationPath)?.Replace('\\', '/'));
            var moveError = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrWhiteSpace(moveError))
            {
                failureReason = "MoveAsset failed: " + moveError;
                return false;
            }

            return true;
        }

        internal static bool TryRenameModelFolder(
            string modelPath,
            string cleanedName,
            string importRoot,
            out string updatedModelPath,
            out bool folderRenamed,
            out string failureReason)
        {
            updatedModelPath = modelPath;
            folderRenamed = false;
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(modelPath)) return true;

            var effectiveName = string.IsNullOrWhiteSpace(cleanedName)
                ? Path.GetFileNameWithoutExtension(modelPath)
                : cleanedName;

            if (string.IsNullOrWhiteSpace(effectiveName)) return true;

            var folderPath = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) return true;

            if (NormalizePath(folderPath).Equals(NormalizePath(importRoot), StringComparison.OrdinalIgnoreCase))
                return true;

            var parentFolder = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(parentFolder) || !AssetDatabase.IsValidFolder(parentFolder))
                return true;

            if (!NormalizePath(parentFolder).StartsWith(NormalizePath(importRoot), StringComparison.OrdinalIgnoreCase))
                return true;

            var destinationFolderPath = parentFolder.TrimEnd('/') + "/" + effectiveName;
            if (NormalizePath(folderPath).Equals(NormalizePath(destinationFolderPath), StringComparison.OrdinalIgnoreCase))
                return true;

            if (AssetDatabase.IsValidFolder(destinationFolderPath))
            {
                failureReason = "Target folder already exists: " + destinationFolderPath;
                return false;
            }

            var moveError = AssetDatabase.MoveAsset(folderPath, destinationFolderPath);
            if (!string.IsNullOrWhiteSpace(moveError))
            {
                failureReason = "Folder rename failed: " + moveError;
                return false;
            }

            folderRenamed = true;
            updatedModelPath = destinationFolderPath + "/" + Path.GetFileName(modelPath);
            return true;
        }

        internal static string BuildUniqueFolderPath(string parentFolder, string folderName)
        {
            var candidate = parentFolder.TrimEnd('/') + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(candidate))
                return candidate;

            var index = 1;
            while (true)
            {
                candidate = parentFolder.TrimEnd('/') + "/" + folderName + "_" + index;
                if (!AssetDatabase.IsValidFolder(candidate))
                    return candidate;

                index++;
            }
        }

        internal static string StripMeshyPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var trimmed = value.Trim();

            const string aiPrefix = "Meshy_AI_";
            while (trimmed.StartsWith(aiPrefix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[aiPrefix.Length..];

            const string prefix = "Meshy_";
            while (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[prefix.Length..];

            trimmed = Regex.Replace(trimmed, @"__?\d+_texture$", string.Empty, RegexOptions.IgnoreCase);
            trimmed = Regex.Replace(trimmed, @"_texture$", string.Empty, RegexOptions.IgnoreCase);
            trimmed = trimmed.Trim('_', '-', ' ');

            return string.IsNullOrWhiteSpace(trimmed) ? value.Trim() : trimmed;
        }

        internal static string NormalizeNameKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));

            return sb.ToString();
        }

        internal static bool IsSupportedModelPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            return extension is ".fbx" or ".obj" or ".dae" or ".blend";
        }

        internal static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        internal static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                var invalid = false;
                for (var i = 0; i < invalidChars.Length; i++)
                {
                    if (ch != invalidChars[i]) continue;
                    invalid = true;
                    break;
                }

                if (!invalid)
                    sb.Append(ch);
            }

            var sanitized = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "Extracted" : sanitized;
        }

        internal static void EnsureFolderHierarchy(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;

            folderPath = folderPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            if (parts.Length == 0) return;

            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
#endif
