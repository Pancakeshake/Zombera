using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Debugging.DebugLogging;

namespace Zombera.Systems
{
    [DisallowMultipleComponent]
    public sealed class SquadCommandVisualizer : MonoBehaviour
    {
        private sealed class MarkerInstance
        {
            public GameObject Root;
            public float SpawnTime;
            public float Lifetime;
        }

        [Header("Marker Lifetime")] [SerializeField] [Min(0.1f)]
        private float markerLifetimeSeconds = 1.75f;

        [SerializeField] [Min(0.1f)] private float markerScale = 1.2f;
        [SerializeField] [Min(0f)] private float markerVerticalOffset = 0.05f;

        [Header("Grounding")] [SerializeField]
        private bool snapMarkersToGround = true;

        [SerializeField] [Min(0.5f)] private float groundRaycastDistance = 72f;
        [SerializeField] [Min(0f)] private float groundRaycastUpOffset = 10f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Colors")] [SerializeField] private Color moveColor = new(0.28f, 0.92f, 0.42f, 0.95f);
        [SerializeField] private Color attackColor = new(0.96f, 0.27f, 0.20f, 0.95f);
        [SerializeField] private Color defendColor = new(0.20f, 0.67f, 0.96f, 0.95f);

        [Header("Diagnostics")] [SerializeField]
        private bool logCommandMarkerDiagnostics;

        private readonly List<MarkerInstance> _markers = new(16);
        private Material _moveMaterial;
        private Material _attackMaterial;
        private Material _defendMaterial;

        private void OnEnable()
        {
            if (CoreEventBus.Instance != null)
                CoreEventBus.Instance.Subscribe<SquadCommandIssuedEvent>(OnSquadCommandIssued);
        }

        private void OnDisable()
        {
            if (CoreEventBus.Instance != null)
                CoreEventBus.Instance.Unsubscribe<SquadCommandIssuedEvent>(OnSquadCommandIssued);

            ClearMarkersImmediate();
        }

        private void OnDestroy()
        {
            DestroyMaterial(ref _moveMaterial);
            DestroyMaterial(ref _attackMaterial);
            DestroyMaterial(ref _defendMaterial);
        }

        private void Update()
        {
            if (_markers.Count == 0) return;

            var now = Time.time;

            for (var i = _markers.Count - 1; i >= 0; i--)
            {
                var marker = _markers[i];
                if (marker == null || marker.Root == null)
                {
                    _markers.RemoveAt(i);
                    continue;
                }

                var age = now - marker.SpawnTime;
                if (age >= marker.Lifetime)
                {
                    Destroy(marker.Root);
                    _markers.RemoveAt(i);
                    continue;
                }

                var normalized = Mathf.Clamp01(age / Mathf.Max(0.001f, marker.Lifetime));
                var pulse = 1f + Mathf.Sin(normalized * 11f) * 0.12f * (1f - normalized);
                marker.Root.transform.localScale = Vector3.one * Mathf.Max(0.1f, markerScale) * pulse;
            }
        }

        private void OnSquadCommandIssued(SquadCommandIssuedEvent gameEvent)
        {
            if (gameEvent.TargetPosition == default) return;

            switch (gameEvent.CommandType)
            {
                case SquadCommandType.Move:
                    SpawnMoveMarker(gameEvent.TargetPosition);
                    return;
                case SquadCommandType.Attack:
                    SpawnAttackMarker(gameEvent.TargetPosition);
                    return;
                case SquadCommandType.Defend:
                    SpawnDefendMarker(gameEvent.TargetPosition);
                    return;
                default:
                    return;
            }
        }

