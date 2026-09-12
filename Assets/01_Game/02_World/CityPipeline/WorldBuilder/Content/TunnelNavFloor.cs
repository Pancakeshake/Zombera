using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>
    ///     Marker on enterable tunnel mid segments so NavMesh bakers can allowlist floor sources.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TunnelNavFloor : MonoBehaviour
    {
        public ulong TunnelStableId;
    }
}
