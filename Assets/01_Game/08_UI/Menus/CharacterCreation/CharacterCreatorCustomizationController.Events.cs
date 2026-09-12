using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI.Menus.CharacterCreation
{
    public sealed partial class CharacterCreatorCustomizationController
    {
        private void BindUiEvents()
        {
            RebindButton(_bodyTabButton, () => SetActiveTab("Body"));
            RebindButton(_hairTabButton, () => SetActiveTab("Hair"));
            RebindButton(_skinTabButton, () => SetActiveTab("Skin"));
            RebindButton(_presetsTabButton, () => SetActiveTab("Presets"));

            RebindButton(_racePrevButton, () => SetRaceByGender(true));
            RebindButton(_raceNextButton, () => SetRaceByGender(false));
            RebindButton(_hairPrevButton, () => CycleHair(-1));
            RebindButton(_hairNextButton, () => CycleHair(1));
            RebindButton(_beardPrevButton, () => CycleBeard(-1));
            RebindButton(_beardNextButton, () => CycleBeard(1));

            RebindButton(_randomAppearanceButton, RandomizeAppearance);
            RebindButton(_resetAppearanceButton, ResetAppearance);

            RebindBodySliderEvents();

            if (_skinToneSlider != null)
            {
                _skinToneSlider.onValueChanged.RemoveAllListeners();
                _skinToneSlider.onValueChanged.AddListener(value =>
                {
                    PlayUiInteractionSfx(true);
                    HandleSkinToneChanged(value);
                });
            }

            if (_hairToneSlider != null)
            {
                _hairToneSlider.onValueChanged.RemoveAllListeners();
                _hairToneSlider.onValueChanged.AddListener(value =>
                {
                    PlayUiInteractionSfx(true);
                    HandleHairToneChanged(value);
                });
            }

            if (_eyeToneSlider != null)
            {
                _eyeToneSlider.onValueChanged.RemoveAllListeners();
                _eyeToneSlider.onValueChanged.AddListener(value =>
                {
                    PlayUiInteractionSfx(true);
                    HandleEyeToneChanged(value);
                });
            }
        }

        private void RebindBodySliderEvents()
        {
            foreach (var pair in _bodyControlSliders)
            {
                var slider = pair.Value;
                if (slider == null) continue;

                var dnaName = pair.Key;
                slider.onValueChanged.RemoveAllListeners();
                slider.onValueChanged.AddListener(value =>
                {
                    PlayUiInteractionSfx(true);
                    HandleBodyControlSliderChanged(dnaName, value);
                });
            }
        }

        private void RebindButton(Button button, Action callback)
        {
            if (button == null) return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                PlayUiInteractionSfx();
                callback?.Invoke();
            });
        }

        private void PlayUiInteractionSfx(bool sliderInteraction = false)
        {
            if (sliderInteraction)
                (_sliderUiInteractionSfxCallback ?? _uiInteractionSfxCallback)?.Invoke();
            else
                _uiInteractionSfxCallback?.Invoke();
        }
    }
}
