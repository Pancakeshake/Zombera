using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.UI;
using Zombera.UI.SquadManagement;

namespace Zombera.Core
{
    public sealed partial class GameManager
    {
        private static void ClearCrossSceneAppearanceReferences()
        {
            var instance = Instance;
            var gameplayWorldSession = instance != null && instance.CurrentState == GameState.Playing;

            CrossSceneAppearanceService.Sync(gameplayWorldSession, StaticSceneNameMatchesWorld);
        }

        /// <summary>
        ///     Clears appearance preview references that point at another scene and rebinds each
        ///     <see cref="DynamicCharacterAvatar" /> to components in its own scene. Safe to call whenever
        ///     additive scenes load (e.g. before <see cref="PlayerSpawner" /> touches avatars).
        /// </summary>
        public static void ClearCrossSceneAppearanceBindingsForAllAvatars()
        {
            ClearCrossSceneAppearanceReferences();
        }

        private static bool StaticSceneNameMatchesWorld(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return false;

            var instance = Instance;
            if (instance is { worldSceneName: { Length: > 0 } configuredWorldScene }
                && string.Equals(sceneName, configuredWorldScene, System.StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(sceneName, "World", System.StringComparison.OrdinalIgnoreCase)) return true;

            return WorldSceneFallbackNames.Any(fallbackName =>
                !string.IsNullOrWhiteSpace(fallbackName)
                && string.Equals(sceneName, fallbackName, System.StringComparison.OrdinalIgnoreCase));
        }

        private void PruneGameplayUiOwnershipForScene(Scene scene)
        {
            if (!scene.IsValid() || IsWorldScene(scene)) return;

            var squadUis =
                FindObjectsByType<ZomberaSquadManagementUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var squadUi in squadUis)
            {
                if (squadUi == null || squadUi.gameObject.scene != scene) continue;

                if (Application.isPlaying)
                {
                    Destroy(squadUi.gameObject);
                    continue;
                }

                DestroyImmediate(squadUi.gameObject);
            }
        }

        private static void SyncGameplayUiVisibility(GameState state)
        {
            var shouldShowGameplayUi = state == GameState.Playing || state == GameState.Paused;

            var hudManagers = FindObjectsByType<HUDManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var hudManager in hudManagers) hudManager?.SetVisible(shouldShowGameplayUi);

            if (shouldShowGameplayUi) return;

            var squadUis =
                FindObjectsByType<ZomberaSquadManagementUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var squadUi in squadUis) squadUi?.SetVisible(false);
        }
    }
}