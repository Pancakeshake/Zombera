using UnityEngine;

namespace Zombera.Data
{
    /// <summary>
    /// Attached to each room label GameObject by the building generator.
    /// Stores the floor-space bounds of the room in the building's local space so tools and
    /// runtime systems (prop spawners, loot, AI patrol etc.) can query the usable area without
    /// needing to re-derive it from geometry.
    /// </summary>
    public sealed class RoomVolume : MonoBehaviour
    {
        [Tooltip("Room type name, e.g. 'Kitchen', 'Bathroom'. Matches InteriorPropConfig room entries.")]
        public string roomName;

        [Tooltip("Generated floor index (0 = ground floor).")]
        public int floorIndex;

        [Tooltip("Min corner of the floor rectangle in building local space (Y = floor surface).")]
        public Vector3 boundsMin;

        [Tooltip("Max corner of the floor rectangle in building local space (Y = floor surface).")]
        public Vector3 boundsMax;

        /// <summary>World-space centre of the floor area.</summary>
        public Vector3 WorldCentre =>
            transform.parent != null
                ? transform.parent.TransformPoint((boundsMin + boundsMax) * 0.5f)
                : (boundsMin + boundsMax) * 0.5f;

        /// <summary>Width (X) of the room in metres.</summary>
        public float Width => boundsMax.x - boundsMin.x;

        /// <summary>Depth (Z) of the room in metres.</summary>
        public float Depth => boundsMax.z - boundsMin.z;

        /// <summary>
        /// Returns a random point on the floor surface inside this room,
        /// offset inward by <paramref name="margin"/> metres from each wall.
        /// </summary>
        public Vector3 RandomFloorPoint(float margin = 0.3f)
        {
            var minX = boundsMin.x + margin;
            var maxX = boundsMax.x - margin;
            var minZ = boundsMin.z + margin;
            var maxZ = boundsMax.z - margin;

            if (minX >= maxX) minX = maxX = (boundsMin.x + boundsMax.x) * 0.5f;
            if (minZ >= maxZ) minZ = maxZ = (boundsMin.z + boundsMax.z) * 0.5f;

            var local = new Vector3(
                Random.Range(minX, maxX),
                boundsMin.y,
                Random.Range(minZ, maxZ));

            return transform.parent != null
                ? transform.parent.TransformPoint(local)
                : local;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var matrix = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
            var oldMatrix = UnityEngine.Gizmos.matrix;
            UnityEngine.Gizmos.matrix = matrix;
            var centre = (boundsMin + boundsMax) * 0.5f;
            var size = boundsMax - boundsMin;
            size.y = 0.05f;
            UnityEngine.Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.35f);
            UnityEngine.Gizmos.DrawCube(centre, size);
            UnityEngine.Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.9f);
            UnityEngine.Gizmos.DrawWireCube(centre, size);
            UnityEngine.Gizmos.matrix = oldMatrix;
        }
#endif
    }
}
