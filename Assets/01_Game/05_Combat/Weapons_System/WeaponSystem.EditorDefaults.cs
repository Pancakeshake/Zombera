#if UNITY_EDITOR
#region

using UnityEditor;
using UnityEngine;

#endregion

namespace Zombera.Combat
{
    public sealed partial class WeaponSystem
    {
        private const string DefaultBowArrowVisualPrefabPath =
            "Assets/03_ThirdParty/Free medieval weapons/Prefabs/Arrow.prefab";

        private const string DefaultBowReleaseArrowClipPath =
            "Assets/Universal Sound FX/WEAPONS/Bow_Arrow/BOW_Release_Arrow_mono.wav";

        private const string DefaultArrowHitBodyClipPath =
            "Assets/Universal Sound FX/WEAPONS/Bow_Arrow/ARROW_Hit_Body_mono.wav";

        private void OnValidate()
        {
            var bowDataChanged = false;
            if (fallbackBowWeaponData != null)
            {
                if (fallbackBowWeaponData.arrowVisualPrefab == null)
                {
                    fallbackBowWeaponData.arrowVisualPrefab =
                        AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBowArrowVisualPrefabPath);
                    bowDataChanged = fallbackBowWeaponData.arrowVisualPrefab != null;
                }

                if (fallbackBowWeaponData.releaseArrowClip == null)
                {
                    fallbackBowWeaponData.releaseArrowClip =
                        AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultBowReleaseArrowClipPath);
                    bowDataChanged = bowDataChanged || fallbackBowWeaponData.releaseArrowClip != null;
                }

                if (fallbackBowWeaponData.hitBodyClip == null)
                {
                    fallbackBowWeaponData.hitBodyClip =
                        AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultArrowHitBodyClipPath);
                    bowDataChanged = bowDataChanged || fallbackBowWeaponData.hitBodyClip != null;
                }

                if (bowDataChanged) EditorUtility.SetDirty(fallbackBowWeaponData);
            }

            if (Application.isPlaying) return;

            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                RebuildVisuals();
            };
        }
    }
}
#endif
