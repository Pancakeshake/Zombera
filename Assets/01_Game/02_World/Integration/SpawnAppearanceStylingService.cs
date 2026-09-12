#region

using System;
using System.Collections;
using UnityEngine;
using UMA.CharacterSystem;
using Zombera.UI.Menus.CharacterCreation;

#endregion

namespace Zombera.Characters
{
    /// <summary>
    ///     Spawn styling service for characters.
    ///     Keeps runtime hierarchy sanitation and delayed post-spawn hooks.
    /// </summary>
    public sealed class SpawnAppearanceStylingService
    {
        private const string LegacyPreviewRootName = "UMA_GLIB";
        private const string LegacyPreviewAvatarRootName = "UMAPreviewAvatar";
        private const string LegacyPreviewCameraName = "UMAPreviewCamera";

        private readonly MonoBehaviour _host;

        public SpawnAppearanceStylingService(MonoBehaviour host)
        {
            _host = host;
        }

        public static GameObject PrepareAvatarForSanitizedSpawn(GameObject unitRoot)
        {
            if (unitRoot == null) return null;

            SanitizeRuntimeUnitHierarchy(unitRoot, null);
            return unitRoot;
        }

        public static void ApplySelectedAppearanceProfile(GameObject instance, string profileJson, GameObject avatarRoot = null)
        {
            if (instance == null) return;

            SanitizeRuntimeUnitHierarchy(instance, null);

            CharacterAppearanceProfile profile;
            if (string.IsNullOrWhiteSpace(profileJson))
            {
                // If no profile is provided (e.g. new game without character creator), generate a valid random male.
                // This ensures the character has base slots and valid DNA so it is visible.
                profile = AppearanceProfileService.GenerateRandomProfile("HumanMale");
            }
            else
            {
                profile = CharacterAppearanceProfile.Deserialize(profileJson);
            }

            var report = AppearanceProfileService.TryApplyProfile(avatarRoot != null ? avatarRoot : instance, profile, true, true);

            if (!report.Success)
            {
                Debug.LogWarning($"[SpawnAppearanceStylingService] Appearance application for {instance.name} had issues:\n{report.ToMultilineString()}");
            }

            ForceEnableRenderers(instance);
        }

        public void StartApplySelectedAppearanceNextFrame(GameObject instance, string profileJson, GameObject avatarRoot,
            UnitController unitControllerForRewarp)
        {
            if (_host == null) return;

            _host.StartCoroutine(ApplyAppearanceNextFrame(instance, profileJson, avatarRoot, unitControllerForRewarp));
        }

        private static IEnumerator ApplyAppearanceNextFrame(GameObject instance, string profileJson, GameObject avatarRoot, UnitController unitControllerForRewarp)
        {
            yield return null;
            
            var target = avatarRoot != null ? avatarRoot : instance;
            var avatar = target.GetComponentInChildren<DynamicCharacterAvatar>();

            // Preserve active-player control state while applying UMA appearance.
            // ApplySelectedAppearanceProfile sanitizes the root and temporarily disables
            // player-specific components (including PlayerInputController).
            var playerInput = instance != null ? instance.GetComponent<Zombera.Systems.PlayerInputController>() : null;
            var restorePlayerComponents = playerInput != null && playerInput.enabled;

            ApplySelectedAppearanceProfile(instance, profileJson, avatarRoot);

            if (restorePlayerComponents)
                EnablePlayerSpecificComponents(instance);

            if (avatar != null)
            {
                // Wait for UMA build to complete. 
                float timeout = Time.time + 5f;
                // Wait until UMAData is assigned
                while (avatar.umaData == null && Time.time < timeout)
                {
                    yield return null;
                }

                if (avatar.umaData != null)
                {
                    // Wait while UMAData is dirty (building)
                    while (avatar.umaData.dirty && Time.time < timeout)
                    {
                        yield return null;
                    }
                }
            }

            if (unitControllerForRewarp != null)
                unitControllerForRewarp.ForceEnableAgent();
        }

        public static void SanitizeRuntimeUnitHierarchy(GameObject unitRoot, PlayerSpawner owningSpawner)
        {
            if (unitRoot == null) return;

            RemoveNamedChildObjects(unitRoot.transform, LegacyPreviewCameraName);
            RemoveNamedChildObjects(unitRoot.transform, LegacyPreviewAvatarRootName);
            RemoveNamedChildObjects(unitRoot.transform, LegacyPreviewRootName);

            // Disable player-specific components by default. 
            // The PlayerSpawner will re-enable them for the actual player unit.
            DisablePlayerSpecificComponents(unitRoot);

            var nestedCameras = unitRoot.GetComponentsInChildren<Camera>(true);
            for (var i = 0; i < nestedCameras.Length; i++)
            {
                var nestedCamera = nestedCameras[i];
                if (nestedCamera == null) continue;

                var looksLikePreviewCamera =
                    nestedCamera.targetTexture != null ||
                    nestedCamera.gameObject.name.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!looksLikePreviewCamera) continue;
                DestroyUnityObject(nestedCamera.gameObject);
            }

            var nestedSpawners = unitRoot.GetComponentsInChildren<PlayerSpawner>(true);
            for (var i = 0; i < nestedSpawners.Length; i++)
            {
                var nestedSpawner = nestedSpawners[i];
                if (nestedSpawner == null || nestedSpawner == owningSpawner) continue;

                nestedSpawner.enabled = false;
                DestroyUnityObject(nestedSpawner);
            }
        }

        private static void DisablePlayerSpecificComponents(GameObject unitRoot)
        {
            SetPlayerSpecificComponentsEnabled(unitRoot, false);
        }

        public static void EnablePlayerSpecificComponents(GameObject unitRoot)
        {
            SetPlayerSpecificComponentsEnabled(unitRoot, true);
        }

        private static void SetPlayerSpecificComponentsEnabled(GameObject unitRoot, bool enabled)
        {
            if (unitRoot == null) return;

            var playerInput = unitRoot.GetComponent<Zombera.Systems.PlayerInputController>();
            if (playerInput != null) playerInput.enabled = enabled;

            var behaviours = unitRoot.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var b in behaviours)
            {
                if (b == null) continue;
                var typeName = b.GetType().Name;

                if (typeName.Contains("Building") || 
                    typeName.Contains("EasyBuild") || 
                    typeName.Contains("Interactor") ||
                    typeName == "CursorManager")
                {
                    b.enabled = enabled;
                }
            }
        }

        private static void RemoveNamedChildObjects(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName)) return;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate == null || candidate == root) continue;
                if (!string.Equals(candidate.name, objectName, StringComparison.Ordinal)) continue;

                DestroyUnityObject(candidate.gameObject);
            }
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null) return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        public static void ForceEnableRenderers(GameObject root)
        {
            if (root == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!r.enabled) r.enabled = true;
            }

            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var s in skinned)
            {
                if (s == null) continue;
                // Helps prevent skinned meshes from being culled incorrectly during initial spawn.
                if (!s.updateWhenOffscreen) s.updateWhenOffscreen = true;
            }
        }
        }
        }
