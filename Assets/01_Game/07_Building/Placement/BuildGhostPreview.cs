#region

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Zombera.Systems;

#endregion

// ReSharper disable Unity.PerformanceCriticalCodeInvocation
// ReSharper disable Unity.PerformanceCriticalCodeNullComparison
// ReSharper disable Unity.PerformanceCriticalCodeCameraMain
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InvertIf
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable MergeIntoPattern

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Shows a transparent build ghost that follows the cursor and indicates validity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildGhostPreview : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int ZWriteFloatId = Shader.PropertyToID("_ZWrite");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");

        [Header("Source")] [SerializeField] private GameObject defaultPreviewPrefab;

        [SerializeField] private BaseManager baseManager;
        [SerializeField] private Camera worldCamera;

        [Header("Placement")] [SerializeField] private LayerMask groundMask = ~0;

        [SerializeField] private LayerMask blockingMask = ~0;
        [SerializeField] [Min(0.1f)] private float maxRayDistance = 1000f;
        [SerializeField] private bool enablePreviewOnStart;
        [SerializeField] private bool allowRotateInput = true;
        [SerializeField] private Key rotateLeftKey = Key.Q;
        [SerializeField] private Key rotateRightKey = Key.R;
        [SerializeField] [Min(1f)] private float manualRotationStep = 90f;
        [SerializeField] [Min(0f)] private float overlapValidationLift = 0.03f;
        [SerializeField] private bool ignoreTerrainColliders = true;
        [SerializeField] [Min(0.1f)] private float fallbackGridSize = 2f;
        [SerializeField] [Min(1f)] private float fallbackRotationStep = 90f;
        [SerializeField]
        [Tooltip("Used only if the cursor raycast doesn't hit Ground Mask.")]
        private float fallbackGroundPlaneY = 0f;

        [Header("Ghost Visual")] [SerializeField]
        private Material ghostMaterial;

        [SerializeField] private Color validColor = new(0.20f, 0.95f, 0.25f, 0.42f);
        [SerializeField] private Color invalidColor = new(0.95f, 0.15f, 0.15f, 0.42f);

        private readonly Collider[] _overlapHits = new Collider[64];
        private Collider _currentGroundCollider;

        private float _currentYawDegrees;
        private Collider[] _ghostColliders = Array.Empty<Collider>();
        private GameObject _ghostInstance;
        private Renderer[] _ghostRenderers = Array.Empty<Renderer>();

        private GameObject _previewSourcePrefab;
        private BoxCollider _primaryGhostCollider;
        private MaterialPropertyBlock _propertyBlock;
        private Material _runtimeGhostMaterial;

        public bool IsPreviewActive { get; private set; }

        public bool IsCurrentPlacementValid { get; private set; }

        private void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;

            baseManager = ResolveBaseManagerInOwnScene(baseManager);

            _propertyBlock = new MaterialPropertyBlock();

            if (enablePreviewOnStart && defaultPreviewPrefab != null) BeginPreview(defaultPreviewPrefab);
        }

        private void OnValidate()
        {
            baseManager = ResolveBaseManagerInOwnScene(baseManager);
        }

        private BaseManager ResolveBaseManagerInOwnScene(BaseManager current)
        {
            if (current != null && current.gameObject.scene == gameObject.scene)
                return current;

            var allManagers = FindObjectsByType<BaseManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < allManagers.Length; i++)
            {
                var manager = allManagers[i];
                if (manager != null && manager.gameObject.scene == gameObject.scene)
                    return manager;
            }

            return null;
        }

        private void Update()
        {
            if (!IsPreviewActive) return;

            if (allowRotateInput) HandleRotationInput();

            UpdateGhostTransformAndValidity();
        }

        private void OnDisable()
        {
            if (_ghostInstance != null) _ghostInstance.SetActive(false);
        }

        private void OnDestroy()
        {
            DestroyGhostInstance();
            DestroyRuntimeGhostMaterial();
        }

        private void DestroyGhostInstance()
        {
            if (_ghostInstance == null) return;

            Destroy(_ghostInstance);
            _ghostInstance = null;
        }

        private void DestroyRuntimeGhostMaterial()
        {
            if (_runtimeGhostMaterial == null) return;

            Destroy(_runtimeGhostMaterial);
            _runtimeGhostMaterial = null;
        }

        public void BeginPreview(GameObject sourcePrefab)
        {
            if (sourcePrefab == null) return;

            _previewSourcePrefab = sourcePrefab;
            RebuildGhostInstance();
            IsPreviewActive = _ghostInstance != null;

            if (_ghostInstance != null) _ghostInstance.SetActive(true);
        }

        public void EndPreview()
        {
            IsPreviewActive = false;

            if (_ghostInstance != null) _ghostInstance.SetActive(false);
        }

        public void SetPreviewPrefab(GameObject sourcePrefab)
        {
            if (sourcePrefab == null) return;

            var wasActive = IsPreviewActive;
            _previewSourcePrefab = sourcePrefab;
            RebuildGhostInstance();
            IsPreviewActive = wasActive;

            if (_ghostInstance != null) _ghostInstance.SetActive(IsPreviewActive);
        }

        public void SetPreviewActive(bool isActive)
        {
            if (!isActive)
                EndPreview();
            else
                BeginPreview(_previewSourcePrefab != null ? _previewSourcePrefab : defaultPreviewPrefab);
        }

        public void RotateByStep(float yawStepDegrees)
        {
            _currentYawDegrees += yawStepDegrees;
        }

        public bool TryGetPlacementPose(out Vector3 position, out Quaternion rotation, out bool isValid)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            isValid = false;

            if (!IsPreviewActive || _ghostInstance == null || !_ghostInstance.activeSelf) return false;

            position = _ghostInstance.transform.position;
            rotation = _ghostInstance.transform.rotation;
            isValid = IsCurrentPlacementValid;
            return true;
        }

        public void SetWorldCamera(Camera targetCamera)
        {
            if (targetCamera != null) worldCamera = targetCamera;
        }

        private void HandleRotationInput()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current[rotateLeftKey].wasPressedThisFrame) RotateByStep(-manualRotationStep);

            if (Keyboard.current[rotateRightKey].wasPressedThisFrame) RotateByStep(manualRotationStep);
        }

        private void UpdateGhostTransformAndValidity()
        {
            if (_ghostInstance == null && !TryEnsureGhostInstance())
            {
                IsPreviewActive = false;
                return;
            }

            if (!TryGetGroundPoint(out var worldPoint))
            {
                _ghostInstance.SetActive(false);
                IsCurrentPlacementValid = false;
                return;
            }

            if (!_ghostInstance.activeSelf) _ghostInstance.SetActive(true);

            var snappedPosition = GetSnappedPosition(worldPoint);
            var snappedYaw = GetSnappedYaw(_currentYawDegrees);
            var snappedRotation = Quaternion.Euler(0f, snappedYaw, 0f);

            _ghostInstance.transform.SetPositionAndRotation(snappedPosition, snappedRotation);

            IsCurrentPlacementValid = EvaluatePlacementValidity();
            ApplyGhostColor(IsCurrentPlacementValid ? validColor : invalidColor);
        }

        private bool TryEnsureGhostInstance()
        {
            if (_ghostInstance != null) return true;

            var source = _previewSourcePrefab != null ? _previewSourcePrefab : defaultPreviewPrefab;
            if (source == null) return false;

            BeginPreview(source);
            return _ghostInstance != null;
        }

        private Vector3 GetSnappedPosition(Vector3 worldPoint)
        {
            if (baseManager != null) return baseManager.GetSnappedBuildPosition(worldPoint);

            var step = Mathf.Max(0.1f, fallbackGridSize);
            worldPoint.x = Mathf.Round(worldPoint.x / step) * step;
            worldPoint.z = Mathf.Round(worldPoint.z / step) * step;
            return worldPoint;
        }

        private float GetSnappedYaw(float yawDegrees)
        {
            if (baseManager != null) return baseManager.GetSnappedBuildYaw(yawDegrees);

            var step = Mathf.Max(1f, fallbackRotationStep);
            return Mathf.Round(yawDegrees / step) * step;
        }

        private bool TryGetGroundPoint(out Vector3 worldPoint)
        {
            worldPoint = default;
            _currentGroundCollider = null;

            if (worldCamera == null) return false;

            if (!CursorService.TryGetGameplayPointerScreenPosition(out var mousePosition)) return false;

            var ray = worldCamera.ScreenPointToRay(mousePosition);

            if (Physics.Raycast(ray, out var hit, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                worldPoint = hit.point;
                _currentGroundCollider = hit.collider;
                return true;
            }

            var fallbackPlane = new Plane(Vector3.up, new Vector3(0f, fallbackGroundPlaneY, 0f));
            if (!fallbackPlane.Raycast(ray, out var distance)) return false;

            worldPoint = ray.GetPoint(distance);
            return true;
        }

        private bool EvaluatePlacementValidity()
        {
            if (_ghostInstance == null) return false;

            if (_primaryGhostCollider == null) return true;

            var worldCenter = _ghostInstance.transform.TransformPoint(_primaryGhostCollider.center) +
                              Vector3.up * overlapValidationLift;
            var halfExtents = Vector3.Scale(_primaryGhostCollider.size * 0.5f, _ghostInstance.transform.lossyScale);

            var hitCount = Physics.OverlapBoxNonAlloc(
                worldCenter,
                halfExtents,
                _overlapHits,
                _ghostInstance.transform.rotation,
                blockingMask,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < hitCount; i++)
            {
                var hit = _overlapHits[i];
                _overlapHits[i] = null;

                if (hit == null) continue;

                if (IsGhostCollider(hit)) continue;

                if (ignoreTerrainColliders && hit is TerrainCollider) continue;

                if (_currentGroundCollider != null && hit == _currentGroundCollider) continue;

                return false;
            }

            return true;
        }

        private bool IsGhostCollider(Collider candidate)
        {
            if (_ghostColliders == null || _ghostColliders.Length == 0) return false;

            for (var i = 0; i < _ghostColliders.Length; i++)
                if (_ghostColliders[i] == candidate)
                    return true;

            return false;
        }

        private void RebuildGhostInstance()
        {
            if (_ghostInstance != null)
            {
                Destroy(_ghostInstance);
                _ghostInstance = null;
            }

            var source = _previewSourcePrefab != null ? _previewSourcePrefab : defaultPreviewPrefab;
            if (source == null) return;

            _ghostInstance = Instantiate(source);
            _ghostInstance.name = source.name + "_GhostPreview";

            SetLayerRecursively(_ghostInstance, LayerMask.NameToLayer("Ignore Raycast"));

            var behaviours = _ghostInstance.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null) continue;

                behaviour.enabled = false;
            }

            _ghostColliders = _ghostInstance.GetComponentsInChildren<Collider>(true);
            foreach (var ghostCollider in _ghostColliders) ghostCollider.enabled = false;

            _primaryGhostCollider = _ghostInstance.GetComponent<BoxCollider>();
            if (_primaryGhostCollider == null)
                _primaryGhostCollider = _ghostInstance.GetComponentInChildren<BoxCollider>(true);

            _ghostRenderers = _ghostInstance.GetComponentsInChildren<Renderer>(true);
            var resolvedMaterial = ResolveGhostMaterial();
            foreach (var ghostRenderer in _ghostRenderers)
            {
                ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
                ghostRenderer.receiveShadows = false;

                if (resolvedMaterial == null) continue;

                var materials = ghostRenderer.sharedMaterials;
                if (materials == null || materials.Length == 0)
                    ghostRenderer.sharedMaterial = resolvedMaterial;
                else
                    for (var m = 0; m < materials.Length; m++)
                        materials[m] = resolvedMaterial;

                if (materials != null && materials.Length > 0) ghostRenderer.sharedMaterials = materials;
            }

            ApplyGhostColor(validColor);
            _ghostInstance.SetActive(IsPreviewActive);
        }

        private Material ResolveGhostMaterial()
        {
            if (ghostMaterial != null) return ghostMaterial;

            if (_runtimeGhostMaterial != null) return _runtimeGhostMaterial;

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null)
            {
                _runtimeGhostMaterial = new Material(urpLit);
                _runtimeGhostMaterial.SetFloat(SurfaceId, 1f);
                _runtimeGhostMaterial.SetFloat(BlendId, 0f);
                _runtimeGhostMaterial.SetFloat(ZWriteFloatId, 0f);
                _runtimeGhostMaterial.SetInt(SrcBlendId, (int)BlendMode.SrcAlpha);
                _runtimeGhostMaterial.SetInt(DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
                _runtimeGhostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _runtimeGhostMaterial.renderQueue = (int)RenderQueue.Transparent;
                _runtimeGhostMaterial.SetColor(BaseColorId, validColor);
                return _runtimeGhostMaterial;
            }

            var standard = Shader.Find("Standard");
            if (standard != null)
            {
                _runtimeGhostMaterial = new Material(standard);
                _runtimeGhostMaterial.SetFloat(ModeId, 3f);
                _runtimeGhostMaterial.SetInt(SrcBlendId, (int)BlendMode.SrcAlpha);
                _runtimeGhostMaterial.SetInt(DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
                _runtimeGhostMaterial.SetInt(ZWriteFloatId, 0);
                _runtimeGhostMaterial.DisableKeyword("_ALPHATEST_ON");
                _runtimeGhostMaterial.EnableKeyword("_ALPHABLEND_ON");
                _runtimeGhostMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                _runtimeGhostMaterial.renderQueue = (int)RenderQueue.Transparent;
                _runtimeGhostMaterial.SetColor(ColorId, validColor);
                return _runtimeGhostMaterial;
            }

            return null;
        }

        private void ApplyGhostColor(Color color)
        {
            if (_ghostRenderers == null || _ghostRenderers.Length == 0) return;

            _propertyBlock ??= new MaterialPropertyBlock();

            foreach (var ghostRenderer in _ghostRenderers)
            {
                if (ghostRenderer == null) continue;

                _propertyBlock.Clear();

                var material = ghostRenderer.sharedMaterial;
                if (material != null && material.HasProperty(BaseColorId)) _propertyBlock.SetColor(BaseColorId, color);

                if (material != null && material.HasProperty(ColorId)) _propertyBlock.SetColor(ColorId, color);

                ghostRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null || layer < 0) return;

            root.layer = layer;

            var transform = root.transform;
            foreach (Transform child in transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}