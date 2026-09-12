#region

using System;
using UnityEngine;
using Zombera.UI.Menus.CharacterCreation;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.Characters
{
    /// <summary>
    ///     Visual randomizer for humanoid rig prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcAppearanceVariantSpawner : MonoBehaviour
    {
        [SerializeField] private bool applyOnEnable;
        [SerializeField] private bool applyOnlyOnce = true;
        [SerializeField] private Vector2 uniformScaleRange = new(0.98f, 1.04f);
        [SerializeField] private bool applyRendererTint;
        [SerializeField] private Color[] rendererTintPalette =
        {
            new(0.95f, 0.92f, 0.86f, 1f),
            new(0.84f, 0.82f, 0.78f, 1f),
            new(0.76f, 0.74f, 0.70f, 1f)
        };
        [SerializeField] [Range(0f, 1f)] private float tintStrength = 0.12f;
        [SerializeField] private bool logRandomization;

        private bool _hasApplied;

        private void OnEnable()
        {
            if (applyOnEnable) ApplyRandomAppearanceNow();
        }

        public void ApplyRandomAppearanceNow(bool force = false)
        {
            if (!force && applyOnlyOnce && _hasApplied) return;

            var profile = AppearanceProfileService.GenerateRandomProfile();
            
            // Map legacy inspector ranges to profile DNA
            var scale = Random.Range(uniformScaleRange.x, uniformScaleRange.y);
            var heightDna = Mathf.InverseLerp(0.9f, 1.1f, scale);
            var heightEntry = profile.bodyValues.Find(e => e.dnaName == "height");
            if (heightEntry != null) heightEntry.dnaValue = heightDna;

            AppearanceProfileService.TryApplyProfile(gameObject, profile);

            _hasApplied = true;

            if (logRandomization)
                Debug.Log($"[NpcAppearanceVariantSpawner] Applied randomized humanoid visual to '{name}'.", this);
        }
    }
}
