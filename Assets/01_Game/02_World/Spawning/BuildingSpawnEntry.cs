using System;
using UnityEngine;
using Zombera.BuildingSystem;

namespace Zombera.World.Spawning
{
    [Serializable]
    public sealed class BuildingSpawnEntry
    {
        public string id = "Building";
        public GameObject prefab;

        [Min(0f)] public float weight = 1f;

        /// <summary>
        ///     Used for placement overlap avoidance: buildings closer than this radius to another spawned building
        ///     on the same tile will be skipped.
        /// </summary>
        [Min(0f)] public float footprintRadiusMeters = 5f;

        [Header("Orientation")]
        [Tooltip("Yaw offset so the building's front face (wherever the door is) faces the road. +Z=0, -Z=180, +X=270, -X=90.")]
        public float yawOffsetDegrees;

        [Header("Structural Components")]
        [Tooltip("Adds a StructureHealth component at runtime if the prefab does not already have one.")]
        public bool ensureStructureHealth = true;

        [Min(1f)] public float structureMaxHealth = 200f;

        [Tooltip("Adds a BuildPiece component at runtime if the prefab does not already have one.")]
        public bool ensureBuildPiece = true;

        public BuildPieceCategory buildPieceCategory = BuildPieceCategory.Other;
    }
}
