using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Systems
{
    /// <summary>
    /// Marks a world object that can be hidden/revealed by the fog-of-war system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FogOfWarTarget : MonoBehaviour
    {
        private static readonly List<FogOfWarTarget> ActiveTargetRegistry = new List<FogOfWarTarget>();

        [Header("Target")]
        [SerializeField] private Transform visibilityPoint;
        [SerializeField] private bool autoCollectRenderers = true;
        [SerializeField] private bool includeInactiveRenderers = true;
        [SerializeField] private Renderer[] controlledRenderers = Array.Empty<Renderer>();

        [Header("Memory")]
        [SerializeField] private bool keepVisibleForShortMemory = true;
        [SerializeField, Min(0f)] private float memoryDurationSeconds = 2f;

        [Header("Debug Gizmos")]
        [SerializeField] private bool drawTargetGizmo = true;
        [SerializeField] private bool drawTargetGizmoWhenNotSelected;
        [SerializeField, Min(0.05f)] private float targetGizmoRadiusMeters = 0.35f;
        [SerializeField] private Color targetVisibleColor = new Color(0.2f, 0.9f, 0.2f, 0.85f);
        [SerializeField] private Color targetHiddenColor = new Color(0.95f, 0.2f, 0.2f, 0.85f);
        [SerializeField] private Color targetEditModeColor = new Color(1f, 0.9f, 0.2f, 0.85f);

        private readonly HashSet<Renderer> hiddenByFogRenderers = new HashSet<Renderer>();
        private bool currentlyHiddenByFog;
        private float lastDirectlyVisibleTime = float.NegativeInfinity;

        public static IReadOnlyList<FogOfWarTarget> ActiveTargets => ActiveTargetRegistry;

        public Vector3 VisibilityPosition
        {
            get
            {
                if (visibilityPoint != null)
                {
                    return visibilityPoint.position;
                }

                return transform.position + Vector3.up * 1.1f;
            }
        }

        private void Awake()
        {
            RebuildRendererCacheIfNeeded();
        }

        private void OnEnable()
        {
            if (!FogOfWarRuntimeConfig.FeatureEnabled)
            {
                if (Application.isPlaying)
                {
                    Destroy(this);
                }
                else
                {
                    enabled = false;
                }

                return;
            }

            if (!ActiveTargetRegistry.Contains(this))
            {
                ActiveTargetRegistry.Add(this);
            }

            RebuildRendererCacheIfNeeded();
        }

        private void OnDisable()
        {
            ActiveTargetRegistry.Remove(this);
            RevealImmediately();
        }

        private void OnDrawGizmos()
        {
            if (!drawTargetGizmoWhenNotSelected)
            {
                return;
            }

            DrawTargetGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (drawTargetGizmoWhenNotSelected)
            {
                return;
            }

            DrawTargetGizmo();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildRendererCacheIfNeeded();
        }
#endif

        public bool ContainsCollider(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            Transform colliderTransform = collider.transform;
            return colliderTransform == transform || colliderTransform.IsChildOf(transform);
        }

        public void ApplyVisionState(bool directlyVisible, float currentTime)
        {
            if (directlyVisible)
            {
                lastDirectlyVisibleTime = currentTime;
            }

            bool rememberedVisible = keepVisibleForShortMemory
                && currentTime - lastDirectlyVisibleTime <= memoryDurationSeconds;

            if (directlyVisible || rememberedVisible)
            {
                RevealImmediately();
            }
            else
            {
                HideImmediately();
            }
        }

        public void RebuildRendererCacheIfNeeded()
        {
            if (!autoCollectRenderers)
            {
                return;
            }

            controlledRenderers = includeInactiveRenderers
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponentsInChildren<Renderer>();
        }

        private void HideImmediately()
        {
            if (currentlyHiddenByFog)
            {
                return;
            }

            if (controlledRenderers == null)
            {
                currentlyHiddenByFog = true;
                return;
            }

            for (int i = 0; i < controlledRenderers.Length; i++)
            {
                Renderer renderer = controlledRenderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                hiddenByFogRenderers.Add(renderer);
                renderer.enabled = false;
            }

            currentlyHiddenByFog = true;
        }

        private void RevealImmediately()
        {
            if (!currentlyHiddenByFog)
            {
                return;
            }

            if (hiddenByFogRenderers.Count > 0)
            {
                foreach (Renderer renderer in hiddenByFogRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }

                hiddenByFogRenderers.Clear();
            }

            currentlyHiddenByFog = false;
        }

        private void DrawTargetGizmo()
        {
            if (!drawTargetGizmo)
            {
                return;
            }

            Vector3 position = VisibilityPosition;
            Gizmos.color = Application.isPlaying
                ? (currentlyHiddenByFog ? targetHiddenColor : targetVisibleColor)
                : targetEditModeColor;

            Gizmos.DrawWireSphere(position, targetGizmoRadiusMeters);

            if (Application.isPlaying && currentlyHiddenByFog)
            {
                Gizmos.DrawSphere(position, targetGizmoRadiusMeters * 0.25f);
            }
        }
    }
}
