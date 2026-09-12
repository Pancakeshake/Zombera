using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zombera.Core
{
    /// <summary>
    ///     UMA cross-scene appearance hygiene: deactivates legacy preview roots (UMA_GLIB) in
    ///     non-world scenes during gameplay and rebinds avatars to their own scene's UMA context.
    ///     Extracted from GameManager.
    /// </summary>
    internal static class CrossSceneAppearanceService
    {
        private const string LegacyPreviewRootName = "UMA_GLIB";

        /// <summary>
        ///     Syncs legacy preview root active state across loaded scenes, then rebinds every avatar.
        /// </summary>
        /// <param name="gameplayWorldSession">True while a world gameplay session is active (Playing).</param>
        /// <param name="sceneNameMatchesWorld">Predicate identifying the world scene by name.</param>
        public static void Sync(bool gameplayWorldSession, Func<string, bool> sceneNameMatchesWorld)
        {
            SyncLegacyPreviewRootActiveState(gameplayWorldSession, sceneNameMatchesWorld);
            UmaGlobalLibraryService.AutoBindAllAvatarsInActiveScenes();
        }

        private static void SyncLegacyPreviewRootActiveState(bool gameplayWorldSession,
            Func<string, bool> sceneNameMatchesWorld)
        {
            if (!gameplayWorldSession)
            {
                foreach (var scene in LoadedScenes())
                    SetLegacyPreviewRootsActiveInScene(scene, true);

                return;
            }

            if (!TryGetLoadedWorldScene(sceneNameMatchesWorld, out var worldScene)) return;

            var loadedCount = LoadedScenes().Count();

            if (loadedCount <= 1) return;

            foreach (var scene in LoadedScenes())
            {
                if (scene.handle == worldScene.handle) continue;

                SetLegacyPreviewRootsActiveInScene(scene, false);
            }
        }

        private static bool TryGetLoadedWorldScene(Func<string, bool> sceneNameMatchesWorld, out Scene worldScene)
        {
            worldScene = LoadedScenes().FirstOrDefault(candidate => sceneNameMatchesWorld(candidate.name));
            if (worldScene.IsValid()) return true;
            worldScene = default;
            return false;
        }

        private static IEnumerable<Scene> LoadedScenes()
        {
            return Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.IsValid() && scene.isLoaded);
        }

        private static void SetLegacyPreviewRootsActiveInScene(Scene scene, bool active)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            var roots = scene.GetRootGameObjects();
            foreach (var rootObject in roots)
            {
                var root = rootObject.transform;
                TrySetLegacyPreviewRootActive(root, active);
                foreach (Transform child in root) TrySetLegacyPreviewRootActive(child, active);
            }
        }

        private static void TrySetLegacyPreviewRootActive(Transform transform, bool active)
        {
            if (transform == null) return;

            if (!string.Equals(transform.name, LegacyPreviewRootName, StringComparison.Ordinal)) return;

            if (transform.gameObject.activeSelf != active) transform.gameObject.SetActive(active);
        }
    }
}
