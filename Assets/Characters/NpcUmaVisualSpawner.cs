using System;
using System.Collections.Generic;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;

namespace Zombera.Characters
{
    /// <summary>
    /// Runtime UMA visual randomizer for squad members and recruitable NPC-style units.
    /// Keeps visual generation separate from gameplay spawning logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcUmaVisualSpawner : MonoBehaviour
    {
        private static readonly string[] DefaultPreferredRaceNames =
        {
            "HumanMale",
            "HumanFemale",
            "Human Male",
            "Human Female",
            "HumanMaleDCS",
            "HumanFemaleDCS"
        };

        private static readonly string[] SafeVariationDnaKeyFragments =
        {
            "height",
            "weight",
            "muscle",
            "belly",
            "waist",
            "hip",
            "thigh",
            "calf"
        };

        private static readonly string[] LockedProportionDnaKeyFragments =
        {
            "armlength",
            "upperarmlength",
            "lowerarmlength",
            "forearmlength",
            "leglength",
            "upperleglength",
            "lowerleglength",
            "torsolength",
            "torsoheight",
            "spinelength",
            "headsize",
            "handsize",
            "footsize",
            "necklength"
        };

        [Header("UMA")]
        [SerializeField] private DynamicCharacterAvatar avatar;
        [SerializeField] private bool applyOnEnable;
        [SerializeField] private bool applyOnlyOnce = true;

        [Header("Randomization")]
        [SerializeField] private bool randomizeDna = true;
        [SerializeField] private Vector2 dnaRandomRange = new Vector2(0.45f, 0.55f);
        [SerializeField] private Vector2 safeBodyDnaRange = new Vector2(0.45f, 0.55f);
        [SerializeField] private Vector2 lockedProportionDnaRange = new Vector2(0.49f, 0.51f);
        [SerializeField] private bool keepUnknownDnaNearNeutral = true;
        [SerializeField] private Vector2 neutralFallbackDnaRange = new Vector2(0.47f, 0.53f);
        [SerializeField] private bool randomizeSharedColors = true;
        [SerializeField] private List<string> preferredRaceNames = new List<string>(DefaultPreferredRaceNames);

        [Header("Color Palettes")]
        [SerializeField] private Color[] skinPalette =
        {
            new Color(0.95f, 0.82f, 0.77f, 1f),
            new Color(0.82f, 0.64f, 0.52f, 1f),
            new Color(0.67f, 0.49f, 0.37f, 1f),
            new Color(0.45f, 0.33f, 0.24f, 1f),
            new Color(0.30f, 0.22f, 0.16f, 1f)
        };

        [SerializeField] private Color[] hairPalette =
        {
            new Color(0.16f, 0.10f, 0.06f, 1f),
            new Color(0.28f, 0.18f, 0.10f, 1f),
            new Color(0.45f, 0.31f, 0.18f, 1f),
            new Color(0.70f, 0.58f, 0.40f, 1f),
            new Color(0.12f, 0.12f, 0.13f, 1f)
        };

        [SerializeField] private Color[] eyePalette =
        {
            new Color(0.24f, 0.40f, 0.80f, 1f),
            new Color(0.23f, 0.55f, 0.34f, 1f),
            new Color(0.58f, 0.39f, 0.21f, 1f),
            new Color(0.48f, 0.47f, 0.52f, 1f)
        };

        [Header("Debug")]
        [SerializeField] private bool logRandomization;

        private bool hasApplied;

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void OnEnable()
        {
            AutoResolveReferences();

            if (applyOnEnable)
            {
                ApplyRandomAppearanceNow();
            }
        }

        public void ApplyRandomAppearanceNow(bool force = false)
        {
            AutoResolveReferences();

            if (avatar == null)
            {
                return;
            }

            if (!force && applyOnlyOnce && hasApplied)
            {
                return;
            }

            System.Random random = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));

            try
            {
                avatar.loadFileOnStart = false;

                string raceName = ResolveRandomRaceName(random);
                if (!string.IsNullOrWhiteSpace(raceName))
                {
                    avatar.ChangeRace(raceName, true);
                }

                if (randomizeDna)
                {
                    ApplyRandomDna(random);
                }

                if (randomizeSharedColors)
                {
                    ApplyRandomSharedColors(random);
                }

                avatar.enabled = true;
                avatar.BuildCharacter(true, false);
                hasApplied = true;

                if (logRandomization)
                {
                    Debug.Log($"[NpcUmaVisualSpawner] Applied randomized UMA visual to '{name}' (Race='{raceName}').", this);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[NpcUmaVisualSpawner] Failed to randomize UMA visual on '{name}'. Reason: {exception.Message}", this);
            }
        }

        private void AutoResolveReferences()
        {
            if (avatar == null)
            {
                avatar = GetComponent<DynamicCharacterAvatar>();
            }
        }

