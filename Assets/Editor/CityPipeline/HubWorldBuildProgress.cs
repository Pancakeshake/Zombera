#if UNITY_EDITOR
using System;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.Editor
{
    /// <summary>
    ///     Forwards hub pipeline progress messages to the Unity Console.
    ///     Reports arrive from tight per-tile / per-batch loops, so lines are throttled to
    ///     stage changes, 5% progress buckets, and the first distinct message inside a bucket.
    /// </summary>
    internal sealed class HubWorldBuildProgress : IWorldBuildProgress
    {
        private const int PercentBucketSize = 5;

        public static readonly HubWorldBuildProgress Instance = new();

        private WorldBuildStageId _lastStage;
        private int _lastBucket = int.MinValue;
        private string _lastMessage;
        private bool _loggedMessageInBucket;

        public void Report(WorldBuildStageId stage, float normalized01, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            var clamped01 = Mathf.Clamp01(normalized01);
            var bucket = Mathf.FloorToInt(clamped01 * 100f) / PercentBucketSize;
            var stageChanged = stage != _lastStage;
            var bucketChanged = bucket != _lastBucket;
            var messageChanged = !string.Equals(message, _lastMessage, StringComparison.Ordinal);
            var firstMessageInBucket = messageChanged && !_loggedMessageInBucket;

            // Gate before formatting — the interpolated string is only built on the way out.
            if (!stageChanged && !bucketChanged && !firstMessageInBucket)
                return;

            _loggedMessageInBucket = firstMessageInBucket && !stageChanged && !bucketChanged;
            _lastStage = stage;
            _lastBucket = bucket;
            _lastMessage = message;

            Debug.Log($"[WorldBuilderHub] stage={stage} progress={clamped01:0.00} {message}");
        }

        public void ReportWarning(WorldBuildStageId stage, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            Debug.LogWarning($"[WorldBuilderHub] stage={stage} {message}");
        }
    }
}
#endif
