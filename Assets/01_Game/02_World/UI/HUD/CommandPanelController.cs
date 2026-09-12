#region

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace Zombera.UI
{
    /// <summary>
    ///     Controls the right-side command HUD panel and emits command button events.
    /// </summary>
    public sealed class CommandPanelController : MonoBehaviour
    {
        [Header("Root")] [SerializeField] private RectTransform panelRoot;

        [SerializeField] private Image panelBackground;

        [Header("Title")] [SerializeField] private TextMeshProUGUI panelTitleText;

        [Header("Command Buttons")] [SerializeField]
        private Button moveButton;

        [SerializeField] private Button attackButton;
        [SerializeField] private Button holdPositionButton;
        [SerializeField] private Button followButton;
        [SerializeField] private Button defendButton;

        [SerializeField] [Min(0f)] private float commandCooldownSeconds = 0.5f;

        public bool IsInitialized { get; private set; }

        public event Action<HudCommandType> CommandRequested;

        public void Initialize(HUDManager manager)
        {
            if (IsInitialized) return;

            if (panelRoot == null) panelRoot = transform as RectTransform;

            if (panelTitleText != null) panelTitleText.text = "Commands";

            BindButton(moveButton, HudCommandType.Move);
            BindButton(attackButton, HudCommandType.Attack);
            BindButton(holdPositionButton, HudCommandType.HoldPosition);
            BindButton(followButton, HudCommandType.Follow);
            BindButton(defendButton, HudCommandType.Defend);

            IsInitialized = true;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetCommandInteractable(HudCommandType commandType, bool interactable)
        {
            var button = GetButton(commandType);

            if (button != null) button.interactable = interactable;
        }

        private void BindButton(Button button, HudCommandType commandType)
        {
            if (button == null) return;

            button.onClick.AddListener(() => RaiseCommandRequested(commandType));
        }

        private void RaiseCommandRequested(HudCommandType commandType)
        {
            CommandRequested?.Invoke(commandType);
            SetCommandInteractable(commandType, false);
            StartCoroutine(RestoreCommandAfterDelay(commandType, commandCooldownSeconds));
        }

        private IEnumerator RestoreCommandAfterDelay(HudCommandType commandType, float delay)
        {
            yield return new WaitForSeconds(delay);
            SetCommandInteractable(commandType, true);
        }

        private Button GetButton(HudCommandType commandType)
        {
            return commandType switch
            {
                HudCommandType.Move => moveButton,
                HudCommandType.Attack => attackButton,
                HudCommandType.HoldPosition => holdPositionButton,
                HudCommandType.Follow => followButton,
                HudCommandType.Defend => defendButton,
                _ => null
            };
        }
    }

    public enum HudCommandType
    {
        Move,
        Attack,
        HoldPosition,
        Follow,
        Defend
    }
}