using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Zombera.Systems;

namespace Zombera.BuildingSystem
{
    /// <summary>
    /// Shows a transparent build ghost that follows the cursor and indicates validity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildGhostPreview : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Source")]
        [SerializeField] private GameObject defaultPreviewPrefab;
        [SerializeField] private BaseManager baseManager;
        [SerializeField] private Camera worldCamera;

        [Header("Placement")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask blockingMask = ~0;
        [SerializeField, Min(0.1f)] private float maxRayDistance = 1000f;
        [SerializeField] private bool enablePreviewOnStart;
        [SerializeField] private bool allowRotateInput = true;
        [SerializeField] private Key rotateLeftKey = Key.Q;
        [SerializeField] private Key rotateRightKey = Key.E;
        [SerializeField, Min(1f)] private float manualRotationStep = 90f;
        [SerializeField, Min(0f)] private float overlapValidationLift = 0.03f;
        [SerializeField] private bool ignoreTerrainColliders = true;
        [SerializeField, Min(0.1f)] private float fallbackGridSize = 2f;
        [SerializeField, Min(1f)] private float fallbackRotationStep = 90f;

        [Header("Ghost Visual")]
        [SerializeField] private Material ghostMaterial;
        [SerializeField] private Color validColor = new Color(0.20f, 0.95f, 0.25f, 0.42f);
        [SerializeField] private Color invalidColor = new Color(0.95f, 0.15f, 0.15f, 0.42f);

        private readonly Collider[] overlapHits = new Collider[64];
        private Renderer[] ghostRenderers = Array.Empty<Renderer>();
        private Collider[] ghostColliders = Array.Empty<Collider>();
        private MaterialPropertyBlock propertyBlock;

        private GameObject previewSourcePrefab;
        private GameObject ghostInstance;
        private Material runtimeGhostMaterial;
        private BoxCollider primaryGhostCollider;
        private Collider currentGroundCollider;

        private float currentYawDegrees;
        private bool previewActive;
        private bool isCurrentPlacementValid;

        public bool IsPreviewActive => previewActive;
        public bool IsCurrentPlacementValid => isCurrentPlacementValid;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (baseManager == null)
            {
                baseManager = FindFirstObjectByType<BaseManager>();
            }

            propertyBlock = new MaterialPropertyBlock();

            if (enablePreviewOnStart && defaultPreviewPrefab != null)
            {
                BeginPreview(defaultPreviewPrefab);
            }
        }

        private void OnDisable()
        {
            if (ghostInstance != null)
            {
                ghostInstance.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
            }

            if (runtimeGhostMaterial != null)
            {
                Destroy(runtimeGhostMaterial);
                runtimeGhostMaterial = null;
            }
        }

        private void Update()
        {
            if (!previewActive)
            {
                return;
            }

            if (allowRotateInput)
            {
                HandleRotationInput();
            }

            UpdateGhostTransformAndValidity();
        }

        public void BeginPreview(GameObject sourcePrefab)
        {
            if (sourcePrefab == null)
            {
                return;
            }

            previewSourcePrefab = sourcePrefab;
            RebuildGhostInstance();
            previewActive = ghostInstance != null;

            if (ghostInstance != null)
            {
                ghostInstance.SetActive(true);
            }
        }

        public void EndPreview()
        {
            previewActive = false;

            if (ghostInstance != null)
            {
                ghostInstance.SetActive(false);
            }
        }

        public void SetPreviewPrefab(GameObject sourcePrefab)
        {
            if (sourcePrefab == null)
            {
                return;
            }

            bool wasActive = previewActive;
            previewSourcePrefab = sourcePrefab;
            RebuildGhostInstance();
            previewActive = wasActive;

            if (ghostInstance != null)
            {
                ghostInstance.SetActive(previewActive);
            }
        }

        public void SetPreviewActive(bool isActive)
        {
            if (isActive)
            {
                GameObject source = previewSourcePrefab != null ? previewSourcePrefab : defaultPreviewPrefab;
                BeginPreview(source);
                return;
            }

            EndPreview();
        }

        public void RotateByStep(float yawStepDegrees)
        {
            currentYawDegrees += yawStepDegrees;
        }

