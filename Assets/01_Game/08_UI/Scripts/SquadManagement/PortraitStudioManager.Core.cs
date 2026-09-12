using System;
using System.Collections;
using System.Collections.Generic;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;
using Zombera.Characters;
using Zombera.Core;
using Zombera.UI;
using Zombera.UI.Menus.CharacterCreation;

namespace Zombera.UI.SquadManagement
{
    public sealed class PortraitStudioRenderResult
    {
        public string UnitKey = string.Empty;
        public bool Success;
        public RenderTexture Texture;
        public Sprite CachedSprite;
    }

    /// <summary>
    ///     Manages a hidden 3D studio for rendering character portraits to a RenderTexture.
    ///     Supports per-character sync, caching, and UI/HUD consumption.
    /// </summary>
    public sealed partial class PortraitStudioManager : MonoBehaviour
    {
        private static PortraitStudioManager _instance;

        public static PortraitStudioManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<PortraitStudioManager>();

                return _instance;
            }
        }

        [Header("Studio Setup")]
        [SerializeField] private DynamicCharacterAvatar studioAvatar;
        [SerializeField] private Camera studioCamera;
        [SerializeField] private RenderTexture portraitRT;
        [SerializeField] private Vector3 studioOffset = new Vector3(10000f, -10000f, 10000f);

        [Header("Framing")]
        [SerializeField] private Transform portraitAnchor;
        [SerializeField] [Min(0.2f)] private float headshotDistance = 1.15f;
        [SerializeField] private float headshotVerticalOffset = 0.04f;
        [SerializeField] private float headshotLookOffset = 0.06f;
        [SerializeField] [Range(15f, 70f)] private float headshotFieldOfView = 22f;

        [Header("Generation")]
        [SerializeField] [Min(1f)] private float generationTimeoutSeconds = 4f;
        [SerializeField] private bool logGenerationWarnings = true;

        private readonly Dictionary<string, Sprite> _spriteCacheByUnitKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> _textureCacheByUnitKey = new(StringComparer.Ordinal);
        private readonly Queue<PortraitRequest> _pendingRequests = new(16);
        private readonly HashSet<string> _queuedUnitKeys = new(StringComparer.Ordinal);

        private bool _isGenerating;
        private Coroutine _renderRoutine;

        private struct PortraitRequest
        {
            public GameObject UnitRoot;
            public string UnitKey;
        }

        public RenderTexture PortraitTexture => portraitRT;

        public event Action<PortraitStudioRenderResult> PortraitRendered;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.position = studioOffset;

            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);

            ValidateStudio();
            SetupStudio();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (studioAvatar != null) studioAvatar.CharacterUpdated.RemoveListener(OnCharacterGenerated);
            ClearPortraitCache();
        }

        public void ValidateStudio()
        {
            if (studioAvatar == null) Debug.LogWarning("[PortraitStudioManager] studioAvatar is not assigned.", this);
            if (studioCamera == null) Debug.LogWarning("[PortraitStudioManager] studioCamera is not assigned.", this);
            if (portraitRT == null) Debug.LogError("[PortraitStudioManager] portraitRT is missing! Portraits will not render.", this);
            if (portraitAnchor == null) Debug.LogWarning("[PortraitStudioManager] portraitAnchor is not assigned.", this);
        }

        private void SetupStudio()
        {
            if (studioCamera != null)
            {
                studioCamera.targetTexture = portraitRT;
                studioCamera.enabled = false;
                studioCamera.allowHDR = false;
                ConfigureStudioCameraCullingMask();
            }

            if (studioAvatar != null)
            {
                UmaAnimationControllerUtility.EnsureAnimationController(studioAvatar);
                studioAvatar.CharacterUpdated.AddListener(OnCharacterGenerated);
                EnsureStudioAvatarLayers();
            }
        }

        public void SyncWithPlayer(DynamicCharacterAvatar playerAvatar)
        {
            if (playerAvatar == null) return;
            RefreshPortraitFromUnitRoot(playerAvatar.gameObject, ResolveUnitKey(playerAvatar.gameObject));
        }

        public void RefreshPortrait(DynamicCharacterAvatar targetAvatar)
        {
            if (targetAvatar == null) return;
            RefreshPortraitFromUnitRoot(targetAvatar.gameObject, ResolveUnitKey(targetAvatar.gameObject));
        }

        public void RefreshPortraitFromUnit(Unit unit, bool forceRefresh = false)
        {
            if (unit == null) return;
            RefreshPortraitFromUnitRoot(unit.gameObject, ResolveUnitKey(unit.gameObject), forceRefresh);
        }

        public void RefreshPortraitFromUnitRoot(GameObject unitRoot, string unitKey = null, bool forceRefresh = false)
        {
            if (unitRoot == null)
            {
                Debug.LogWarning("[PortraitStudioManager] RefreshPortraitFromUnitRoot called with null unitRoot.");
                return;
            }

            if (studioAvatar == null)
            {
                Debug.LogError("[PortraitStudioManager] studioAvatar is null. Cannot refresh portrait.", this);
                return;
            }

            var resolvedKey = string.IsNullOrWhiteSpace(unitKey) ? ResolveUnitKey(unitRoot) : unitKey;
            if (string.IsNullOrWhiteSpace(resolvedKey)) return;

            if (!forceRefresh && TryGetCachedPortrait(resolvedKey, out _))
                return;

            RemoveQueuedRequestsForKey(resolvedKey);

            _pendingRequests.Enqueue(new PortraitRequest
            {
                UnitRoot = unitRoot,
                UnitKey = resolvedKey
            });
            _queuedUnitKeys.Add(resolvedKey);

            if (_renderRoutine == null)
                _renderRoutine = StartCoroutine(ProcessPortraitQueueRoutine());
        }

        private void RemoveQueuedRequestsForKey(string unitKey)
        {
            if (string.IsNullOrWhiteSpace(unitKey) || _pendingRequests.Count == 0) return;

            var retained = new Queue<PortraitRequest>(_pendingRequests.Count);
            while (_pendingRequests.Count > 0)
            {
                var request = _pendingRequests.Dequeue();
                if (string.Equals(request.UnitKey, unitKey, StringComparison.Ordinal))
                {
                    _queuedUnitKeys.Remove(request.UnitKey);
                    continue;
                }

                retained.Enqueue(request);
            }

            while (retained.Count > 0)
                _pendingRequests.Enqueue(retained.Dequeue());
        }

        public bool TryGetCachedPortrait(string unitKey, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(unitKey)) return false;
            return _spriteCacheByUnitKey.TryGetValue(unitKey, out sprite) && sprite != null;
        }

        public void ClearPortraitCache()
        {
            foreach (var texture in _textureCacheByUnitKey.Values)
            {
                if (texture == null) continue;
                if (Application.isPlaying) Destroy(texture);
                else DestroyImmediate(texture);
            }

            _textureCacheByUnitKey.Clear();
            _spriteCacheByUnitKey.Clear();
        }

        private IEnumerator ProcessPortraitQueueRoutine()
        {
            while (_pendingRequests.Count > 0)
            {
                var request = _pendingRequests.Dequeue();
                _queuedUnitKeys.Remove(request.UnitKey);

                if (request.UnitRoot == null) continue;

                yield return SyncRoutine(request.UnitRoot, request.UnitKey);
            }

            _renderRoutine = null;
        }

    }
}
