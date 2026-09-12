using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zombera.Characters;
using Zombera.UI.SquadManagement;

namespace Zombera.UI
{
    public sealed partial class SquadPortraitStrip
    {
        // ──────────────────────────────────────────────
        //  Runtime headshot capture + cache management
        // ──────────────────────────────────────────────

        private int ResolvePortraitRetryBudget(int visibleSlotCount)
        {
            var clampedVisibleSlotCount = Mathf.Max(0, visibleSlotCount);
            if (clampedVisibleSlotCount == 0) return 0;

            if (PortraitStudioManager.Instance != null || !useRuntimeHeadshotCapture)
                return clampedVisibleSlotCount;

            return Mathf.Clamp(maxRuntimeHeadshotCapturesPerRetryTick, 1, clampedVisibleSlotCount);
        }

        private bool TryGetCachedHeadshot(int unitInstanceId, out Sprite headshotSprite)
        {
            headshotSprite = null;

            if (!_headshotCacheByUnitId.TryGetValue(unitInstanceId, out var cachedSprite)) return false;

            if (cachedSprite == null)
            {
                RemoveCachedHeadshot(unitInstanceId);
                return false;
            }

            headshotSprite = cachedSprite;
            TouchCachedHeadshotCacheOrder(unitInstanceId);
            return true;
        }

        private void CacheHeadshot(int unitInstanceId, Sprite headshotSprite, Texture2D headshotTexture)
        {
            if (headshotSprite == null)
            {
                if (headshotTexture != null) Destroy(headshotTexture);
                return;
            }

            RemoveCachedHeadshot(unitInstanceId);

            _headshotCacheByUnitId[unitInstanceId] = headshotSprite;
            if (headshotTexture != null) _headshotCacheTexturesByUnitId[unitInstanceId] = headshotTexture;

            TouchCachedHeadshotCacheOrder(unitInstanceId);
            EnforceHeadshotCacheBudget();
        }

        private void RemoveCachedHeadshot(int unitInstanceId)
        {
            if (_headshotCacheNodesByUnitId.TryGetValue(unitInstanceId, out var cachedNode))
            {
                _headshotCacheOrder.Remove(cachedNode);
                _headshotCacheNodesByUnitId.Remove(unitInstanceId);
            }

            if (_headshotCacheByUnitId.TryGetValue(unitInstanceId, out var cachedSprite) && cachedSprite != null)
                Destroy(cachedSprite);

            _headshotCacheByUnitId.Remove(unitInstanceId);

            if (_headshotCacheTexturesByUnitId.TryGetValue(unitInstanceId, out var cachedTexture) &&
                cachedTexture != null) Destroy(cachedTexture);

            _headshotCacheTexturesByUnitId.Remove(unitInstanceId);
        }

        private void PruneHeadshotCacheToRoster()
        {
            if (_headshotCacheByUnitId.Count == 0 && _portraitReadbackBlockedUnitIds.Count == 0) return;

            _rosterUnitIds.Clear();
            foreach (var unitId in _rosterUnits.Where(unit => unit != null).Select(unit => unit.GetInstanceID()))
                _rosterUnitIds.Add(unitId);

            if (_headshotCacheByUnitId.Count > 0)
            {
                _cacheRemovalBuffer.Clear();
                _cacheRemovalBuffer.AddRange(
                    _headshotCacheByUnitId.Keys.Where(unitId => !_rosterUnitIds.Contains(unitId)));

                foreach (var unitId in _cacheRemovalBuffer) RemoveCachedHeadshot(unitId);
            }

            if (_portraitReadbackBlockedUnitIds.Count > 0)
            {
                _cacheRemovalBuffer.Clear();
                _cacheRemovalBuffer.AddRange(
                    _portraitReadbackBlockedUnitIds.Where(unitId => !_rosterUnitIds.Contains(unitId)));

                foreach (var unitId in _cacheRemovalBuffer) _portraitReadbackBlockedUnitIds.Remove(unitId);
            }

            _cacheRemovalBuffer.Clear();
            _rosterUnitIds.Clear();
        }

        private void ClearHeadshotCache()
        {
            if (_headshotCacheByUnitId.Count == 0)
            {
                _headshotCacheOrder.Clear();
                _headshotCacheNodesByUnitId.Clear();
                _headshotCacheTexturesByUnitId.Clear();
                return;
            }

            _cacheRemovalBuffer.Clear();
            foreach (var pair in _headshotCacheByUnitId) _cacheRemovalBuffer.Add(pair.Key);

            foreach (var unitId in _cacheRemovalBuffer) RemoveCachedHeadshot(unitId);

            _cacheRemovalBuffer.Clear();
            _headshotCacheByUnitId.Clear();
            _headshotCacheTexturesByUnitId.Clear();
            _headshotCacheOrder.Clear();
            _headshotCacheNodesByUnitId.Clear();
        }

        private void TouchCachedHeadshotCacheOrder(int unitInstanceId)
        {
            if (_headshotCacheNodesByUnitId.TryGetValue(unitInstanceId, out var existingNode))
            {
                _headshotCacheOrder.Remove(existingNode);
                _headshotCacheOrder.AddLast(existingNode);
                return;
            }

            var node = _headshotCacheOrder.AddLast(unitInstanceId);
            _headshotCacheNodesByUnitId[unitInstanceId] = node;
        }

