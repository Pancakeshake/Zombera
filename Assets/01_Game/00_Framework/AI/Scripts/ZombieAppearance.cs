#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Data;
using Zombera.UI.Menus.CharacterCreation;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.AI
{
    /// <summary>
    ///     Zombie appearance randomizer for non-UMA rigs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZombieAppearance : MonoBehaviour
    {
        [SerializeField] private ZombieVisualProfile fallbackProfile;
        [SerializeField] private List<ZombieVisualProfile> profilePool = new();
        [SerializeField] private bool applyOnEnable;

        private void OnEnable()
        {
            if (applyOnEnable) ApplyRandomAppearance();
        }

        public void ApplyRandomAppearance()
        {
            var profile = AppearanceProfileService.GenerateRandomProfile("Zombie");
            
            // Allow the local palette/scale settings to influence if desired, 
            // but for unification we use the profile.
            // Here we can map ZombieVisualProfile values into the generated profile.
            var visualSettings = ResolveProfile();
            if (visualSettings != null)
            {
                var height = Random.Range(visualSettings.scaleRange.x, visualSettings.scaleRange.y);
                var heightDna = Mathf.InverseLerp(0.9f, 1.1f, height);
                
                var heightEntry = profile.bodyValues.Find(e => e.dnaName == "height");
                if (heightEntry != null) heightEntry.dnaValue = heightDna;

                if (visualSettings.applyRendererTint && visualSettings.rendererTintPalette.Length > 0)
                {
                    var tint = visualSettings.rendererTintPalette[Random.Range(0, visualSettings.rendererTintPalette.Length)];
                    profile.skinColor = Color.Lerp(profile.skinColor, tint, visualSettings.rendererTintStrength);
                }
            }

            AppearanceProfileService.TryApplyProfile(gameObject, profile);
        }

        private ZombieVisualProfile ResolveProfile()
        {
            var validProfiles = new List<ZombieVisualProfile>();
            for (var i = 0; i < profilePool.Count; i++)
            {
                var candidate = profilePool[i];
                if (candidate != null) validProfiles.Add(candidate);
            }

            if (validProfiles.Count > 0)
                return validProfiles[Random.Range(0, validProfiles.Count)];

            return fallbackProfile;
        }
        }
        }
