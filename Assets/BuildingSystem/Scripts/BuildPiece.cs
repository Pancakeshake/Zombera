using UnityEngine;

namespace Zombera.BuildingSystem
{
    public enum BuildPieceCategory
    {
        Wall,
        Floor,
        Roof,
        Utility,
        Other
    }

    public enum WallPieceType
    {
        Full,
        Window,
        Door,
        Damaged
    }

    /// <summary>
    /// Shared metadata component for modular build pieces.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildPiece : MonoBehaviour
    {
        [Header("Piece Identity")]
        [SerializeField] private BuildPieceCategory category = BuildPieceCategory.Wall;
        [SerializeField] private WallPieceType wallType = WallPieceType.Full;

        [Header("Optional Snap Points")]
        [SerializeField] private Transform[] snapPoints = new Transform[0];

        [Header("Health")]
        [SerializeField] private StructureHealth structureHealth;

        public BuildPieceCategory Category => category;
        public WallPieceType WallType => wallType;
        public Transform[] SnapPoints => snapPoints;
        public StructureHealth Health => structureHealth;

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnValidate()
        {
            EnsureReferences();
        }

        public void SetCategory(BuildPieceCategory newCategory)
        {
            category = newCategory;
        }

        public void SetWallType(WallPieceType newWallType)
        {
            wallType = newWallType;
        }

        public void TakeDamage(float amount, GameObject source = null)
        {
            if (structureHealth == null)
            {
                return;
            }

            structureHealth.TakeDamage(amount, source);
        }

        private void EnsureReferences()
        {
            if (structureHealth == null)
            {
                structureHealth = GetComponent<StructureHealth>();
            }
        }
    }
}
