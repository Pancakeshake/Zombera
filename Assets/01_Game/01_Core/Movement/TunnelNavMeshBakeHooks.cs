namespace Zombera.Systems
{
    /// <summary>
    ///     Cross-assembly hook so City pipeline can request tunnel-floor NavMesh requeues
    ///     without referencing StreamingNavMeshTileService (World → City cycle).
    /// </summary>
    public static class TunnelNavMeshBakeHooks
    {
        public static System.Action RequestEnqueueOverlappingFloors;

        public static void EnqueueOverlappingFloorsIfPresent()
        {
            RequestEnqueueOverlappingFloors?.Invoke();
        }
    }
}
