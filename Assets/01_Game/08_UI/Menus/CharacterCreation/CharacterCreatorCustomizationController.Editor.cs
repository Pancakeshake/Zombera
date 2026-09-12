#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Zombera.UI.Menus.CharacterCreation
{
    public sealed partial class CharacterCreatorCustomizationController
    {
        private bool _editorUiRefreshQueued;

        private void OnEnable()
        {
            QueueEditorPanelUiRefresh();
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RefreshEditorPanelUiAfterDelay;
            _editorUiRefreshQueued = false;
        }

        private void OnValidate()
        {
            QueueEditorPanelUiRefresh();
        }

        private void QueueEditorPanelUiRefresh()
        {
            if (Application.isPlaying || !EnableRuntimeCustomizationUi) return;

            if (_editorUiRefreshQueued) return;

            _editorUiRefreshQueued = true;
            EditorApplication.delayCall += RefreshEditorPanelUiAfterDelay;
        }

        private void RefreshEditorPanelUiAfterDelay()
        {
            EditorApplication.delayCall -= RefreshEditorPanelUiAfterDelay;
            _editorUiRefreshQueued = false;

            if (IsUnityObjectDestroyed(this) || !isActiveAndEnabled) return;

            if (Application.isPlaying || !EnableRuntimeCustomizationUi) return;

            BuildOrResolveRuntimeUi();
        }

        private static bool IsUnityObjectDestroyed(Object value)
        {
            return value == null;
        }
    }
}
#endif
