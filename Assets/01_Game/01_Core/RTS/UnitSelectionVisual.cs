#region

using UnityEngine;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Visual toggle for squad-unit selection. Supports a dedicated indicator object
    ///     and optional emission highlighting on renderers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitSelectionVisual : MonoBehaviour
    {
        [SerializeField] private GameObject selectionIndicatorRoot;

        [Header("Emission Highlight")]
        [SerializeField] private bool useEmissionHighlight = true;
        [SerializeField] private Renderer[] emissionRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Color selectedEmissionColor = new(0.22f, 0.95f, 0.45f, 1f);
        [SerializeField] [Min(0f)] private float selectedEmissionIntensity = 1.25f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _propertyBlock;

        private bool _isSelected;
        private bool _initialized;

        private void Awake()
        {
            EnsureInitialized();

            ApplyVisualState(false);
        }

        public void SetSelected(bool isSelected)
        {
            var alreadyInitialized = _initialized;
            EnsureInitialized();

            if (alreadyInitialized && _isSelected == isSelected) return;

            _isSelected = isSelected;
            ApplyVisualState(isSelected);
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;

            _propertyBlock ??= new MaterialPropertyBlock();

            if (emissionRenderers == null || emissionRenderers.Length == 0)
                emissionRenderers = GetComponentsInChildren<Renderer>(true);

            _initialized = true;
        }

        private void ApplyVisualState(bool isSelected)
        {
            EnsureInitialized();

            if (selectionIndicatorRoot != null) selectionIndicatorRoot.SetActive(isSelected);

            if (!useEmissionHighlight || emissionRenderers == null || emissionRenderers.Length == 0 ||
                _propertyBlock == null) return;

            var emissionColor = isSelected
                ? selectedEmissionColor * Mathf.Max(0f, selectedEmissionIntensity)
                : Color.black;

            for (var i = 0; i < emissionRenderers.Length; i++)
            {
                var renderer = emissionRenderers[i];
                if (renderer == null || !SupportsEmission(renderer)) continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(EmissionColorId, emissionColor);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private static bool SupportsEmission(Renderer renderer)
        {
            if (renderer == null) return false;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0) return false;

            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material != null && material.HasProperty(EmissionColorId)) return true;
            }

            return false;
        }
    }
}