        private string ResolveRandomRaceName(System.Random random)
        {
            UMAContextBase context = UMAContextBase.Instance;
            if (context == null)
            {
                return string.Empty;
            }

            RaceData[] races = context.GetAllRaces();
            if (races == null || races.Length == 0)
            {
                return string.Empty;
            }

            List<string> availableRaces = new List<string>(races.Length);
            for (int i = 0; i < races.Length; i++)
            {
                RaceData race = races[i];
                if (race == null || string.IsNullOrWhiteSpace(race.raceName))
                {
                    continue;
                }

                availableRaces.Add(race.raceName);
            }

            if (availableRaces.Count == 0)
            {
                return string.Empty;
            }

            List<string> candidateRaceNames = GetCandidateRaceNames(preferredRaceNames);
            List<string> preferredMatches = new List<string>(candidateRaceNames.Count);

            for (int i = 0; i < candidateRaceNames.Count; i++)
            {
                string candidate = candidateRaceNames[i];
                string candidateKey = NormalizeRaceKey(candidate);

                for (int r = 0; r < availableRaces.Count; r++)
                {
                    string available = availableRaces[r];
                    if (NormalizeRaceKey(available) == candidateKey && !preferredMatches.Contains(available))
                    {
                        preferredMatches.Add(available);
                        break;
                    }
                }
            }

            List<string> selectionPool = preferredMatches.Count > 0 ? preferredMatches : availableRaces;
            return selectionPool[random.Next(selectionPool.Count)];
        }

        private void ApplyRandomDna(System.Random random)
        {
            Dictionary<string, DnaSetter> dna;

            try
            {
                dna = avatar.GetDNA();
            }
            catch
            {
                return;
            }

            if (dna == null || dna.Count == 0)
            {
                return;
            }

            Vector2 defaultRange = NormalizeRange(dnaRandomRange, 0.45f, 0.55f);
            Vector2 safeRange = NormalizeRange(safeBodyDnaRange, defaultRange.x, defaultRange.y);
            Vector2 lockedRange = NormalizeRange(lockedProportionDnaRange, 0.49f, 0.51f);
            Vector2 neutralRange = NormalizeRange(neutralFallbackDnaRange, 0.47f, 0.53f);

            foreach (KeyValuePair<string, DnaSetter> pair in dna)
            {
                DnaSetter setter = pair.Value;
                if (setter == null)
                {
                    continue;
                }

                string dnaKey = NormalizeDnaKey(pair.Key);
                Vector2 selectedRange = defaultRange;

                if (ContainsAnyFragment(dnaKey, LockedProportionDnaKeyFragments))
                {
                    selectedRange = lockedRange;
                }
                else if (ContainsAnyFragment(dnaKey, SafeVariationDnaKeyFragments))
                {
                    selectedRange = safeRange;
                }
                else if (keepUnknownDnaNearNeutral)
                {
                    selectedRange = neutralRange;
                }

                float value = Mathf.Lerp(selectedRange.x, selectedRange.y, (float)random.NextDouble());
                setter.Set(value);
            }
        }

        private static Vector2 NormalizeRange(Vector2 range, float fallbackMin, float fallbackMax)
        {
            float min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
            float max = Mathf.Clamp01(Mathf.Max(range.x, range.y));

            if (max <= min)
            {
                min = Mathf.Clamp01(Mathf.Min(fallbackMin, fallbackMax));
                max = Mathf.Clamp01(Mathf.Max(fallbackMin, fallbackMax));
            }

            return new Vector2(min, max);
        }

        private static bool ContainsAnyFragment(string source, string[] fragments)
        {
            if (string.IsNullOrEmpty(source) || fragments == null || fragments.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < fragments.Length; i++)
            {
                string fragment = fragments[i];
                if (string.IsNullOrEmpty(fragment))
                {
                    continue;
                }

                if (source.Contains(fragment))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeDnaKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }

        private void ApplyRandomSharedColors(System.Random random)
        {
            TrySetSharedColor(random, "Skin", skinPalette);
            TrySetSharedColor(random, "Hair", hairPalette);
            TrySetSharedColor(random, "Eyes", eyePalette);
        }

        private void TrySetSharedColor(System.Random random, string colorName, Color[] palette)
        {
            if (palette == null || palette.Length == 0 || string.IsNullOrWhiteSpace(colorName))
            {
                return;
            }

            int index = random.Next(palette.Length);
            Color value = palette[Mathf.Clamp(index, 0, palette.Length - 1)];
            value.a = 1f;
            avatar.SetColor(colorName, value, new Color(0f, 0f, 0f, 0f), 0f, false);
        }

        private static List<string> GetCandidateRaceNames(List<string> configuredCandidates)
        {
            List<string> result = new List<string>();

            if (configuredCandidates != null)
            {
                for (int i = 0; i < configuredCandidates.Count; i++)
                {
                    string candidate = configuredCandidates[i];
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        result.Add(candidate.Trim());
                    }
                }
            }

            if (result.Count > 0)
            {
                return result;
            }

            result.AddRange(DefaultPreferredRaceNames);
            return result;
        }

        private static string NormalizeRaceKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(" ", string.Empty).Trim().ToLowerInvariant();
        }
    }
}
