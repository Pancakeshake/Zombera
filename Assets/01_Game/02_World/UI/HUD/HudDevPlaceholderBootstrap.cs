using UnityEngine;
using Zombera.Characters.Work;
using Zombera.Factions;

namespace Zombera.UI
{
    /// <summary>
    ///     Provisions lightweight runtime stubs so World HUD tabs show meaningful preview data in dev scenes.
    /// </summary>
    internal static class HudDevPlaceholderBootstrap
    {
        public static void EnsureForWorldHud(WorldHUDController hud)
        {
            var host = hud != null ? hud.gameObject : null;
            if (host == null)
            {
                var foundHud = Object.FindFirstObjectByType<WorldHUDController>();
                host = foundHud != null ? foundHud.gameObject : null;
            }

            if (host == null) return;

            EnsureFactionManager(host);
            EnsureWorkManager(host);
        }

        private static void EnsureFactionManager(GameObject host)
        {
            var manager = FactionManager.Instance;
            if (manager == null)
            {
                manager = host.GetComponent<FactionManager>();
                if (manager == null) manager = host.AddComponent<FactionManager>();
            }

            manager.RestoreStandings(HudDevPlaceholderData.BuildFactionStandings());
        }

        private static void EnsureWorkManager(GameObject host)
        {
            if (WorkManager.Instance != null) return;

            var manager = host.GetComponent<WorkManager>();
            if (manager == null) manager = host.AddComponent<WorkManager>();
        }
    }
}
