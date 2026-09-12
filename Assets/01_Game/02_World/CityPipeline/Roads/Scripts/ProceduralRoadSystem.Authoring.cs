using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Editor pinned-tile authoring entry points. MapMagic pin walking lives in
    ///     Legacy <c>MapMagicPinnedTileRoadAuthoring</c>; this partial keeps the City-safe
    ///     flags used by tile-build / gameplay publish / pipeline paths.
    /// </summary>
    public sealed partial class ProceduralRoadSystem
    {
        /// <summary>
        ///     When true, <see cref="ClearPinnedTilePreviewRoads"/> skips first-party
        ///     preview-root cleanup. Kept for the World Builder roads stage
        ///     compatibility during generation. Not serialized; callers must restore.
        /// </summary>
        [System.NonSerialized] public bool DeferEasyRoadsPurgeOnClear;

        public void RegeneratePinnedTilePreviewNow(UnityEngine.Object preferredGenerationTarget = null)
        {
            _ = preferredGenerationTarget;
            Debug.LogWarning(
                "[ProceduralRoadSystem] Pinned-tile preview requires Legacy MapMagicPinnedTileRoadAuthoring.",
                this);
        }

        public void ClearPinnedTilePreviewRoads(UnityEngine.Object preferredGenerationTarget = null)
        {
            _ = preferredGenerationTarget;
            enableEditorPinnedTileAuthoringMode = false;
            lastAuthoringPinnedTileLabel = string.Empty;
            lastAuthoringPreviewRoadCount = 0;
            if (DeferEasyRoadsPurgeOnClear)
                return;

            autoGenerateOnTileApply = _savedAutoGenerateOnTileApply;
            // EasyRoads purge removed — clear first-party procedural tile roots only.
            ClearAllTileRoads();
        }

        public void SetEditorPinnedTileAuthoringMode(bool enabled, string tileLabel = null)
        {
            if (enabled && !enableEditorPinnedTileAuthoringMode)
            {
                _savedAutoGenerateOnTileApply = autoGenerateOnTileApply;
                autoGenerateOnTileApply = false;
            }
            else if (!enabled && enableEditorPinnedTileAuthoringMode)
            {
                autoGenerateOnTileApply = _savedAutoGenerateOnTileApply;
            }

            enableEditorPinnedTileAuthoringMode = enabled;
            if (!string.IsNullOrEmpty(tileLabel))
                lastAuthoringPinnedTileLabel = tileLabel;
        }

        private bool ShouldSkipRuntimeTileApplyGeneration()
        {
#if UNITY_EDITOR
            return !Application.isPlaying && enableEditorPinnedTileAuthoringMode;
#else
            return false;
#endif
        }
    }
}
