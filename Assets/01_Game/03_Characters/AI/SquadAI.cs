#region

using System;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Legacy name kept for scene/prefab compatibility. Use <see cref="SquadController" /> for new content.
    /// </summary>
    [Obsolete("Use SquadController. SquadAI remains as a legacy wrapper for backwards compatibility.")]
    public sealed class SquadAI : SquadController
    {
    }
}