        private void SpawnMoveMarker(Vector3 targetPosition)
        {
            var root = CreateMarkerRoot("Move", targetPosition);
            if (root == null) return;

            var material = GetOrCreateMaterial(ref _moveMaterial, moveColor);

            CreatePrimitive(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.02f, 0f),
                new Vector3(0.55f, 0.02f, 0.55f), material);
            CreatePrimitive(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.16f, 0f),
                new Vector3(0.10f, 0.12f, 0.36f), material);
            CreatePrimitive(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.16f, 0.18f),
                new Vector3(0.24f, 0.12f, 0.12f), material);

            TrackMarker(root);
        }

        private void SpawnAttackMarker(Vector3 targetPosition)
        {
            var root = CreateMarkerRoot("Attack", targetPosition);
            if (root == null) return;

            var material = GetOrCreateMaterial(ref _attackMaterial, attackColor);

            CreatePrimitive(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.02f, 0f),
                new Vector3(0.60f, 0.02f, 0.60f), material);

            var slashA = CreatePrimitive(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.16f, 0f),
                new Vector3(0.11f, 0.11f, 0.50f), material);
            if (slashA != null) slashA.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            var slashB = CreatePrimitive(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.16f, 0f),
                new Vector3(0.11f, 0.11f, 0.50f), material);
            if (slashB != null) slashB.transform.localRotation = Quaternion.Euler(0f, -45f, 0f);

            TrackMarker(root);
        }

        private void SpawnDefendMarker(Vector3 targetPosition)
        {
            var root = CreateMarkerRoot("Defend", targetPosition);
            if (root == null) return;

            var material = GetOrCreateMaterial(ref _defendMaterial, defendColor);

            CreatePrimitive(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.02f, 0f),
                new Vector3(0.62f, 0.02f, 0.62f), material);
            CreatePrimitive(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.25f, 0f),
                new Vector3(0.34f, 0.42f, 0.10f), material);
            CreatePrimitive(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.11f, 0f),
                new Vector3(0.10f, 0.20f, 0.10f), material);

            TrackMarker(root);
        }

        private void TrackMarker(GameObject root)
        {
            if (root == null) return;

            _markers.Add(new MarkerInstance
            {
                Root = root,
                SpawnTime = Time.time,
                Lifetime = Mathf.Max(0.1f, markerLifetimeSeconds)
            });
        }

        private GameObject CreateMarkerRoot(string markerLabel, Vector3 targetPosition)
        {
            var resolvedPosition = ResolveMarkerPosition(targetPosition);

            var root = new GameObject("SquadCommandMarker_" + markerLabel);
            root.transform.SetParent(transform, true);
            root.transform.position = resolvedPosition;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * Mathf.Max(0.1f, markerScale);

            if (logCommandMarkerDiagnostics)
                DebugLogger.LogTrace(
                    LogCategory.Squad,
                    "[SquadCommandVisualizer] Spawned " + markerLabel + " marker at " + resolvedPosition + ".",
                    this);

            return root;
        }

        private Vector3 ResolveMarkerPosition(Vector3 targetPosition)
        {
            var resolved = targetPosition;

            if (snapMarkersToGround)
            {
                var origin = targetPosition + Vector3.up * Mathf.Max(0f, groundRaycastUpOffset);
                if (Physics.Raycast(origin, Vector3.down, out var hit, Mathf.Max(0.5f, groundRaycastDistance),
                        groundMask, QueryTriggerInteraction.Ignore))
                    resolved = hit.point;
            }

            resolved.y += markerVerticalOffset;
            return resolved;
        }

        private static GameObject CreatePrimitive(PrimitiveType type, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var primitive = GameObject.CreatePrimitive(type);
            if (primitive == null) return null;

            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;

            var collider = primitive.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return primitive;
        }

        private static Material GetOrCreateMaterial(ref Material material, Color color)
        {
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = shader != null ? new Material(shader) : null;

            if (material != null)
            {
                material.color = color;
                material.enableInstancing = true;
            }

            return material;
        }

        private void ClearMarkersImmediate()
        {
            for (var i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                if (marker?.Root == null) continue;
                Destroy(marker.Root);
            }

            _markers.Clear();
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material == null) return;

            Destroy(material);
            material = null;
        }
    }
}
