#region

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

#endregion

namespace Zombera.UI.Menus
{
    public sealed partial class MainMenuController
    {
        private void EnsureUiClickAudioSource()
        {
            if (_uiClickAudioSource != null) return;

            _uiClickAudioSource = GetComponent<AudioSource>();
            if (_uiClickAudioSource == null)
                _uiClickAudioSource = gameObject.AddComponent<AudioSource>();

            _uiClickAudioSource.playOnAwake = false;
            _uiClickAudioSource.loop = false;
            _uiClickAudioSource.spatialBlend = 0f;
            _uiClickAudioSource.volume = 1f;
        }

        private void PlayMenuClickSfx()
        {
#if UNITY_EDITOR
            if (menuClickSfx == null && !string.IsNullOrWhiteSpace(menuClickSfxAssetPath))
                menuClickSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(menuClickSfxAssetPath);
#endif

            if (menuClickSfx == null) return;

            EnsureUiClickAudioSource();
            if (_uiClickAudioSource == null) return;

            _uiClickAudioSource.PlayOneShot(menuClickSfx, Mathf.Clamp01(menuClickVolume));
        }
    }
}
