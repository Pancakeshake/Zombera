#if UNITY_EDITOR
#region

using System.IO;
using UnityEditor;

#endregion

namespace Zombera.Environment
{
    internal static class DayNightDefaultsProfileUtility
    {
        private const string DefaultProfileAssetPath = "Assets/01_Game/01_Core/Environment/DayNightDefaultsProfile.asset";

        internal static void ApplyDefaultsToController(DayNightController controller)
        {
            if (controller == null) return;

            var profile = controller.DefaultsProfile;
            if (profile == null)
                profile = LoadOrCreateDefaultProfile();

            controller.ApplyDefaultsProfile(profile);
            EditorUtility.SetDirty(controller);
        }

        private static DayNightDefaultsProfile LoadOrCreateDefaultProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<DayNightDefaultsProfile>(DefaultProfileAssetPath);
            if (profile != null) return profile;

            EnsureParentFolderExists(DefaultProfileAssetPath);
            profile = DayNightDefaultsProfile.CreateZomberaSurvivalProfile();
            AssetDatabase.CreateAsset(profile, DefaultProfileAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return profile;
        }

        private static void EnsureParentFolderExists(string assetPath)
        {
            var directoryPath = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(directoryPath)) return;

            directoryPath = directoryPath.Replace('\\', '/');

            var segments = directoryPath.Split('/');
            if (segments.Length == 0) return;

            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);

                current = next;
            }
        }
    }
}
#endif
