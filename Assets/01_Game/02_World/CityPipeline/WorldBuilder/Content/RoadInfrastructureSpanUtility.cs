using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Validates and stretches a 10 m Z-axis road infrastructure module across a span.</summary>
    internal static class RoadInfrastructureSpanUtility
    {
        public const float AuthoredModuleLengthMeters = 10f;

        public static bool TryResolveSpan(
            Vector2 entry,
            Vector2 exit,
            float entryY,
            float exitY,
            out Vector3 position,
            out Quaternion rotation,
            out float length)
        {
            position = default;
            rotation = Quaternion.identity;
            length = 0f;
            if (!IsFinite(entry.x) || !IsFinite(entry.y) || !IsFinite(exit.x) || !IsFinite(exit.y) ||
                !IsFinite(entryY) || !IsFinite(exitY))
                return false;

            var start = new Vector3(entry.x, entryY, entry.y);
            var end = new Vector3(exit.x, exitY, exit.y);
            var delta = end - start;
            length = delta.magnitude;
            if (length < 0.5f)
                return false;

            rotation = Quaternion.LookRotation(delta / length, Vector3.up);
            position = start;
            return true;
        }

        public static void StretchAndTileUvs(GameObject instance, float lengthMeters)
        {
            if (instance == null || lengthMeters < 0.5f)
                return;

            var multiplier = lengthMeters / AuthoredModuleLengthMeters;
            var scale = instance.transform.localScale;
            scale.z *= multiplier;
            instance.transform.localScale = scale;

            var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                ApplyTiledUvScale(renderers[i], multiplier);
        }

        private static void ApplyTiledUvScale(MeshRenderer renderer, float multiplier)
        {
            if (renderer == null || renderer.sharedMaterial == null)
                return;

            var material = renderer.sharedMaterial;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            ApplyTextureTransform(material, block, "_BaseMap_ST", multiplier);
            ApplyTextureTransform(material, block, "_MainTex_ST", multiplier);
            renderer.SetPropertyBlock(block);
        }

        private static void ApplyTextureTransform(
            Material material,
            MaterialPropertyBlock block,
            string propertyName,
            float multiplier)
        {
            if (!material.HasProperty(propertyName))
                return;

            var transform = material.GetVector(propertyName);
            if (Mathf.Abs(transform.x) < 0.0001f)
                transform.x = 1f;
            if (Mathf.Abs(transform.y) < 0.0001f)
                transform.y = 1f;
            transform.y *= multiplier;
            block.SetVector(propertyName, transform);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
