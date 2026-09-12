using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Core;
using Random = UnityEngine.Random;

namespace Zombera.UI.Menus.CharacterCreation
{
    public static class CharacterCreatorRandomizationHelper
    {
        public static void RandomizeAppearance(
            CharacterAppearanceProfile profile,
            CharacterAppearanceCatalog catalog,
            List<string> raceOptions,
            ref int raceOptionIndex,
            Vector2 randomBodySliderRange,
            Vector2 randomHeightSliderRange,
            Vector2 randomLockedProportionSliderRange,
            Vector2 randomSkinToneRange,
            Vector2 randomHairToneRange,
            Vector2 randomEyeToneRange,
            Func<string, float, float> convertBodySliderToDnaValue,
            Action<string, float> setDnaValue)
        {
            if (raceOptions.Count > 0)
            {
                raceOptionIndex = Random.Range(0, raceOptions.Count);
                profile.raceName = raceOptions[raceOptionIndex];
            }

            var bodyRange = CharacterCreatorUtility.NormalizeRange(randomBodySliderRange, 0.48f, 0.52f);
            var heightRange = CharacterCreatorUtility.NormalizeRange(randomHeightSliderRange, 0.49f, 0.51f);
            var lockedRange = CharacterCreatorUtility.NormalizeRange(randomLockedProportionSliderRange, 0.495f, 0.505f);
            var skinToneRange = CharacterCreatorUtility.NormalizeRange(randomSkinToneRange, 0.40f, 0.65f);
            var hairToneRange = CharacterCreatorUtility.NormalizeRange(randomHairToneRange, 0.25f, 0.75f);
            var eyeToneRange = CharacterCreatorUtility.NormalizeRange(randomEyeToneRange, 0.35f, 0.75f);

            if (catalog != null)
                foreach (var definition in catalog.dnaControls)
                {
                    Vector2 sliderRange;
                    if (string.Equals(definition.dnaName, "height", StringComparison.OrdinalIgnoreCase))
                        sliderRange = heightRange;
                    else if (definition.isLocked)
                        sliderRange = lockedRange;
                    else
                        sliderRange = bodyRange;

                    var randomizedSliderValue = Random.Range(sliderRange.x, sliderRange.y);
                    var randomizedDnaValue = convertBodySliderToDnaValue?.Invoke(definition.dnaName, randomizedSliderValue) ?? 0.5f;
                    setDnaValue?.Invoke(definition.dnaName, randomizedDnaValue);
                }

            profile.skinColor = CharacterCreatorUtility.PickRandomPresetSkinTone();
            profile.hairColor = CharacterCreatorUtility.EvaluateTone(Random.Range(hairToneRange.x, hairToneRange.y),
                CharacterCreatorStyle.HairToneDark, CharacterCreatorStyle.HairToneLight);
            profile.eyeColor = CharacterCreatorUtility.EvaluateTone(Random.Range(eyeToneRange.x, eyeToneRange.y),
                CharacterCreatorStyle.EyeToneDark, CharacterCreatorStyle.EyeToneLight);
            profile.wardrobeSelection.SetRecipe("Hair", string.Empty);
            profile.wardrobeSelection.SetRecipe("Beard", string.Empty);
        }
    }
}
