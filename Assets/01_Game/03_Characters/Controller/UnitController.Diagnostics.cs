using UnityEngine;
using UnityEngine.AI;
using Zombera.Systems;

namespace Zombera.Characters
{
    public sealed partial class UnitController
    {
        public void LogGroundingState(string context)
        {
            var profile = MovementGroundingSettings.Active;
            if (!logGroundingDiagnostics && !profile.LogUnitGroundingState) return;

            var pos = transform.position;
            var agentActive = _agent != null && _agent.enabled;
            var isOnNavMesh = _agent != null && _agent.isOnNavMesh;
            
            float navY = float.NaN;
            float navDist = -1f;
            if (NavMesh.SamplePosition(pos, out var hit, 5f, NavMesh.AllAreas))
            {
                navY = hit.position.y;
                navDist = hit.distance;
            }

            var nextPos = _agent != null ? _agent.nextPosition : Vector3.zero;
            var baseOffset = _agent != null ? _agent.baseOffset : 0f;

            float groundY = float.NaN;
            Vector3 groundNormal = Vector3.up;
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var rayHit, 10f, profile.ResolveGroundMask()))
            {
                groundY = rayHit.point.y;
                groundNormal = rayHit.normal;
            }

            Debug.Log($"[Grounding][{context}] {name} (Role: {role})\n" +
                      $"Pos: {pos:F3}, NavY: {navY:F3} (dist: {navDist:F2})\n" +
                      $"AgentActive: {agentActive}, OnNavMesh: {isOnNavMesh}\n" +
                      $"NextPos: {nextPos:F3}, BaseOffset: {baseOffset:F2}\n" +
                      $"GroundY: {groundY:F3}, Normal: {groundNormal:F3}");
        }
    }
}
