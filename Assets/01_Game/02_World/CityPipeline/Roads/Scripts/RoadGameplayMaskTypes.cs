using UnityEngine;

namespace Zombera.World.Roads
{
    [CreateAssetMenu(menuName = "Zombera/World/Road Gameplay Mask Bake Settings", fileName = "RoadGameplayMaskBakeSettings")]
    public sealed class RoadGameplayMaskBakeSettings : ScriptableObject
    {
        [Header("Bounds")]
        public bool useGraphBounds = true;

        public Vector2 manualWorldMinXZ = new(-1024f, -1024f);
        public Vector2 manualWorldMaxXZ = new(1024f, 1024f);

        [Min(0f)] public float boundsPaddingMeters = 64f;

        [Header("Texture")]
        [Min(128)] public int textureResolution = 1024;

        [Header("Road Stamp")]
        [Min(0.25f)] public float lineSampleStepMeters = 2f;
        [Min(0f)] public float extraRoadWidthMeters = 2f;
        [Min(0.1f)] public float falloffExponent = 1.75f;

        [Header("Spawn Stamp")]
        [Min(0f)] public float defaultSpawnInfluenceMeters = 10f;
    }

    [CreateAssetMenu(menuName = "Zombera/World/Road Gameplay Mask Set", fileName = "RoadGameplayMaskSet")]
    public sealed class RoadGameplayMaskSet : ScriptableObject
    {
        [Header("Masks")]
        public Texture2D terrainFlattenMask;
        public Texture2D microSplatPaintMask;
        public Texture2D navModifierMask;
        public Texture2D spawnDensityMask;
        public Texture2D pathAreaMask;
        public Texture2D sidewalkMask;
        public Texture2D decalPlacementMask;
        public Texture2D roadsideLotMask;

        [Header("Metadata")]
        public Vector2 worldMinXZ;
        public Vector2 worldMaxXZ;
        public int resolution;

        public void SetMetadata(Vector2 worldMin, Vector2 worldMax, int textureResolution)
        {
            worldMinXZ = worldMin;
            worldMaxXZ = worldMax;
            resolution = textureResolution;
        }
    }
}