        public bool TryGetPlacementPose(out Vector3 position, out Quaternion rotation, out bool isValid)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            isValid = false;

            if (!previewActive || ghostInstance == null || !ghostInstance.activeSelf)
            {
                return false;
            }

            position = ghostInstance.transform.position;
            rotation = ghostInstance.transform.rotation;
            isValid = isCurrentPlacementValid;
            return true;
        }

        public void SetWorldCamera(Camera camera)
        {
            if (camera != null)
            {
                worldCamera = camera;
            }
        }

        private void HandleRotationInput()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current[rotateLeftKey].wasPressedThisFrame)
            {
                RotateByStep(-manualRotationStep);
            }

            if (Keyboard.current[rotateRightKey].wasPressedThisFrame)
            {
                RotateByStep(manualRotationStep);
            }
        }

        private void UpdateGhostTransformAndValidity()
        {
            if (ghostInstance == null)
            {
                GameObject source = previewSourcePrefab != null ? previewSourcePrefab : defaultPreviewPrefab;
                if (source == null)
                {
                    previewActive = false;
                    return;
                }

                BeginPreview(source);
                if (ghostInstance == null)
                {
                    previewActive = false;
                    return;
                }
            }

            if (!TryGetGroundPoint(out Vector3 worldPoint))
            {
                ghostInstance.SetActive(false);
                isCurrentPlacementValid = false;
                return;
            }

            if (!ghostInstance.activeSelf)
            {
                ghostInstance.SetActive(true);
            }

            Vector3 snappedPosition = GetSnappedPosition(worldPoint);
            float snappedYaw = GetSnappedYaw(currentYawDegrees);
            Quaternion snappedRotation = Quaternion.Euler(0f, snappedYaw, 0f);

            ghostInstance.transform.SetPositionAndRotation(snappedPosition, snappedRotation);

            isCurrentPlacementValid = EvaluatePlacementValidity();
            ApplyGhostColor(isCurrentPlacementValid ? validColor : invalidColor);
        }

        private Vector3 GetSnappedPosition(Vector3 worldPoint)
        {
            if (baseManager != null)
            {
                return baseManager.GetSnappedBuildPosition(worldPoint);
            }

            float step = Mathf.Max(0.1f, fallbackGridSize);
            worldPoint.x = Mathf.Round(worldPoint.x / step) * step;
            worldPoint.z = Mathf.Round(worldPoint.z / step) * step;
            return worldPoint;
        }

        private float GetSnappedYaw(float yawDegrees)
        {
            if (baseManager != null)
            {
                return baseManager.GetSnappedBuildYaw(yawDegrees);
            }

            float step = Mathf.Max(1f, fallbackRotationStep);
            return Mathf.Round(yawDegrees / step) * step;
        }

        private bool TryGetGroundPoint(out Vector3 worldPoint)
        {
            worldPoint = default;
            currentGroundCollider = null;

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera == null || Mouse.current == null)
            {
                return false;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = worldCamera.ScreenPointToRay(mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                worldPoint = hit.point;
                currentGroundCollider = hit.collider;
                return true;
            }

            Plane fallbackPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (fallbackPlane.Raycast(ray, out float distance))
            {
                worldPoint = ray.GetPoint(distance);
                return true;
            }

            return false;
        }

        private bool EvaluatePlacementValidity()
        {
            if (ghostInstance == null)
            {
                return false;
            }

            if (primaryGhostCollider == null)
            {
                return true;
            }

            Vector3 worldCenter = ghostInstance.transform.TransformPoint(primaryGhostCollider.center) + Vector3.up * overlapValidationLift;
            Vector3 halfExtents = Vector3.Scale(primaryGhostCollider.size * 0.5f, ghostInstance.transform.lossyScale);

            int hitCount = Physics.OverlapBoxNonAlloc(
                worldCenter,
                halfExtents,
                overlapHits,
                ghostInstance.transform.rotation,
                blockingMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = overlapHits[i];
                overlapHits[i] = null;

                if (hit == null)
                {
                    continue;
                }

                if (IsGhostCollider(hit))
                {
                    continue;
                }

                if (ignoreTerrainColliders && hit is TerrainCollider)
                {
                    continue;
                }

                if (currentGroundCollider != null && hit == currentGroundCollider)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private bool IsGhostCollider(Collider candidate)
        {
            for (int i = 0; i < ghostColliders.Length; i++)
            {
                if (ghostColliders[i] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildGhostInstance()
        {
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
            }

            GameObject source = previewSourcePrefab != null ? previewSourcePrefab : defaultPreviewPrefab;
            if (source == null)
            {
                return;
            }

            ghostInstance = Instantiate(source);
            ghostInstance.name = source.name + "_GhostPreview";

            SetLayerRecursively(ghostInstance, LayerMask.NameToLayer("Ignore Raycast"));

            MonoBehaviour[] behaviours = ghostInstance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                {
                    behaviours[i].enabled = false;
                }
            }

            ghostColliders = ghostInstance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < ghostColliders.Length; i++)
            {
                ghostColliders[i].enabled = false;
            }

            primaryGhostCollider = ghostInstance.GetComponent<BoxCollider>();
            if (primaryGhostCollider == null)
            {
                primaryGhostCollider = ghostInstance.GetComponentInChildren<BoxCollider>(true);
            }

            ghostRenderers = ghostInstance.GetComponentsInChildren<Renderer>(true);
            Material resolvedMaterial = ResolveGhostMaterial();
            for (int i = 0; i < ghostRenderers.Length; i++)
            {
                Renderer renderer = ghostRenderers[i];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                if (resolvedMaterial == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    renderer.sharedMaterial = resolvedMaterial;
                    continue;
                }

                for (int m = 0; m < materials.Length; m++)
                {
                    materials[m] = resolvedMaterial;
                }

                renderer.sharedMaterials = materials;
            }

            ApplyGhostColor(validColor);
            ghostInstance.SetActive(previewActive);
        }

        private Material ResolveGhostMaterial()
        {
            if (ghostMaterial != null)
            {
                return ghostMaterial;
            }

            if (runtimeGhostMaterial != null)
            {
                return runtimeGhostMaterial;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null)
            {
                runtimeGhostMaterial = new Material(urpLit);
                runtimeGhostMaterial.SetFloat("_Surface", 1f);
                runtimeGhostMaterial.SetFloat("_Blend", 0f);
                runtimeGhostMaterial.SetFloat("_ZWrite", 0f);
                runtimeGhostMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                runtimeGhostMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                runtimeGhostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                runtimeGhostMaterial.renderQueue = (int)RenderQueue.Transparent;
                runtimeGhostMaterial.SetColor("_BaseColor", validColor);
                return runtimeGhostMaterial;
            }

            Shader standard = Shader.Find("Standard");
            if (standard != null)
            {
                runtimeGhostMaterial = new Material(standard);
                runtimeGhostMaterial.SetFloat("_Mode", 3f);
                runtimeGhostMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                runtimeGhostMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                runtimeGhostMaterial.SetInt("_ZWrite", 0);
                runtimeGhostMaterial.DisableKeyword("_ALPHATEST_ON");
                runtimeGhostMaterial.EnableKeyword("_ALPHABLEND_ON");
                runtimeGhostMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                runtimeGhostMaterial.renderQueue = (int)RenderQueue.Transparent;
                runtimeGhostMaterial.SetColor("_Color", validColor);
                return runtimeGhostMaterial;
            }

            return null;
        }

        private void ApplyGhostColor(Color color)
        {
            if (ghostRenderers == null || ghostRenderers.Length == 0)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            for (int i = 0; i < ghostRenderers.Length; i++)
            {
                Renderer renderer = ghostRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                propertyBlock.Clear();

                Material material = renderer.sharedMaterial;
                if (material != null && material.HasProperty(BaseColorId))
                {
                    propertyBlock.SetColor(BaseColorId, color);
                }

                if (material != null && material.HasProperty(ColorId))
                {
                    propertyBlock.SetColor(ColorId, color);
                }

                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null || layer < 0)
            {
                return;
            }

            root.layer = layer;

            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                SetLayerRecursively(transform.GetChild(i).gameObject, layer);
            }
        }
    }
}