        private void EnforceHeadshotCacheBudget()
        {
            var cacheBudget = Mathf.Max(4, maxCachedHeadshots);
            while (_headshotCacheByUnitId.Count > cacheBudget && _headshotCacheOrder.Count > 0)
            {
                var oldestNode = _headshotCacheOrder.First;
                if (oldestNode == null) break;

                var unitInstanceId = oldestNode.Value;
                RemoveCachedHeadshot(unitInstanceId);
            }
        }

        private bool TryCaptureRuntimeHeadshot(Unit unit, Transform head, out Sprite portraitSprite,
            out Texture2D portraitTexture)
        {
            portraitSprite = null;
            portraitTexture = null;

            var captureStartedAt = enablePortraitCaptureDiagnostics ? Time.realtimeSinceStartup : 0f;

            if (!Application.isPlaying
                || !useRuntimeHeadshotCapture
                || _runtimeHeadshotCaptureDisabledForSession
                || unit == null
                || head == null)
                return false;

            var resolution = Mathf.Clamp(runtimeHeadshotResolution, 64, 512);
            var headshotRenderTexture = RenderTexture.GetTemporary(
                resolution,
                resolution,
                16,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);

            headshotRenderTexture.name = "SquadPortraitHeadshotRT";

            GameObject cameraObject = null;
            GameObject lightObject = null;
            Texture2D capturedTexture = null;

            try
            {
                cameraObject = new GameObject("SquadPortraitCaptureCamera", typeof(Camera));
                var captureCamera = cameraObject.GetComponent<Camera>();
                captureCamera.enabled = false;
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                captureCamera.cullingMask = ~0;
                captureCamera.nearClipPlane = 0.01f;
                captureCamera.farClipPlane = 8f;
                captureCamera.fieldOfView = runtimeHeadshotFieldOfView;
                captureCamera.allowHDR = false;
                captureCamera.allowMSAA = false;
                captureCamera.targetTexture = headshotRenderTexture;

                var targetPosition = head.position;
                var lookPosition = targetPosition + Vector3.up * runtimeHeadshotLookOffset;
                var forward = ResolvePortraitForward(head, unit.transform);
                var cameraPosition = targetPosition + Vector3.up * runtimeHeadshotVerticalOffset +
                                     forward * runtimeHeadshotDistance;

                captureCamera.transform.position = cameraPosition;
                captureCamera.transform.rotation = Quaternion.LookRotation(lookPosition - cameraPosition, Vector3.up);

                if (runtimeHeadshotUseFillLight)
                {
                    lightObject = new GameObject("SquadPortraitCaptureLight", typeof(Light));
                    var fillLight = lightObject.GetComponent<Light>();
                    fillLight.type = LightType.Directional;
                    fillLight.color = runtimeHeadshotFillLightColor;
                    fillLight.intensity = runtimeHeadshotFillLightIntensity;
                    fillLight.shadows = LightShadows.None;
                    fillLight.transform.rotation = captureCamera.transform.rotation * Quaternion.Euler(12f, -20f, 0f);
                }

                captureCamera.Render();

                if (!TryCopyRenderTextureToTexture2D(headshotRenderTexture, out capturedTexture) ||
                    capturedTexture == null)
                {
                    LogPortraitCaptureTiming("Runtime headshot capture", unit, captureStartedAt, false);
                    return false;
                }

                var sourceRect = new RectInt(0, 0, capturedTexture.width, capturedTexture.height);
                var subjectRect =
                    TryFindOpaqueBounds(capturedTexture, sourceRect, alphaDetectionThreshold, out var detected)
                        ? detected
                        : sourceRect;
                var cropRect = ComputeFaceCropRect(sourceRect, subjectRect);

                portraitSprite = Sprite.Create(
                    capturedTexture,
                    new Rect(cropRect.x, cropRect.y, cropRect.width, cropRect.height),
                    new Vector2(0.5f, 0.5f),
                    100f);

                if (portraitSprite == null)
                {
                    Destroy(capturedTexture);
                    LogPortraitCaptureTiming("Runtime headshot capture", unit, captureStartedAt, false);
                    return false;
                }

                portraitTexture = capturedTexture;
                capturedTexture = null;
                LogPortraitCaptureTiming("Runtime headshot capture", unit, captureStartedAt, true);
                return true;
            }
            catch (Exception)
            {
                DisableRuntimeHeadshotCaptureForSession();

                if (capturedTexture != null) Destroy(capturedTexture);

                LogPortraitCaptureTiming("Runtime headshot capture", unit, captureStartedAt, false);
                return false;
            }
            finally
            {
                if (cameraObject != null) Destroy(cameraObject);
                if (lightObject != null) Destroy(lightObject);
                RenderTexture.ReleaseTemporary(headshotRenderTexture);
            }
        }

        private void DisableRuntimeHeadshotCaptureForSession()
        {
            _runtimeHeadshotCaptureDisabledForSession = true;
            if (_runtimeHeadshotCaptureDisableLogged) return;

            _runtimeHeadshotCaptureDisableLogged = true;
            Debug.LogWarning(
                "[SquadPortraitStrip] Runtime headshot capture disabled for this play session after a render failure. Falling back to texture portraits.",
                this);
        }
    }
}
