#region

using System;

#endregion

namespace Zombera.AI
{
    /// <summary>
    ///     Legacy name kept for scene/prefab compatibility. Use <see cref="ZombieController" /> for new content.
    /// </summary>
    [Obsolete("Use ZombieController. ZombieAI remains as a legacy wrapper for backwards compatibility.")]
    public sealed class ZombieAI : ZombieController
    {
    }
}