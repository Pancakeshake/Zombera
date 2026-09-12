using UnityEngine.AI;
using Zombera.Systems;

namespace Zombera.World
{
    public sealed partial class StreamingNavMeshTileService
    {
        private MovementGroundingProfile ResolveNavMeshBakeProfile()
        {
            return MovementGroundingSettings.Active;
        }

        private void ApplyActiveNavMeshBuildSettings(ref NavMeshBuildSettings settings)
        {
            ResolveNavMeshBakeProfile().ApplyNavMeshBuildSettings(ref settings);
        }
    }
}
