#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Zombera.UI.SquadManagement;

#endregion

namespace Zombera.UI
{
    public sealed partial class SquadPortraitStrip
    {

        private void QueueRosterPortraitStudioRequests()
        {
            var studio = PortraitStudioManager.Instance;
            if (studio == null || _rosterUnits == null || _rosterUnits.Count == 0) return;

            for (var i = 0; i < _rosterUnits.Count; i++)
            {
                var unit = _rosterUnits[i];
                if (unit == null || !unit.IsAlive) continue;

                if (unit.Role == UnitRole.Player && CharacterSelectionState.SelectedPortraitSprite != null)
                    continue;

                studio.RefreshPortraitFromUnit(unit);
            }
        }

        private void RetryMissingPortraits()
        {
            QueueRosterPortraitStudioRequests();

            if (_slots == null || _slots.Length == 0) return;

            var visibleSlotCount = 0;
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null || !slot.gameObject.activeSelf || slot.BoundUnit == null || slot.portraitImage == null)
                    continue;

                visibleSlotCount++;
            }

            if (visibleSlotCount == 0) return;

            var retryBudget = ResolvePortraitRetryBudget(visibleSlotCount);
            if (retryBudget <= 0) return;

            var startIndex = Mathf.Clamp(_nextPortraitRetryStartIndex, 0, Mathf.Max(0, _slots.Length - 1));
            var inspected = 0;
            var consumedBudget = 0;

            for (var offset = 0; offset < _slots.Length && consumedBudget < retryBudget; offset++)
            {
                var slotIndex = (startIndex + offset) % _slots.Length;
                var slot = _slots[slotIndex];
                if (slot == null || !slot.gameObject.activeSelf || slot.BoundUnit == null || slot.portraitImage == null)
                    continue;

                inspected++;

                if (slot.portraitImage.sprite != null) continue;

                if (!TryApplySelectionPortrait(slot, slot.BoundUnit))
                    ApplyPortraitFromUnitHead(slot, slot.BoundUnit);

                consumedBudget++;
            }

            if (_slots.Length == 0)
            {
                _nextPortraitRetryStartIndex = 0;
                return;
            }

