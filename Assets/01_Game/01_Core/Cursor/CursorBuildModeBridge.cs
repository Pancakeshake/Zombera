#region

using UnityEngine;
using Zombera.BuildingSystem;
using Zombera.Core;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Keeps cursor capture context aligned with build mode without per-frame polling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CursorBuildModeBridge : MonoBehaviour
    {
        private CursorStateHandle _buildModeHandle;

        private void OnEnable()
        {
            CoreEventBus.Instance?.Subscribe<BuildModeChangedEvent>(OnBuildModeChanged);
        }

        private void OnDisable()
        {
            CoreEventBus.Instance?.Unsubscribe<BuildModeChangedEvent>(OnBuildModeChanged);
            _buildModeHandle.Dispose();
        }

        private void OnBuildModeChanged(BuildModeChangedEvent evt)
        {
            _buildModeHandle.Dispose();

            if (!evt.IsActive) return;

            _buildModeHandle = CursorService.Request(
                CursorContextPriority.BuildMode,
                CursorPresentation.FreePointer);
        }
    }
}
