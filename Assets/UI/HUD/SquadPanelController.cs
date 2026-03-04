using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.UI
{
    /// <summary>
    /// Controls the left-side squad HUD panel UI references and refresh hooks.
    /// </summary>
    public sealed class SquadPanelController : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image panelBackground;
        [SerializeField] private ScrollRect membersScrollRect;
        [SerializeField] private RectTransform membersContentRoot;

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI squadTitleText;
        [SerializeField] private TextMeshProUGUI squadCountText;

        [Header("Entry Template")]
        [SerializeField] private GameObject squadMemberEntryPrefab;

        public bool IsInitialized { get; private set; }

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

            SetSquadHeader("Squad", 0, 0);
            IsInitialized = true;

            // TODO: Bind squad roster events and instantiate row entries.
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetSquadHeader(string squadTitle, int currentMembers, int maxMembers)
        {
            if (squadTitleText != null)
            {
                squadTitleText.text = string.IsNullOrWhiteSpace(squadTitle) ? "Squad" : squadTitle;
            }

            if (squadCountText != null)
            {
                squadCountText.text = $"{Mathf.Max(0, currentMembers)}/{Mathf.Max(0, maxMembers)}";
            }
        }

        public void SetMembers(IReadOnlyList<SquadMemberViewData> members)
        {
            int currentCount = members != null ? members.Count : 0;
            int maxCount = Mathf.Max(currentCount, 0);
            SetSquadHeader("Squad", currentCount, maxCount);

            // TODO: Populate member UI rows from members data.
        }

        public void ClearMembers()
        {
            SetSquadHeader("Squad", 0, 0);

            // TODO: Destroy/pool instantiated member rows.
        }
    }

    [Serializable]
    public struct SquadMemberViewData
    {
        public string unitId;
        public string displayName;
        [Range(0f, 1f)] public float health01;
        public bool isSelected;
        public bool isDowned;
    }
}