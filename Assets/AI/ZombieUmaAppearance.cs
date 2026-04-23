using System.Collections.Generic;
using System.Text.RegularExpressions;
using UMA;
using UMA.CharacterSystem;
using UMA.Dynamics;
using UnityEngine;
using UnityEngine.AI;
using Zombera.Characters;
using Zombera.Data;

namespace Zombera.AI
{
    /// <summary>
    /// Applies limited UMA randomization for spawned zombie variants.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZombieUmaAppearance : MonoBehaviour
    {
        [Header("UMA")]
        [SerializeField] private DynamicCharacterAvatar avatar;
        [SerializeField] private ZombieUmaVisualProfile fallbackProfile;
        [SerializeField] private List<ZombieUmaVisualProfile> profilePool = new List<ZombieUmaVisualProfile>();
        [SerializeField] private bool applyOnEnable;
        [Tooltip("When enabled, zombie appearance can load external recipe text/assets. Leave off to avoid invalid recipe crashes.")]
        [SerializeField] private bool allowRecipeLoading;
        [SerializeField] private bool logUmaApplyFailures = true;
        [Tooltip("When enabled, removes UMA physics-avatar generated rig colliders/rigidbodies so zombies keep gameplay collider setup.")]
        [SerializeField] private bool stripUmaPhysicsRigColliders = true;

        [Header("Spawn Recovery")]
        [SerializeField] private UnitController unitController;
        [SerializeField] private NavMeshAgent navMeshAgent;
        private bool suppressFurtherAppearanceChanges;
        private bool loggedUmaApplyFailure;

        private bool callbacksBound;

        private void Awake()
        {
            AutoResolveReferences();
            BindUmaCallbacks();
        }

        private void OnEnable()
        {
            AutoResolveReferences();
            BindUmaCallbacks();

            if (applyOnEnable)
            {
                ApplyRandomAppearance();
            }
        }

        private void OnDisable()
        {
            UnbindUmaCallbacks();
        }

        private void OnDestroy()
        {
            UnbindUmaCallbacks();
        }

        public void ApplyRandomAppearance()
        {
            AutoResolveReferences();

            if (avatar == null)
            {
                return;
            }
            if (suppressFurtherAppearanceChanges)
            {
                return;
            }

            ZombieUmaVisualProfile profile = ResolveProfile();

            if (profile == null)
            {
                return;
            }

            bool loadedRecipe = false;

            // Prefer authored recipe data when enabled; it contains the full visual state.
            if (allowRecipeLoading && TryPickRecipe(profile, out string recipe))
            {
                try
                {
                    recipe = NormalizeRecipeRaceName(recipe);

                    if (HasValidRaceField(recipe))
                    {
                        avatar.loadFileOnStart = false;
                        avatar.LoadFromRecipeString(recipe);
                        avatar.enabled = true;
                        loadedRecipe = true;
                    }
                }
                catch (System.Exception exception)
                {
                    HandleUmaApplyFailure("LoadFromRecipeString", exception);
                }
            }

            if (!loadedRecipe)
            {
                string raceName = ResolveAvailableRaceName(
                    PickRandomNonEmpty(profile.raceNames),
                    "Human Male",
                    "Human Female",
                    "HumanMale",
                    "HumanFemale",
                    "HumanMaleDCS",
                    "HumanFemaleDCS");

                if (!string.IsNullOrWhiteSpace(raceName))
                {
                    avatar.ChangeRace(raceName, true);
                }
            }

            ApplyScale(profile);
        }

        private string NormalizeRecipeRaceName(string recipe)
        {
            if (string.IsNullOrWhiteSpace(recipe))
            {
                return recipe;
            }

            string maleRace = ResolveAvailableRaceName("Human Male", "HumanMale", "HumanMaleDCS");
            string femaleRace = ResolveAvailableRaceName("Human Female", "HumanFemale", "HumanFemaleDCS");

            if (!string.IsNullOrWhiteSpace(maleRace))
            {
                recipe = Regex.Replace(recipe, "\"race\"\\s*:\\s*\"Human\\s*Male(?:\\s*DCS)?\"", $"\"race\":\"{maleRace}\"", RegexOptions.IgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(femaleRace))
            {
                recipe = Regex.Replace(recipe, "\"race\"\\s*:\\s*\"Human\\s*Female(?:\\s*DCS)?\"", $"\"race\":\"{femaleRace}\"", RegexOptions.IgnoreCase);
            }

            return recipe;
        }

        private string ResolveAvailableRaceName(params string[] candidates)
        {
            if (candidates == null || candidates.Length <= 0)
            {
                return string.Empty;
            }

            UMAContextBase context = UMAContextBase.Instance;

            if (context == null)
            {
                return string.Empty;
            }

            RaceData[] races = context.GetAllRaces();

            if (races == null || races.Length <= 0)
            {
                return string.Empty;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];

                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                string candidateNormalized = NormalizeRaceKey(candidate);

                for (int r = 0; r < races.Length; r++)
                {
                    RaceData race = races[r];

                    if (race == null || string.IsNullOrWhiteSpace(race.raceName))
                    {
                        continue;
                    }

                    if (NormalizeRaceKey(race.raceName) == candidateNormalized)
                    {
                        return race.raceName;
                    }
                }
            }

            return string.Empty;
        }

        private static bool HasValidRaceField(string recipe)
        {
            if (string.IsNullOrWhiteSpace(recipe))
            {
                return false;
            }

            return Regex.IsMatch(recipe, "\"race\"\\s*:\\s*\"[^\"]+\"", RegexOptions.IgnoreCase);
        }

        private static string NormalizeRaceKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(" ", string.Empty).Trim().ToLowerInvariant();
        }

