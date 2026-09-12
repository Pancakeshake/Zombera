#region

using System;
using System.Diagnostics.CodeAnalysis;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Legacy name kept for scene/prefab compatibility. Use <see cref="SurvivorController" /> for new content.
    /// </summary>
    [SuppressMessage(
        "Major Code Smell",
        "S1133",
        Justification = "Legacy wrapper is intentionally retained for scene and prefab compatibility.")]
    [Obsolete("Use SurvivorController. SurvivorAI remains as a legacy wrapper for backwards compatibility.")]
    public sealed class SurvivorAI : SurvivorController
    {
    }
}