using UnityEngine;

namespace Zombera.Environment
{
    /// <summary>
    ///     Applies rain material repairs once after scene load so Enviro weather particles
    ///     use URP-compatible shaders before the weather cycle controller's first rescan.
    /// </summary>
    internal static class EnviroRainUrpMaterialBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyRainFixAfterSceneLoad()
        {
            if (!Application.isPlaying) return;
            EnviroRainFixUtility.ApplySceneRainFixes();
        }
    }
}
