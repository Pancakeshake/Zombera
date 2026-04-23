using System;
using System.Collections;
using UMA.CharacterSystem;
using UnityEngine;
using Zombera.Core;

namespace Zombera.Characters
{
    public sealed class UmaSpawnStylingService
    {
        private const string PreviewUmaRootName = "UMA_GLIB";
        private const string PreviewAvatarRootName = "UMAPreviewAvatar";
        private const string PreviewCameraName = "UMAPreviewCamera";

        private readonly MonoBehaviour _host;
        private readonly UnityEngine.Object _logContext;

        public UmaSpawnStylingService(MonoBehaviour host, UnityEngine.Object logContext)
        {
            _host = host;
            _logContext = logContext;
        }

        public DynamicCharacterAvatar PrepareUmaAvatarForSanitizedSpawn(GameObject unitRoot)
        {
            if (unitRoot == null)
            {
                return null;
            }

            // Purge any preview artifacts before UMA tries to auto-generate.
            SanitizeRuntimeUnitHierarchy(unitRoot, null);

            DynamicCharacterAvatar avatar = unitRoot.GetComponent<DynamicCharacterAvatar>();
            if (avatar == null)
            {
                return null;
            }

            avatar.loadFileOnStart = false;
            avatar.enabled = false;
            return avatar;
        }

        public void ApplySelectedUmaRecipe(GameObject instance, DynamicCharacterAvatar avatar = null)
        {
            // Sanitize before and after recipe application to avoid preview bones persisting into avatar build.
            SanitizeRuntimeUnitHierarchy(instance, null);

            if (instance == null)
            {
                return;
            }

            if (avatar == null)
            {
                avatar = instance.GetComponent<DynamicCharacterAvatar>();
            }

            if (avatar == null)
            {
                return;
            }

            string recipe = CharacterSelectionState.SelectedUmaRecipe;
            if (!string.IsNullOrEmpty(recipe))
            {
                avatar.loadFileOnStart = false;
                avatar.LoadFromRecipeString(recipe);
            }

            SanitizeRuntimeUnitHierarchy(instance, null);

            // Re-enable so DCA's Start() runs and builds the character.
            avatar.enabled = true;
        }

        public void StartApplySelectedUmaRecipeNextFrame(GameObject instance, DynamicCharacterAvatar dca, UnitController unitControllerForRewarp)
        {
            if (_host == null)
            {
                return;
            }

            _host.StartCoroutine(ApplyUmaRecipeNextFrame(instance, dca, unitControllerForRewarp));
        }

        private IEnumerator ApplyUmaRecipeNextFrame(GameObject instance, DynamicCharacterAvatar dca, UnitController unitControllerForRewarp)
        {
            yield return null; // let Start() run on all components first
            ApplySelectedUmaRecipe(instance, dca);

            // UMA rebuilds asynchronously after LoadFromRecipeString. Re-warp after each build completes.
            if (unitControllerForRewarp != null && dca != null)
            {
                dca.CharacterCreated.AddListener(_ => unitControllerForRewarp.ForceEnableAgent());
                dca.CharacterUpdated.AddListener(_ => unitControllerForRewarp.ForceEnableAgent());
            }
        }

        public void SanitizeRuntimeUnitHierarchy(GameObject unitRoot, PlayerSpawner owningSpawner)
        {
            if (unitRoot == null)
            {
                return;
            }

            RemoveNamedChildObjects(unitRoot.transform, PreviewCameraName);
            RemoveNamedChildObjects(unitRoot.transform, PreviewAvatarRootName);
            RemoveNamedChildObjects(unitRoot.transform, PreviewUmaRootName);
            DisableAndRemoveNestedPreviewAvatars(unitRoot.transform);

            Camera[] nestedCameras = unitRoot.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < nestedCameras.Length; i++)
            {
                Camera nestedCamera = nestedCameras[i];
                if (nestedCamera == null)
                {
                    continue;
                }

                bool looksLikePreviewCamera =
                    nestedCamera.targetTexture != null ||
                    nestedCamera.gameObject.name.IndexOf("Preview", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!looksLikePreviewCamera)
                {
                    continue;
                }

                DestroyUnityObject(nestedCamera.gameObject);
            }

            // Defensive: avoid recursive spawners on Unit prefabs.
            PlayerSpawner[] nestedSpawners = unitRoot.GetComponentsInChildren<PlayerSpawner>(true);
            for (int i = 0; i < nestedSpawners.Length; i++)
            {
                PlayerSpawner nestedSpawner = nestedSpawners[i];
                if (nestedSpawner == null || nestedSpawner == owningSpawner)
                {
                    continue;
                }

                nestedSpawner.enabled = false;
                DestroyUnityObject(nestedSpawner);
            }
        }

        private static void DisableAndRemoveNestedPreviewAvatars(Transform unitRoot)
        {
            if (unitRoot == null)
            {
                return;
            }

            DynamicCharacterAvatar[] nestedAvatars = unitRoot.GetComponentsInChildren<DynamicCharacterAvatar>(true);
            for (int i = 0; i < nestedAvatars.Length; i++)
            {
                DynamicCharacterAvatar nestedAvatar = nestedAvatars[i];
                if (nestedAvatar == null)
                {
                    continue;
                }

                if (!HasPreviewMarkerInHierarchy(nestedAvatar.transform, unitRoot))
                {
                    continue;
                }

                nestedAvatar.loadFileOnStart = false;
                nestedAvatar.enabled = false;

                if (nestedAvatar.gameObject.activeSelf)
                {
                    nestedAvatar.gameObject.SetActive(false);
                }

                DetachAndDestroy(nestedAvatar.gameObject, unitRoot);
            }
        }

        private static void RemoveNamedChildObjects(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform candidate = allTransforms[i];
                if (candidate == null || candidate == root)
                {
                    continue;
                }

                if (!MatchesNamedObject(candidate.name, objectName))
                {
                    continue;
                }

                DetachAndDestroy(candidate.gameObject, root);
            }
        }

        private static bool MatchesNamedObject(string candidateName, string objectName)
        {
            if (string.IsNullOrWhiteSpace(candidateName) || string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            if (string.Equals(candidateName, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Handle Unity runtime/editor renaming patterns like "Name (1)" and "Name(Clone)".
            return candidateName.StartsWith(objectName + " ", StringComparison.OrdinalIgnoreCase) ||
                   candidateName.StartsWith(objectName + "(", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPreviewMarkerName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return false;
            }

            return objectName.IndexOf(PreviewUmaRootName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf(PreviewAvatarRootName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("UMAPreview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("PreviewAvatar", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasPreviewMarkerInHierarchy(Transform candidate, Transform root)
        {
            Transform current = candidate;
            while (current != null && current != root)
            {
                if (IsPreviewMarkerName(current.name))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static void DetachAndDestroy(GameObject target, Transform root)
        {
            if (target == null)
            {
                return;
            }

            Transform targetTransform = target.transform;
            if (root != null && targetTransform != null && targetTransform != root && targetTransform.IsChildOf(root))
            {
                targetTransform.SetParent(null, false);
            }

            DestroyUnityObject(target);
        }

        private static void DestroyUnityObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}

