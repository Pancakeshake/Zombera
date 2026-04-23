using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    [AddComponentMenu("Zombera/UI/Squad Roster Modal Controller")]
    [DisallowMultipleComponent]
    public sealed class SquadRosterModalController : MonoBehaviour
    {
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private SquadPortraitStrip modalPortraitStrip;

        private void Awake()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (openButton != null) openButton.onClick.AddListener(OpenModal);
            if (closeButton != null) closeButton.onClick.AddListener(CloseModal);
            if (backdropButton != null) backdropButton.onClick.AddListener(CloseModal);
        }

        private void OnDisable()
        {
            if (openButton != null) openButton.onClick.RemoveListener(OpenModal);
            if (closeButton != null) closeButton.onClick.RemoveListener(CloseModal);
            if (backdropButton != null) backdropButton.onClick.RemoveListener(CloseModal);
        }

        public void OpenModal()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }

            if (modalPortraitStrip == null && modalRoot != null)
            {
                modalPortraitStrip = modalRoot.GetComponentInChildren<SquadPortraitStrip>(true);
            }

            modalPortraitStrip?.RefreshBindings();
        }

        public void CloseModal()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        public void ToggleModal()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(!modalRoot.activeSelf);
            }
        }
    }
}