            _nextPortraitRetryStartIndex = (startIndex + Mathf.Max(1, inspected)) % _slots.Length;
        }


        private static bool TryApplyPortraitStudioCache(SquadPortraitSlot slot, Unit unit)
        {
            if (slot == null || slot.portraitImage == null || unit == null) return false;

            var studio = PortraitStudioManager.Instance;
            if (studio == null) return false;

            var unitKey = string.IsNullOrWhiteSpace(unit.UnitId)
                ? unit.GetInstanceID().ToString()
                : unit.UnitId;

            if (!studio.TryGetCachedPortrait(unitKey, out var cachedSprite) || cachedSprite == null)
                return false;

            slot.portraitImage.sprite = cachedSprite;
            slot.portraitImage.color = Color.white;
            slot.portraitImage.type = Image.Type.Simple;
            slot.portraitImage.preserveAspect = true;
            return true;
        }

        private bool TryApplySelectionPortrait(SquadPortraitSlot slot, Unit unit)
        {
            if (slot == null || slot.portraitImage == null || unit == null)
                return false;

            if (unit.Role != UnitRole.Player)
                return false;

            var selectedPortrait = CharacterSelectionState.SelectedPortraitSprite;
            if (selectedPortrait == null)
                return false;

            var portraitSprite = selectedPortrait;
            var cropped = CreateFaceCropSprite(selectedPortrait);
            if (cropped != null)
            {
                _capturedPortraitSprites.Add(cropped);
                portraitSprite = cropped;
            }

            slot.portraitImage.sprite = portraitSprite;
            slot.portraitImage.color = Color.white;
            slot.portraitImage.type = Image.Type.Simple;
            slot.portraitImage.preserveAspect = true;
            return true;
        }


        private void ApplyPortraitFromUnitHead(SquadPortraitSlot slot, Unit unit)
        {
            if (slot == null || unit == null || slot.portraitImage == null) return;

            if (TryApplySelectionPortrait(slot, unit))
                return;

            if (TryApplyPortraitStudioCache(slot, unit))
                return;

            var studio = PortraitStudioManager.Instance;
            if (studio != null)
            {
                studio.RefreshPortraitFromUnit(unit);
                slot.portraitImage.sprite = null;
                slot.portraitImage.color = new Color(0.20f, 0.20f, 0.25f, 1f);
                return;
            }

            slot.portraitImage.sprite = null;
            slot.portraitImage.color = new Color(0.20f, 0.20f, 0.25f, 1f);

            var unitInstanceId = unit.GetInstanceID();
            if (_portraitReadbackBlockedUnitIds.Contains(unitInstanceId)) return;

            if (TryGetCachedHeadshot(unitInstanceId, out var cachedHeadshot))
            {
                slot.portraitImage.sprite = cachedHeadshot;
                slot.portraitImage.color = Color.white;
                slot.portraitImage.type = Image.Type.Simple;
                slot.portraitImage.preserveAspect = true;
                return;
            }

            var head = FindHeadTransform(unit.transform);
            if (head == null) return;

            if (TryCaptureRuntimeHeadshot(unit, head, out var headshotSprite, out var headshotTexture))
            {
                CacheHeadshot(unitInstanceId, headshotSprite, headshotTexture);

                slot.portraitImage.sprite = headshotSprite;
                slot.portraitImage.color = Color.white;
                slot.portraitImage.type = Image.Type.Simple;
                slot.portraitImage.preserveAspect = true;
                return;
            }

            if (!TryResolvePortraitTexture(unit.transform, head, out var tex) || tex == null)
                return;

            if (!allowRuntimeTextureReadbackFallback && tex is not Texture2D)
                return;

            var readbackStartedAt = enablePortraitCaptureDiagnostics ? Time.realtimeSinceStartup : 0f;

            var sprite = CreateFaceCropSprite(tex);
            if (sprite == null && !TryCreateSpriteFromTexture(tex, out sprite))
            {
                LogPortraitCaptureTiming("Fallback portrait readback", unit, readbackStartedAt, false);
                return;
            }

            _portraitReadbackBlockedUnitIds.Remove(unitInstanceId);

            _capturedPortraitSprites.Add(sprite);

            LogPortraitCaptureTiming("Fallback portrait readback", unit, readbackStartedAt, true);

            slot.portraitImage.sprite = sprite;
            slot.portraitImage.color = Color.white;
            slot.portraitImage.type = Image.Type.Simple;
            slot.portraitImage.preserveAspect = true;
        }


        private void LogPortraitCaptureTiming(string operationName, Unit unit, float startedAt, bool success)
        {
            if (!enablePortraitCaptureDiagnostics) return;

            var elapsedMs = Mathf.Max(0f, (Time.realtimeSinceStartup - startedAt) * 1000f);
            var thresholdMs = Mathf.Max(0f, portraitCaptureDiagnosticsThresholdMs);
            if (success && elapsedMs < thresholdMs) return;

            var unitName = unit != null ? unit.gameObject.name : "<null>";
            var status = success ? "ok" : "failed";
            Debug.Log($"[SquadPortraitStrip] {operationName} {status} in {elapsedMs:F1} ms (unit: {unitName})", this);
        }


        private void ReleaseCapturedPortraits()
        {
            if (_slots != null)
                foreach (var slot in _slots)
                    if (slot != null && slot.portraitImage != null)
                    {
                        slot.portraitImage.sprite = null;
                        slot.portraitImage.color = new Color(0.20f, 0.20f, 0.25f, 1f);
                    }

            foreach (var sprite in _capturedPortraitSprites.Where(sprite => sprite != null))
                Destroy(sprite);
            _capturedPortraitSprites.Clear();

            foreach (var texture in _capturedPortraitTextures.Where(texture => texture != null))
                Destroy(texture);

            _capturedPortraitTextures.Clear();
        }
    }
}
