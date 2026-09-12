using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Removes leftover road containers by name, wherever they live in the scene.
    ///     These containers historically hung under the EasyRoads "Road Network" root,
    ///     which nothing creates any more — so looking them up through that root always
    ///     returned null and the cleanup silently did nothing.
    /// </summary>
    internal static class RoadLegacyContainerCleanup
    {
        /// <summary>Destroys every scene object with the given name. Safe to call when absent.</summary>
        public static void DestroyContainer(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
                return;

            var matches = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var i = 0; i < matches.Length; i++)
            {
                var candidate = matches[i];
                if (candidate == null || candidate.name != containerName)
                    continue;

                if (Application.isPlaying)
                    Object.Destroy(candidate.gameObject);
                else
                    Object.DestroyImmediate(candidate.gameObject);
            }
        }
    }
}
