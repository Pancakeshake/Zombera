#region

using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Pooled primitive markers for right-click move ghost previews.
    /// </summary>
    internal sealed class RightClickMoveGhostPool
    {
        private static readonly Color ValidTint = new(0.2f, 0.9f, 0.35f, 0.55f);
        private static readonly Color InvalidTint = new(0.9f, 0.25f, 0.2f, 0.55f);

        private readonly Transform _root;
        private readonly List<GhostInstance> _instances = new(32);
        private Material _validMaterial;
        private Material _invalidMaterial;

        public RightClickMoveGhostPool(Transform root)
        {
            _root = root;
        }

        public void EnsureCapacity(int requestedCount, int maxCount)
        {
            var targetCount = Mathf.Clamp(requestedCount, 0, Mathf.Max(1, maxCount));
            while (_instances.Count < targetCount)
                _instances.Add(CreateGhost(_instances.Count));

            for (var i = 0; i < _instances.Count; i++)
                _instances[i].Root.gameObject.SetActive(i < targetCount);
        }

        public void HideAll()
        {
            for (var i = 0; i < _instances.Count; i++)
                _instances[i].Root.gameObject.SetActive(false);
        }

        public void SetGhost(int index, Vector3 position, Vector3 forward, bool valid)
        {
            if (index < 0 || index >= _instances.Count) return;

            var ghost = _instances[index];
            ghost.Root.gameObject.SetActive(true);
            ghost.Root.SetPositionAndRotation(position, ResolveFacingRotation(forward));

            var material = valid ? ResolveValidMaterial() : ResolveInvalidMaterial();
            ghost.BodyRenderer.sharedMaterial = material;
            ghost.ArrowRenderer.sharedMaterial = material;
        }

        public void Dispose()
        {
            for (var i = 0; i < _instances.Count; i++)
            {
                var ghost = _instances[i];
                if (ghost.Root != null)
                    Object.Destroy(ghost.Root.gameObject);
            }

            _instances.Clear();

            if (_validMaterial != null) Object.Destroy(_validMaterial);
            if (_invalidMaterial != null) Object.Destroy(_invalidMaterial);

            _validMaterial = null;
            _invalidMaterial = null;
        }

        private GhostInstance CreateGhost(int index)
        {
            var root = new GameObject($"MoveGhost_{index}");
            root.transform.SetParent(_root, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            Object.Destroy(body.GetComponent<Collider>());

            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.name = "Facing";
            arrow.transform.SetParent(root.transform, false);
            arrow.transform.localPosition = new Vector3(0f, 0.35f, 0.65f);
            arrow.transform.localScale = new Vector3(0.12f, 0.08f, 0.45f);
            Object.Destroy(arrow.GetComponent<Collider>());

            root.SetActive(false);

            return new GhostInstance
            {
                Root = root.transform,
                BodyRenderer = body.GetComponent<Renderer>(),
                ArrowRenderer = arrow.GetComponent<Renderer>()
            };
        }

        private static Quaternion ResolveFacingRotation(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return Quaternion.identity;
            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private Material ResolveValidMaterial()
        {
            if (_validMaterial != null) return _validMaterial;

            _validMaterial = CreateGhostMaterial(ValidTint);
            return _validMaterial;
        }

        private Material ResolveInvalidMaterial()
        {
            if (_invalidMaterial != null) return _invalidMaterial;

            _invalidMaterial = CreateGhostMaterial(InvalidTint);
            return _invalidMaterial;
        }

        private static Material CreateGhostMaterial(Color tint)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Standard");

            var material = new Material(shader);

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", tint);
            else
                material.color = tint;

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);

            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            return material;
        }

        private struct GhostInstance
        {
            public Transform Root;
            public Renderer BodyRenderer;
            public Renderer ArrowRenderer;
        }
    }
}
