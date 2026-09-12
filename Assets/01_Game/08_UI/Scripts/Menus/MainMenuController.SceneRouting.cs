#region

using System;
using UnityEngine;

#endregion

namespace Zombera.UI.Menus
{
    public sealed partial class MainMenuController
    {
        private static readonly string[] WorldSceneFallbackNames =
        {
            "World_MapMagicStream",
            "World_Map_MagicStream"
        };

        private static bool TryResolveLoadableWorldScene(string configuredWorldSceneName,
            out string resolvedWorldSceneName)
        {
            if (!string.IsNullOrWhiteSpace(configuredWorldSceneName)
                && Application.CanStreamedLevelBeLoaded(configuredWorldSceneName))
            {
                resolvedWorldSceneName = configuredWorldSceneName;
                return true;
            }

            foreach (var fallbackName in WorldSceneFallbackNames)
            {
                if (string.IsNullOrWhiteSpace(fallbackName)) continue;

                if (!Application.CanStreamedLevelBeLoaded(fallbackName)) continue;

                resolvedWorldSceneName = fallbackName;
                return true;
            }

            resolvedWorldSceneName = configuredWorldSceneName;
            return false;
        }

        private static string GetWorldSceneCandidateSummary(string configuredWorldSceneName)
        {
            var summary = string.IsNullOrWhiteSpace(configuredWorldSceneName)
                ? "(no configured world scene)"
                : $"'{configuredWorldSceneName}'";

            foreach (var fallbackName in WorldSceneFallbackNames)
            {
                if (string.IsNullOrWhiteSpace(fallbackName)) continue;

                if (!string.IsNullOrWhiteSpace(configuredWorldSceneName)
                    && string.Equals(configuredWorldSceneName, fallbackName, StringComparison.OrdinalIgnoreCase))
                    continue;

                summary += $", '{fallbackName}'";
            }

            return summary;
        }
    }
}
