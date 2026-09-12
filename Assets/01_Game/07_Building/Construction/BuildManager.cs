#region

using UnityEngine;

#endregion

namespace Zombera.BaseBuilding
{
    /// <summary>
    ///     Coordinates base construction flow: place blueprint, assign workers, deliver materials, build, complete.
    /// </summary>
    public sealed class BuildManager : MonoBehaviour
    {
        [SerializeField] private BaseStorage baseStorage;

        [Header("Placement Snap")] [SerializeField]
        private bool snapPositionToGrid = true;

        [SerializeField] [Min(0.1f)] private float gridSize = 3f;
        [SerializeField] private bool snapRotation = true;
        [SerializeField] [Min(1f)] private float rotationSnapDegrees = 90f;

        public BaseStorage BaseStorage => baseStorage;

        public Vector3 GetSnappedPosition(Vector3 worldPosition)
        {
            if (!snapPositionToGrid) return worldPosition;

            var step = Mathf.Max(0.1f, gridSize);
            worldPosition.x = Mathf.Round(worldPosition.x / step) * step;
            worldPosition.z = Mathf.Round(worldPosition.z / step) * step;
            return worldPosition;
        }

        public float GetSnappedYaw(float yawDegrees)
        {
            if (!snapRotation) return yawDegrees;

            var step = Mathf.Max(1f, rotationSnapDegrees);
            return Mathf.Round(yawDegrees / step) * step;
        }
    }
}