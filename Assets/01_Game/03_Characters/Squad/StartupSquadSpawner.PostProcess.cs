#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Systems;

#endregion

namespace Zombera.Characters
{
    public sealed partial class StartupSquadSpawner
    {
        // ── Visual Variants ───────────────────────────────────────────

        private static void ApplyRandomizedVisualVariantsToStartupSquad(List<Unit> squadUnits, bool enabled)
        {
            if (!enabled || squadUnits == null) return;

            foreach (var squadUnit in squadUnits) ApplyRandomizedVisualVariant(squadUnit);
        }

        private static void ApplyRandomizedVisualVariant(Unit unit)
        {
            if (unit == null) return;

                var visualSpawner = unit.GetComponent<NpcAppearanceVariantSpawner>();
                if (visualSpawner == null) visualSpawner = unit.gameObject.AddComponent<NpcAppearanceVariantSpawner>();
            visualSpawner.ApplyRandomAppearanceNow(true);
            ForceEnableRenderers(unit.gameObject);
        }

        private static void DisableRenderers(GameObject root)
        {
            if (root == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;

                if (r.enabled) r.enabled = false;
            }
        }

        private static void ForceEnableRenderers(GameObject root)
        {
            if (root == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;

                if (!r.enabled) r.enabled = true;
            }

            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var s in skinned)
            {
                if (s == null) continue;

                // Helps prevent skinned meshes from being culled incorrectly during initial spawn.
                if (!s.updateWhenOffscreen) s.updateWhenOffscreen = true;
            }
        }

        // ── Skill Tiers ───────────────────────────────────────────────

        private static void ApplyStartupSquadSkillTiers(Unit playerUnit, List<Unit> squadUnits, int[] tiers,
            int[] defaultTiers)
        {
            if (playerUnit == null) return;

            var rosterIndex = 0;
            ApplyUniformSkillTier(playerUnit, ResolveStartupSkillTierForRosterIndex(rosterIndex, tiers, defaultTiers));
            rosterIndex++;

            if (squadUnits == null) return;

            foreach (var unit in squadUnits.Where(unit => unit != null))
            {
                ApplyUniformSkillTier(unit, ResolveStartupSkillTierForRosterIndex(rosterIndex, tiers, defaultTiers));
                rosterIndex++;
            }
        }

        private static int ResolveStartupSkillTierForRosterIndex(int rosterIndex, int[] tiers, int[] defaultTiers)
        {
            var resolved = tiers;
            if (resolved == null || resolved.Length == 0) resolved = defaultTiers;

            if (resolved == null || resolved.Length == 0) return UnitStats.MinSkillLevel;

            var safeIndex = Mathf.Clamp(rosterIndex, 0, resolved.Length - 1);
            var rawTier = resolved[safeIndex];
            return Mathf.Clamp(rawTier, UnitStats.MinSkillLevel, UnitStats.MaxSkillLevel);
        }

        private static void ApplyUniformSkillTier(Unit unit, int tier)
        {
            if (unit == null || unit.Stats == null) return;

            var clampedTier = Mathf.Clamp(tier, UnitStats.MinSkillLevel, UnitStats.MaxSkillLevel);
            var stats = unit.Stats;
            var allSkillTypes = (UnitSkillType[])Enum.GetValues(typeof(UnitSkillType));
            foreach (var skillType in allSkillTypes) stats.SetSkill(skillType, clampedTier);

            unit.Health?.ResetHealthToMax();
        }
    }
}