        private static string FirstNonEmpty(string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i].Trim();
                }
            }

            return string.Empty;
        }

        private void AutoResolveReferences()
        {
            if (avatar == null)
            {
                avatar = GetComponent<DynamicCharacterAvatar>();
            }

            if (unitController == null)
            {
                unitController = GetComponent<UnitController>();
            }

            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }
        }

        private ZombieUmaVisualProfile ResolveProfile()
        {
            if (profilePool != null)
            {
                int validCount = 0;

                for (int i = 0; i < profilePool.Count; i++)
                {
                    if (profilePool[i] != null)
                    {
                        validCount++;
                    }
                }

                if (validCount > 0)
                {
                    int chosen = Random.Range(0, validCount);

                    for (int i = 0; i < profilePool.Count; i++)
                    {
                        ZombieUmaVisualProfile profile = profilePool[i];

                        if (profile == null)
                        {
                            continue;
                        }

                        if (chosen == 0)
                        {
                            return profile;
                        }

                        chosen--;
                    }
                }
            }

            return fallbackProfile;
        }

        private static string PickRandomNonEmpty(List<string> values)
        {
            if (values == null || values.Count <= 0)
            {
                return string.Empty;
            }

            int validCount = 0;

            for (int i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    validCount++;
                }
            }

            if (validCount <= 0)
            {
                return string.Empty;
            }

            int chosen = Random.Range(0, validCount);

            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i];

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (chosen == 0)
                {
                    return value.Trim();
                }

                chosen--;
            }

            return string.Empty;
        }

        private static bool TryPickRecipe(ZombieUmaVisualProfile profile, out string recipe)
        {
            recipe = string.Empty;

            if (profile == null)
            {
                return false;
            }

            int umaTextRecipeValidCount = 0;
            int assetValidCount = 0;
            int stringValidCount = 0;

            if (profile.umaTextRecipes != null)
            {
                for (int i = 0; i < profile.umaTextRecipes.Count; i++)
                {
                    UMATextRecipe umaTextRecipe = profile.umaTextRecipes[i];

                    if (umaTextRecipe != null && !string.IsNullOrWhiteSpace(umaTextRecipe.recipeString))
                    {
                        umaTextRecipeValidCount++;
                    }
                }
            }

            if (profile.recipeAssets != null)
            {
                for (int i = 0; i < profile.recipeAssets.Count; i++)
                {
                    TextAsset asset = profile.recipeAssets[i];

                    if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
                    {
                        assetValidCount++;
                    }
                }
            }

            if (profile.recipeStrings != null)
            {
                for (int i = 0; i < profile.recipeStrings.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(profile.recipeStrings[i]))
                    {
                        stringValidCount++;
                    }
                }
            }

            int totalValid = umaTextRecipeValidCount + assetValidCount + stringValidCount;

            if (totalValid <= 0)
            {
                return false;
            }

            int chosen = Random.Range(0, totalValid);

            if (chosen < umaTextRecipeValidCount && profile.umaTextRecipes != null)
            {
                for (int i = 0; i < profile.umaTextRecipes.Count; i++)
                {
                    UMATextRecipe umaTextRecipe = profile.umaTextRecipes[i];

                    if (umaTextRecipe == null || string.IsNullOrWhiteSpace(umaTextRecipe.recipeString))
                    {
                        continue;
                    }

                    if (chosen == 0)
                    {
                        recipe = umaTextRecipe.recipeString;
                        return true;
                    }

                    chosen--;
                }
            }
            else if (chosen < umaTextRecipeValidCount + assetValidCount && profile.recipeAssets != null)
            {
                chosen -= umaTextRecipeValidCount;

                for (int i = 0; i < profile.recipeAssets.Count; i++)
                {
                    TextAsset asset = profile.recipeAssets[i];

                    if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                    {
                        continue;
                    }

                    if (chosen == 0)
                    {
                        recipe = asset.text;
                        return true;
                    }

                    chosen--;
                }
            }
            else if (profile.recipeStrings != null)
            {
                chosen -= umaTextRecipeValidCount + assetValidCount;

                for (int i = 0; i < profile.recipeStrings.Count; i++)
                {
                    string value = profile.recipeStrings[i];

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    if (chosen == 0)
                    {
                        recipe = value.Trim();
                        return true;
                    }

                    chosen--;
                }
            }

            return false;
        }

        private void ApplyScale(ZombieUmaVisualProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            float minScale = Mathf.Max(0.1f, Mathf.Min(profile.uniformScaleRange.x, profile.uniformScaleRange.y));
            float maxScale = Mathf.Max(minScale, Mathf.Max(profile.uniformScaleRange.x, profile.uniformScaleRange.y));
            float chosenScale = Random.Range(minScale, maxScale);
            transform.localScale = new Vector3(chosenScale, chosenScale, chosenScale);
        }

        private void BindUmaCallbacks()
        {
            if (callbacksBound || avatar == null)
            {
                return;
            }

            avatar.CharacterCreated.AddListener(HandleUmaCharacterBuilt);
            avatar.CharacterUpdated.AddListener(HandleUmaCharacterBuilt);
            callbacksBound = true;
        }

        private void UnbindUmaCallbacks()
        {
            if (!callbacksBound || avatar == null)
            {
                callbacksBound = false;
                return;
            }

            avatar.CharacterCreated.RemoveListener(HandleUmaCharacterBuilt);
            avatar.CharacterUpdated.RemoveListener(HandleUmaCharacterBuilt);
            callbacksBound = false;
        }

        private void HandleUmaApplyFailure(string stage, System.Exception exception)
        {
            suppressFurtherAppearanceChanges = true;

            if (!loggedUmaApplyFailure)
            {
                loggedUmaApplyFailure = true;

                if (logUmaApplyFailures)
                {
                    string reason = exception != null ? exception.Message : "Unknown UMA error";
                    Debug.LogWarning($"ZombieUmaAppearance failed during {stage} on {name}. Disabling further UMA updates for this zombie. Reason: {reason}", this);
                }
            }

            // Keep agent recovery behavior even when UMA application fails.
            HandleUmaCharacterBuilt(null);
        }


        private void HandleUmaCharacterBuilt(UMAData _)
        {
            StripUmaPhysicsRig();

            if (unitController != null)
            {
                unitController.ForceEnableAgent();
                return;
            }

            if (navMeshAgent == null)
            {
                navMeshAgent = GetComponent<NavMeshAgent>();
            }

            if (navMeshAgent == null)
            {
                return;
            }

            if (!navMeshAgent.enabled)
            {
                navMeshAgent.enabled = true;
            }

            if (navMeshAgent.enabled)
            {
                navMeshAgent.Warp(transform.position);
            }
        }

        private void StripUmaPhysicsRig()
        {
            if (!stripUmaPhysicsRigColliders)
            {
                return;
            }

            UMAPhysicsAvatar physicsAvatar = GetComponent<UMAPhysicsAvatar>();
            if (physicsAvatar == null)
            {
                return;
            }

            RemoveChildComponents<SphereCollider>();
            RemoveChildComponents<BoxCollider>();
            RemoveChildComponents<CapsuleCollider>();
            RemoveChildComponents<CharacterJoint>();
            RemoveChildComponents<Rigidbody>();

            Destroy(physicsAvatar);
        }

        private void RemoveChildComponents<T>() where T : Component
        {
            T[] components = GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component == null)
                {
                    continue;
                }

                if (component.gameObject == gameObject)
                {
                    continue;
                }

                Destroy(component);
            }
        }
    }
}
