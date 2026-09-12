using UnityEngine;
using Zombera.Core;

namespace Zombera.BuildingSystem
{
    /// <summary>
    /// Published whenever build mode enters or exits.
    /// Used to drive UI state transitions without per-frame polling.
    /// </summary>
    public struct BuildModeChangedEvent : IGameEvent
    {
        public bool IsActive;
        public object Source;

        public BuildModeChangedEvent(bool isActive, object source)
        {
            IsActive = isActive;
            Source = source;
        }
    }
}
