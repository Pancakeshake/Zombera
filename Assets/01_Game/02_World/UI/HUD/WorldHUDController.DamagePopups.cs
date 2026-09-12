#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;
using UnityEngine.UI;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.UI
{
    public sealed partial class WorldHUDController
    {
    private const int DamagePopupPoolDefaultCapacity = 24;
    private const int DamagePopupPoolMaxSize = 160;
    private IObjectPool<DamagePopupView> _damagePopupPool;

        private void TrySubscribeDamageEvents()
        {
            if (_damageEventsSubscribed) return;

            if (CoreEventBus.Instance == null) return;

            CoreEventBus.Instance.Subscribe<UnitDamagedEvent>(OnUnitDamaged);
            _damageEventsSubscribed = true;
        }


        private void UnsubscribeDamageEvents()
        {
            if (!_damageEventsSubscribed) return;

            CoreEventBus.Instance?.Unsubscribe<UnitDamagedEvent>(OnUnitDamaged);
            _damageEventsSubscribed = false;
        }


        private void OnUnitDamaged(UnitDamagedEvent gameEvent)
        {
            if (!Application.isPlaying || !showDamagePopups ||
                gameEvent is { Amount: <= 0f } or { UnitObject: null })
                return;

            var isPlayer = gameEvent.Role == UnitRole.Player;
            var isEnemy = gameEvent.Role is UnitRole.Zombie or UnitRole.Enemy or UnitRole.Bandit;

            if (isPlayer && showPlayerDamagePopups)
            {
                ShowDamagePopupForTarget(
                    gameEvent.Amount,
                    gameEvent.UnitObject.transform,
                    playerDamagePopupHeight,
                    playerDamagePopupLifetime,
                    playerDamagePopupRisePixels,
                    playerDamagePopupJitter,
                    playerDamagePopupFontSize,
                    playerDamagePopupColor,
                    true);
                return;
            }

            if (isPlayer || !isEnemy || !showEnemyDamagePopups) return;

            ShowDamagePopupForTarget(
                gameEvent.Amount,
                gameEvent.UnitObject.transform,
                enemyDamagePopupHeight,
                enemyDamagePopupLifetime,
                enemyDamagePopupRisePixels,
                enemyDamagePopupJitter,
                enemyDamagePopupFontSize,
                enemyDamagePopupColor,
                false);
        }


        private void ShowDamagePopupForTarget(
            float amount,
            Transform followTarget,
            float popupHeight,
            float popupLifetime,
            float popupRisePixels,
            Vector2 popupJitter,
            float popupFontSize,
            Color popupColor,
            bool includeHpSuffix)
        {
            if (amount <= 0f || followTarget == null) return;

            if (!EnsureDamagePopupRoot()) return;

            var popup = GetDamagePopupView();
            if (popup?.Root == null || popup.Label == null) return;

            var popupRoot = popup.Root;
            var label = popup.Label;
            popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
            popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
            popupRoot.pivot = new Vector2(0.5f, 0.5f);
            popupRoot.anchoredPosition = Vector2.zero;
            popupRoot.sizeDelta = new Vector2(220f, 48f);

            var damageValue = Mathf.Max(1, Mathf.RoundToInt(amount));
            var suffix = includeHpSuffix ? " HP" : string.Empty;
            label.text = $"-{damageValue}{suffix}";
            label.fontSize = popupFontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = popupColor;

            popup.FollowTarget = followTarget;
            popup.WorldAnchor = followTarget.position + Vector3.up * popupHeight;
            popup.Jitter = new Vector2(
                Random.Range(-popupJitter.x, popupJitter.x),
                Random.Range(-popupJitter.y, popupJitter.y));
            popup.Age = 0f;
            popup.Height = popupHeight;
            popup.Lifetime = popupLifetime;
            popup.RisePixels = popupRisePixels;
            popup.BaseColor = popupColor;

            _damagePopups.Add(popup);
            UpdateDamagePopups();
        }


        private bool EnsureDamagePopupRoot()
        {
            if (_damagePopupRoot == null)
            {
                if (transform.Find("DamagePopups") is RectTransform existing)
                {
                    _damagePopupRoot = existing;
                }
                else
                {
                    _damagePopupRoot = MakeRect("DamagePopups", transform);
                    var group = _damagePopupRoot.gameObject.AddComponent<CanvasGroup>();
                    group.interactable = false;
                    group.blocksRaycasts = false;
                }
            }

            if (_damagePopupRoot == null) return false;

            _damagePopupRoot.anchorMin = Vector2.zero;
            _damagePopupRoot.anchorMax = Vector2.one;
            _damagePopupRoot.pivot = new Vector2(0.5f, 0.5f);
            _damagePopupRoot.anchoredPosition = Vector2.zero;
            _damagePopupRoot.sizeDelta = Vector2.zero;
            _damagePopupRoot.SetAsLastSibling();
            return true;
        }


        private void UpdateDamagePopups()
        {
            if (_damagePopups.Count == 0) return;

            if (!EnsureDamagePopupRoot()) return;

            var worldCamera = ResolvePopupCamera();
            if (!IsCameraUsable(worldCamera)) return;

            for (var i = _damagePopups.Count - 1; i >= 0; i--)
            {
                var popup = _damagePopups[i];
                var result = DamagePopupProjectionHelper.UpdatePopupProjection(
                    popup,
                    _damagePopupRoot,
                    worldCamera,
                    Time.unscaledDeltaTime);

                if (result == DamagePopupProjectionResult.Invalid)
                {
                    ReleaseDamagePopupView(popup);
                    _damagePopups.RemoveAt(i);
                    continue;
                }

                if (result != DamagePopupProjectionResult.Expired) continue;

                ReleaseDamagePopupView(popup);
                _damagePopups.RemoveAt(i);
            }
        }


        private Camera ResolvePopupCamera()
        {
            _popupCamera = DamagePopupProjectionHelper.ResolvePopupCamera(
                _popupCamera,
                Time.unscaledTime,
                ref _nextPopupCameraFullScanTime,
                PopupCameraFullScanIntervalSeconds);
            return _popupCamera;
        }


        private static bool IsCameraUsable(Camera camera)
        {
            return DamagePopupProjectionHelper.IsCameraUsable(camera);
        }


        private void ClearDamagePopups()
        {
            for (var i = _damagePopups.Count - 1; i >= 0; i--)
            {
                var popup = _damagePopups[i];
                ReleaseDamagePopupView(popup);
            }

            _damagePopups.Clear();
        }


        private DamagePopupView GetDamagePopupView()
        {
            if (!EnsureDamagePopupRoot()) return null;

            return (_damagePopupPool ??= CreateDamagePopupPool()).Get();
        }


        private IObjectPool<DamagePopupView> CreateDamagePopupPool()
        {
            return new ObjectPool<DamagePopupView>(
                CreateDamagePopupView,
                OnGetDamagePopupView,
                OnReleaseDamagePopupView,
                OnDestroyDamagePopupView,
                false,
                DamagePopupPoolDefaultCapacity,
                DamagePopupPoolMaxSize);
        }


        private DamagePopupView CreateDamagePopupView()
        {
            if (!EnsureDamagePopupRoot()) return null;

            var popupRoot = MakeRect("DamagePopup", _damagePopupRoot);
            popupRoot.anchorMin = new Vector2(0.5f, 0.5f);
            popupRoot.anchorMax = new Vector2(0.5f, 0.5f);
            popupRoot.pivot = new Vector2(0.5f, 0.5f);
            popupRoot.anchoredPosition = Vector2.zero;
            popupRoot.sizeDelta = new Vector2(220f, 48f);

            var label = MakeText("Label", popupRoot, string.Empty, playerDamagePopupFontSize);
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;

            return new DamagePopupView
            {
                Root = popupRoot,
                Label = label
            };
        }


        private static void OnGetDamagePopupView(DamagePopupView popup)
        {
            if (popup?.Root == null) return;

            popup.Age = 0f;
            popup.Root.localScale = Vector3.one;
            popup.Root.gameObject.SetActive(true);
        }


        private void OnReleaseDamagePopupView(DamagePopupView popup)
        {
            if (popup == null) return;

            popup.Age = 0f;
            popup.FollowTarget = null;
            popup.WorldAnchor = Vector3.zero;
            popup.Jitter = Vector2.zero;
            popup.Height = 0f;
            popup.Lifetime = 0f;
            popup.RisePixels = 0f;

            if (popup.Label != null)
            {
                popup.Label.text = string.Empty;
                popup.Label.color = popup.BaseColor;
            }

            if (popup.Root == null) return;

            if (_damagePopupRoot != null && popup.Root.parent != _damagePopupRoot)
                popup.Root.SetParent(_damagePopupRoot, false);

            popup.Root.localScale = Vector3.one;
            popup.Root.anchoredPosition = Vector2.zero;
            popup.Root.gameObject.SetActive(false);
        }


        private static void OnDestroyDamagePopupView(DamagePopupView popup)
        {
            if (popup?.Root == null) return;

            if (Application.isPlaying)
                Destroy(popup.Root.gameObject);
            else
                DestroyImmediate(popup.Root.gameObject);
        }


        private void ReleaseDamagePopupView(DamagePopupView popup)
        {
            if (popup == null) return;

            if (_damagePopupPool == null)
            {
                OnDestroyDamagePopupView(popup);
                return;
            }

            _damagePopupPool.Release(popup);
        }


        private enum DamagePopupProjectionResult
        {
            Alive,
            Hidden,
            Expired,
            Invalid
        }


        private static class DamagePopupProjectionHelper
        {
            internal static Camera ResolvePopupCamera(
                Camera cachedCamera,
                float now,
                ref float nextPopupCameraFullScanTime,
                float fullScanIntervalSeconds)
            {
                if (IsCameraUsable(cachedCamera)) return cachedCamera;

                if (IsCameraUsable(Zombera.Core.CameraRegistry.Main)) return Zombera.Core.CameraRegistry.Main;

                if (now < nextPopupCameraFullScanTime) return cachedCamera;

                nextPopupCameraFullScanTime = now + fullScanIntervalSeconds;

                var cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                return Array.Find(cameras, IsCameraUsable);
            }

            internal static bool IsCameraUsable(Camera camera)
            {
                return camera != null && camera.isActiveAndEnabled && camera.gameObject.activeInHierarchy;
            }

            internal static DamagePopupProjectionResult UpdatePopupProjection(
                DamagePopupView popup,
                RectTransform popupRoot,
                Camera worldCamera,
                float unscaledDeltaTime)
            {
                if (popup == null || popup.Root == null || popup.Label == null) return DamagePopupProjectionResult.Invalid;

                popup.Age += unscaledDeltaTime;
                var lifetime = Mathf.Max(0.05f, popup.Lifetime);
                var t = popup.Age / lifetime;

                if (t >= 1f)
                    return DamagePopupProjectionResult.Expired;

                var worldAnchor = popup.FollowTarget != null
                    ? popup.FollowTarget.position + Vector3.up * popup.Height
                    : popup.WorldAnchor;
                var screenPoint = worldCamera.WorldToScreenPoint(worldAnchor);

                if (screenPoint.z <= 0f)
                {
                    popup.Root.gameObject.SetActive(false);
                    return DamagePopupProjectionResult.Hidden;
                }

                if (!popup.Root.gameObject.activeSelf) popup.Root.gameObject.SetActive(true);

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(popupRoot, screenPoint, null,
                        out var localPoint))
                {
                    localPoint += popup.Jitter;
                    localPoint.y += Mathf.Lerp(0f, popup.RisePixels, t);
                    popup.Root.anchoredPosition = localPoint;
                }

                var color = popup.BaseColor;
                color.a = Mathf.Clamp01(1f - t);
                popup.Label.color = color;

                var scale = Mathf.Lerp(1f, 1.08f, t);
                popup.Root.localScale = new Vector3(scale, scale, 1f);

                return DamagePopupProjectionResult.Alive;
            }
        }
    }
}
