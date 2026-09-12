using UnityEngine;

namespace Zombera.World.Roads
{
    public sealed class RoadEntrance : MonoBehaviour
    {
        [Tooltip("The direction this entrance faces (optional).")]
        public Vector3 direction = Vector3.forward;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.rotation * direction * 5f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);
        }
    }
}