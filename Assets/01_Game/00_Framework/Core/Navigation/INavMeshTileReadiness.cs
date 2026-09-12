using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    ///     World-side NavMesh streaming readiness without coupling gameplay code to world types.
    /// </summary>
    public interface INavMeshTileReadiness
    {
        bool IsNavMeshReadyNear(Vector3 worldPosition);
    }
}
