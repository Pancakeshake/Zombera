#region

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Core;

#endregion

namespace Zombera.UI
{
    /// <summary>
    ///     Controls the left-side squad HUD panel UI references and refresh hooks.
    /// </summary>
    public sealed class SquadPanelController : MonoBehaviour
    {
        [Header("Root")] [SerializeField] private RectTransform panelRoot;

        [SerializeField] private Image panelBackground;
        [SerializeField] private ScrollRect membersScrollRect;
        [SerializeField] private RectTransform membersContentRoot;

        [Header("Text")] [SerializeField] private TextMeshProUGUI squadTitleText;

        [SerializeField] private TextMeshProUGUI squadCountText;

        [Header("Entry Template")] [SerializeField]
        private GameObject squadMemberEntryPrefab;

        public bool IsInitialized { get; private set; }

        public void Initialize(HUDManager manager)
        {
            if (IsInitialized) return;

            if (panelRoot == null) panelRoot = transform as RectTransform;

            SetSquadHeader("Squad", 0, 0);
            IsInitialized = true;
            BindSquadRosterEvents();
        }

        private static void BindSquadRosterEvents()
        {
            CoreEventBus.Instance?.Subscribe<SquadRosterChangedEvent>(OnRosterChanged);
        }

        private static void OnRosterChanged(SquadRosterChangedEvent evt)
        {
            // Let the HUDManager push a fresh member list when it's available.
            // Direct access to SquadManager from here would create a cross-layer dependency.
            _ = evt;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetSquadHeader(string squadTitle, int currentMembers, int maxMembers)
        {
            if (squadTitleText != null)
                squadTitleText.text = string.IsNullOrWhiteSpace(squadTitle) ? "Squad" : squadTitle;

            if (squadCountText != null)
                squadCountText.text = $"{Mathf.Max(0, currentMembers)}/{Mathf.Max(0, maxMembers)}";
        }

        public void SetMembers(IReadOnlyList<SquadMemberViewData> members)
        {
            var currentCount = members?.Count ?? 0;
            var maxCount = Mathf.Max(currentCount, 0);
            SetSquadHeader("Squad", currentCount, maxCount);

            if (squadMemberEntryPrefab == null || membersContentRoot == null) return;

            // Pool existing rows: deactivate rather than destroy so they can be reused.
            for (var i = 0; i < membersContentRoot.childCount; i++)
                membersContentRoot.GetChild(i).gameObject.SetActive(false);

            if (members == null) return;

            for (var i = 0; i < members.Count; i++)
            {
                var rowTransform = i < membersContentRoot.childCount
                    ? membersContentRoot.GetChild(i)
                    : Instantiate(squadMemberEntryPrefab, membersContentRoot).transform;

                rowTransform.gameObject.SetActive(true);
                var row = rowTransform.GetComponent<SquadMemberRowView>();
                row?.Bind(members[i]);
            }
        }

        public void ClearMembers()
        {
            SetSquadHeader("Squad", 0, 0);

            if (membersContentRoot == null) return;

            // Deactivate pooled rows rather than destroying them.
            for (var i = 0; i < membersContentRoot.childCount; i++)
                membersContentRoot.GetChild(i).gameObject.SetActive(false);
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