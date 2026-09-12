using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zombera.Core;
using System;
using System.Collections;

namespace Zombera.UI.Menus
{
    public class SaveSlotItem : MonoBehaviour
    {
        [Header("Slot Info")]
        [SerializeField] private TMP_Text slotNameText;
        [SerializeField] private TMP_Text slotStatsText;
        [SerializeField] private TMP_Text timestampText;
        [SerializeField] private Image screenshotImage;
        [SerializeField] private GameObject currentSaveBadge;
        [SerializeField] private GameObject newSlotOverlay;
        [SerializeField] private GameObject existingSlotContent;

        [Header("Visual Elements")]
        [SerializeField] private Image backgroundFill;
        [SerializeField] private Image borderGlow;
        [SerializeField] private Image noiseOverlay;
        [SerializeField] private Image innerStroke;
        
        [Header("Selection Effects")]
        [SerializeField] private float selectedScale = 1.02f;
        [SerializeField] private float selectedGlowAlpha = 0.6f;
        [SerializeField] private float normalGlowAlpha = 0.2f;
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Color normalTextColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        [Header("Interactions")]
        [SerializeField] private Button actionButton;

        private SaveMetadata _metadata;
        private Action<SaveSlotItem> _onSelected;
        private bool _isNewSlot;
        private Coroutine _selectionCoroutine;

        public string SlotId { get; private set; }
        public SaveMetadata Metadata => _metadata;
        public bool IsNewSlot => _isNewSlot;

        private void Awake()
        {
            if (actionButton == null) actionButton = GetComponent<Button>();
        }

        public void Setup(SaveMetadata metadata, bool isNewSlot, Action<SaveSlotItem> onSelected, bool isCurrent)
        {
            _metadata = metadata;
            SlotId = metadata?.slotId;
            _isNewSlot = isNewSlot;
            _onSelected = onSelected;

            if (newSlotOverlay != null) newSlotOverlay.SetActive(isNewSlot);
if (existingSlotContent != null) existingSlotContent.SetActive(!isNewSlot);

            if (!isNewSlot)
            {
                if (slotNameText != null) slotNameText.text = metadata.slotName;
                if (timestampText != null) timestampText.text = metadata.timestamp;
                
                if (slotStatsText != null)
                {
                    TimeSpan t = TimeSpan.FromSeconds(metadata.playTimeSeconds);
                    string timeStr = string.Format("{0:D2}:{1:D2}", t.Hours, t.Minutes);
                    // Aesthetic format: Day 23 • 14:37 • Riverside Town
                    // Progress: 42% • Version 0.9.2
                    slotStatsText.text = $"Day {metadata.dayNumber} • {timeStr} • {metadata.locationName}\n" +
                                       $"Progress: {metadata.progressPercent}% • Version {metadata.gameVersion}";
                }

                if (currentSaveBadge != null) currentSaveBadge.SetActive(isCurrent);
            }
            else
            {
                if (slotNameText != null) slotNameText.text = "START A NEW GAME";
            }

            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(() => _onSelected?.Invoke(this));
            }
            
            SetSelected(false, true);
            }

            public void UpdateMetadata(SaveMetadata metadata)
            {
            _metadata = metadata;
            SlotId = metadata?.slotId;
            }

            public void SetSelected(bool selected)
{
            SetSelected(selected, false);
        }

        public void SetSelected(bool selected, bool immediate)
        {
            if (_selectionCoroutine != null) StopCoroutine(_selectionCoroutine);
            
            if (immediate)
            {
                ApplySelectionState(selected ? 1f : 0f);
            }
            else
            {
                _selectionCoroutine = StartCoroutine(TransitionSelection(selected));
            }
        }

        private IEnumerator TransitionSelection(bool selected)
        {
            float target = selected ? 1f : 0f;
            float current = borderGlow != null ? (borderGlow.color.a - normalGlowAlpha) / (selectedGlowAlpha - normalGlowAlpha) : 0f;
            
            float elapsed = 0f;
            float duration = 0.15f;
            
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(current, target, elapsed / duration);
                ApplySelectionState(t);
                yield return null;
            }
            
            ApplySelectionState(target);
        }

        private void ApplySelectionState(float t)
        {
            // Scale
            transform.localScale = Vector3.one * Mathf.Lerp(1f, selectedScale, t);
            
            // Border Glow Alpha
            if (borderGlow != null)
            {
                Color c = borderGlow.color;
                c.a = Mathf.Lerp(normalGlowAlpha, selectedGlowAlpha, t);
                borderGlow.color = c;
            }
            
            // Text Color
            if (slotNameText != null) slotNameText.color = Color.Lerp(normalTextColor, selectedTextColor, t);
        }

        public void SetScreenshot(Sprite sprite)
        {
            if (screenshotImage != null)
            {
                screenshotImage.sprite = sprite;
                screenshotImage.color = sprite != null ? Color.white : new Color(0, 0, 0, 0.4f);
            }
        }
    }
}

