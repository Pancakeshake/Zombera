using UnityEngine;
using Zombera.AI;
using Zombera.Characters;

namespace Zombera.Systems
{
    /// <summary>
    /// Keeps the OS cursor visible and swaps it to an attack cursor when the mouse
    /// hovers over a live zombie unit.
    ///
    /// Setup:
    ///   1. Import a default cursor PNG and a sword/attack cursor PNG into the project.
    ///   2. Set each Texture2D's Texture Type to "Cursor" in the Inspector.
    ///   3. Add this component anywhere in the scene (e.g. the same GameObject as
    ///      PlayerInputController) and assign the textures + hotspots in the Inspector.
    /// </summary>
    public sealed class CursorManager : MonoBehaviour
    {
        private enum HotspotAutoAnchor
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Center
        }

        [Header("Cursor Textures")]
        [Tooltip("Normal pointer cursor. Leave null to use the OS default.")]
        [SerializeField] private Texture2D defaultCursor;

        [Tooltip("Sword / attack cursor shown when hovering over a zombie.")]
        [SerializeField] private Texture2D attackCursor;

        [Tooltip("Pixel offset from the top-left of the default cursor texture to use as the click point.")]
        [SerializeField] private Vector2 defaultHotspot = Vector2.zero;

        [Tooltip("Pixel offset from the top-left of the attack cursor texture to use as the click point. " +
                 "For a sword pointing top-right, try the blade tip (e.g. 0,0).")]
        [SerializeField] private Vector2 attackHotspot = Vector2.zero;

        [Tooltip("When hotspot is (0,0), auto-center the default cursor click point to avoid perceived offset.")]
        [SerializeField] private bool autoCenterDefaultHotspotWhenUnset = false;

        [Tooltip("When hotspot is (0,0), auto-center the attack cursor click point.")]
        [SerializeField] private bool autoCenterAttackHotspotWhenUnset = false;

        [Tooltip("Additional hotspot offset for default cursor in pixels. Positive Y moves the click point downward.")]
        [SerializeField] private Vector2 defaultHotspotOffset = Vector2.zero;

        [Tooltip("Additional hotspot offset for attack cursor in pixels. Positive Y moves the click point downward.")]
        [SerializeField] private Vector2 attackHotspotOffset = Vector2.zero;

        [Tooltip("For default UI cursor, prefer top-left hotspot when not explicitly configured. Prevents center-hotspot drift from older serialized settings.")]
        [SerializeField] private bool forceTopLeftDefaultHotspotWhenUnset = true;

        [Header("Auto Hotspot Detection")]
        [Tooltip("When hotspot is (0,0), detect a click point from opaque pixels in the cursor texture.")]
        [SerializeField] private bool autoDetectDefaultHotspotWhenUnset = true;

        [Tooltip("Anchor used for default cursor auto-detection.")]
        [SerializeField] private HotspotAutoAnchor defaultAutoHotspotAnchor = HotspotAutoAnchor.TopLeft;

        [Tooltip("When hotspot is (0,0), detect a click point from opaque pixels in the attack cursor texture.")]
        [SerializeField] private bool autoDetectAttackHotspotWhenUnset = true;

        [Tooltip("Anchor used for attack cursor auto-detection.")]
        [SerializeField] private HotspotAutoAnchor attackAutoHotspotAnchor = HotspotAutoAnchor.TopRight;

        [Tooltip("Minimum alpha considered solid when auto-detecting hotspot.")]
        [SerializeField, Range(0.01f, 1f)] private float autoDetectAlphaThreshold = 0.2f;

        [Header("Detection")]
        [Tooltip("World camera used for hover raycasting (set by PlayerInputController).")]
        [SerializeField] private Camera worldCamera;

        private bool _isShowingAttackCursor;
        private readonly System.Collections.Generic.Dictionary<int, Vector2> autoHotspotCache =
            new System.Collections.Generic.Dictionary<int, Vector2>();

        private void Awake()
        {
            if (worldCamera == null)
                worldCamera = Camera.main;

            ApplyCursorHotspotMigration();

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            ApplyCursor(false);
        }

