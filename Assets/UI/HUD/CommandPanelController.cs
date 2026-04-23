using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    /// <summary>
    /// Controls the right-side command HUD panel and emits command button events.
    /// </summary>
    public sealed class CommandPanelController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image panelBackground;

        [Header("Title")]
        [SerializeField] private TextMeshProUGUI panelTitleText;

        [Header("Command Buttons")]
        [SerializeField] private Button moveButton;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button holdPositionButton;
        [SerializeField] private Button followButton;
        [SerializeField] private Button defendButton;

        public bool IsInitialized { get; private set; }

        public event Action<HUDCommandType> CommandRequested;

        private HUDManager hudManager;

        public void Initialize(HUDManager manager)
        {
            if (IsInitialized)
            {
                return;
            }

            hudManager = manager;

            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            if (panelTitleText != null)
            {
                panelTitleText.text = "Commands";
            }

            BindButton(moveButton, HUDCommandType.Move);
            BindButton(attackButton, HUDCommandType.Attack);
            BindButton(holdPositionButton, HUDCommandType.HoldPosition);
            BindButton(followButton, HUDCommandType.Follow);
            BindButton(defendButton, HUDCommandType.Defend);

            IsInitialized = true;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetCommandInteractable(HUDCommandType commandType, bool interactable)
        {
            Button button = GetButton(commandType);

            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private void BindButton(Button button, HUDCommandType commandType)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.AddListener(() => RaiseCommandRequested(commandType));
        }

        private void RaiseCommandRequested(HUDCommandType commandType)
        {
            CommandRequested?.Invoke(commandType);
            SetCommandInteractable(commandType, false);
            StartCoroutine(RestoreCommandAfterDelay(commandType, commandCooldownSeconds));
        }

        [SerializeField, Min(0f)] private float commandCooldownSeconds = 0.5f;

        private System.Collections.IEnumerator RestoreCommandAfterDelay(HUDCommandType commandType, float delay)
        {
            yield return new UnityEngine.WaitForSeconds(delay);
            SetCommandInteractable(commandType, true);
        }

        private Button GetButton(HUDCommandType commandType)
        {
            switch (commandType)
            {
                case HUDCommandType.Move:
                    return moveButton;
                case HUDCommandType.Attack:
                    return attackButton;
                case HUDCommandType.HoldPosition:
                    return holdPositionButton;
                case HUDCommandType.Follow:
                    return followButton;
                case HUDCommandType.Defend:
                    return defendButton;
                default:
                    return null;
            }
        }
    }

    public enum HUDCommandType
    {
        Move,
        Attack,
        HoldPosition,
        Follow,
        Defend
    }
}