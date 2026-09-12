#region

using System;
using UnityEngine;

#endregion

namespace Zombera.BuildingSystem
{
    // ReSharper disable UnusedMember.Global
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
    // ReSharper restore UnusedMember.Global

    /// <summary>
    ///     Shared metadata component for modular build pieces.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildPiece : MonoBehaviour
    {
        [Header("Piece Identity")] [SerializeField]
        private BuildPieceCategory category = BuildPieceCategory.Wall;

        [SerializeField] private WallPieceType wallType = WallPieceType.Full;

        [Header("Optional Snap Points")] [SerializeField]
        private Transform[] snapPoints = Array.Empty<Transform>();

        [Header("Health")] [SerializeField] private StructureHealth structureHealth;

        // ReSharper disable once UnusedMember.Global
        public BuildPieceCategory Category => category;

        // ReSharper disable once UnusedMember.Global
        public WallPieceType WallType => wallType;
        // ReSharper disable once UnusedMember.Global
        public Transform[] SnapPoints => snapPoints;
        // ReSharper disable once UnusedMember.Global
        public StructureHealth Health => structureHealth;

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnValidate()
        {
            EnsureReferences();
        }

        // ReSharper disable once UnusedMember.Global
        public void SetCategory(BuildPieceCategory newCategory)
        {
            category = newCategory;
        }

        // ReSharper disable once UnusedMember.Global
        public void SetWallType(WallPieceType newWallType)
        {
            wallType = newWallType;
        }

        // ReSharper disable once UnusedMember.Global
        public void TakeDamage(float amount, GameObject source = null)
        {
            if (structureHealth == null) return;

            structureHealth.TakeDamage(amount, source);
        }

        private void EnsureReferences()
        {
            if (structureHealth == null) structureHealth = GetComponent<StructureHealth>();
        }
    }
}