        private void OnEnable()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // Re-assert every frame so nothing else can steal visibility away.
        private void Update()
        {
            if (!Cursor.visible) Cursor.visible = true;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>
        /// Called each frame by PlayerInputController with the result of its own hover raycast.
        /// </summary>
        public void SetAttackHover(bool overZombie)
        {
            if (overZombie == _isShowingAttackCursor)
                return;

            _isShowingAttackCursor = overZombie;
            ApplyCursor(overZombie);
        }

        private void ApplyCursor(bool attack)
        {
            Texture2D tex = attack ? attackCursor : defaultCursor;
            Vector2 configuredHotspot = attack ? attackHotspot : defaultHotspot;
            bool autoCenterWhenUnset = attack ? autoCenterAttackHotspotWhenUnset : autoCenterDefaultHotspotWhenUnset;
            bool autoDetectWhenUnset = attack ? autoDetectAttackHotspotWhenUnset : autoDetectDefaultHotspotWhenUnset;
            HotspotAutoAnchor autoAnchor = attack ? attackAutoHotspotAnchor : defaultAutoHotspotAnchor;

            if (!attack && forceTopLeftDefaultHotspotWhenUnset && configuredHotspot.sqrMagnitude <= 0.0001f)
            {
                autoCenterWhenUnset = false;
            }

            Vector2 baseHotspot = ResolveHotspot(
                tex,
                configuredHotspot,
                autoCenterWhenUnset,
                autoDetectWhenUnset,
                autoAnchor,
                autoDetectAlphaThreshold);
            Vector2 hotspotOffset = attack ? attackHotspotOffset : defaultHotspotOffset;
            Vector2 hotspot = ClampHotspot(tex, baseHotspot + hotspotOffset);
            // Passing null resets to the OS default cursor.
            Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
        }

        private void ApplyCursorHotspotMigration()
        {
            if (!forceTopLeftDefaultHotspotWhenUnset)
            {
                return;
            }

            if (defaultHotspot.sqrMagnitude > 0.0001f)
            {
                return;
            }

            if (defaultHotspotOffset.sqrMagnitude > 0.0001f)
            {
                return;
            }

            autoCenterDefaultHotspotWhenUnset = false;
        }

        private Vector2 ResolveHotspot(
            Texture2D texture,
            Vector2 configuredHotspot,
            bool autoCenterWhenUnset,
            bool autoDetectWhenUnset,
            HotspotAutoAnchor autoAnchor,
            float alphaThreshold)
        {
            if (texture == null)
            {
                return configuredHotspot;
            }

            if (configuredHotspot.sqrMagnitude > 0.0001f)
            {
                return configuredHotspot;
            }

            if (autoDetectWhenUnset && TryResolveAutoHotspot(texture, autoAnchor, alphaThreshold, out Vector2 detectedHotspot))
            {
                return detectedHotspot;
            }

            if (!autoCenterWhenUnset)
            {
                return configuredHotspot;
            }

            return new Vector2(texture.width * 0.5f, texture.height * 0.5f);
        }

        private bool TryResolveAutoHotspot(Texture2D texture, HotspotAutoAnchor anchor, float alphaThreshold, out Vector2 hotspot)
        {
            hotspot = Vector2.zero;

            if (texture == null)
            {
                return false;
            }

            int thresholdByte = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(alphaThreshold) * 255f), 1, 255);
            int cacheKey = BuildHotspotCacheKey(texture, anchor, thresholdByte);

            if (autoHotspotCache.TryGetValue(cacheKey, out hotspot))
            {
                return true;
            }

            if (!TryResolveOpaqueAnchorHotspot(texture, anchor, thresholdByte, out hotspot))
            {
                return false;
            }

            autoHotspotCache[cacheKey] = hotspot;
            return true;
        }

        private static int BuildHotspotCacheKey(Texture2D texture, HotspotAutoAnchor anchor, int thresholdByte)
        {
            unchecked
            {
                int key = texture.GetInstanceID();
                key = (key * 397) ^ (int)anchor;
                key = (key * 397) ^ thresholdByte;
                return key;
            }
        }

        private static bool TryResolveOpaqueAnchorHotspot(Texture2D texture, HotspotAutoAnchor anchor, int thresholdByte, out Vector2 hotspot)
        {
            hotspot = Vector2.zero;

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException)
            {
                return false;
            }

            if (pixels == null || pixels.Length == 0)
            {
                return false;
            }

            int width = texture.width;
            int height = texture.height;
            int maxX = Mathf.Max(0, width - 1);
            int maxY = Mathf.Max(0, height - 1);
            float centerX = maxX * 0.5f;
            float centerY = maxY * 0.5f;

            int bestIndex = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a < thresholdByte)
                {
                    continue;
                }

                int x = i % width;
                int y = i / width;
                float score;

                switch (anchor)
                {
                    case HotspotAutoAnchor.TopRight:
                        score = (maxX - x) + y;
                        break;
                    case HotspotAutoAnchor.BottomLeft:
                        score = x + (maxY - y);
                        break;
                    case HotspotAutoAnchor.BottomRight:
                        score = (maxX - x) + (maxY - y);
                        break;
                    case HotspotAutoAnchor.Center:
                        float dx = x - centerX;
                        float dy = y - centerY;
                        score = (dx * dx) + (dy * dy);
                        break;
                    default:
                        score = x + y;
                        break;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            int resolvedX = bestIndex % width;
            int resolvedY = bestIndex / width;
            hotspot = new Vector2(resolvedX, resolvedY);
            return true;
        }

        private static Vector2 ClampHotspot(Texture2D texture, Vector2 hotspot)
        {
            if (texture == null)
            {
                return hotspot;
            }

            float maxX = Mathf.Max(0f, texture.width - 1f);
            float maxY = Mathf.Max(0f, texture.height - 1f);
            return new Vector2(Mathf.Clamp(hotspot.x, 0f, maxX), Mathf.Clamp(hotspot.y, 0f, maxY));
        }

        /// <summary>
        /// Allows external systems (e.g. UI open/close) to temporarily force the
        /// default cursor regardless of hover state.
        /// </summary>
        public void ForceDefaultCursor()
        {
            _isShowingAttackCursor = false;
            ApplyCursor(false);
        }

        /// <summary>
        /// Updates the camera used for hover raycasting (called by PlayerInputController
        /// when the camera is late-assigned).
        /// </summary>
        public void SetCamera(Camera camera)
        {
            worldCamera = camera;
        }
    }
}
