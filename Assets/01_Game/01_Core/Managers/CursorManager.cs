#region

using UnityEngine;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Applies cursor icons from a <see cref="CursorProfile"/> through <see cref="CursorService"/>.
    /// </summary>
    public sealed class CursorManager : MonoBehaviour
    {
        private const string DefaultProfileResourcePath = "DefaultCursorProfile";

        private static CursorManager _runtimeInstance;

        public static CursorManager RuntimeInstance => _runtimeInstance;

        [SerializeField] private CursorProfile cursorProfile;

        private readonly CursorHotspotResolver _hotspotResolver = new();
        private readonly CursorIconCatalog _iconCatalog = new();
        private readonly CursorIconResolver _iconResolver = new();
        private readonly CursorRuntimeCalibrationInput _calibrationInput = new();

        private CursorIconIntent _activeIconIntent = CursorIconIntent.Default;
        private Vector2 _cachedDefaultOffset;
        private Vector2 _cachedAttackOffset;

        public CursorProfile Profile => cursorProfile;
        public CursorIconIntent ActiveIconIntent => _activeIconIntent;

        private void Awake()
        {
            if (cursorProfile == null)
                cursorProfile = Resources.Load<CursorProfile>(DefaultProfileResourcePath);

            if (cursorProfile == null || !cursorProfile.HasAnyIcon())
            {
                enabled = false;
                return;
            }

            _runtimeInstance = this;

            CursorService.BindProfile(cursorProfile);

            RebuildFromProfile();
            CacheProfileOffsets();
            CursorService.EnsureDefaultState();
            ApplyCursor(CursorIconIntent.Default);
        }

        private void OnDestroy()
        {
            CursorService.UnbindProfile(cursorProfile);
            if (_runtimeInstance == this) _runtimeInstance = null;
        }

        private void Update()
        {
            if (_calibrationInput.Tick(cursorProfile, out _) || ProfileOffsetsChanged())
            {
                CacheProfileOffsets();
                ApplyCursor(_activeIconIntent);
            }
        }

        private void OnEnable()
        {
            CursorService.EnsureDefaultState();
        }

        public void SetAttackHover(bool overZombie)
        {
            ApplyCursor(overZombie ? CursorIconIntent.Attack : CursorIconIntent.Default);
        }

        public void ForceDefaultCursor()
        {
            ApplyCursor(CursorIconIntent.Default);
        }

        public void SetIconIntent(CursorIconIntent intent)
        {
            ApplyCursor(intent);
        }

        public void SetProfile(CursorProfile newProfile)
        {
            CursorService.UnbindProfile(cursorProfile);
            cursorProfile = newProfile;
            if (cursorProfile == null || !cursorProfile.HasAnyIcon()) return;

            CursorService.BindProfile(cursorProfile);
            RebuildFromProfile();
            CacheProfileOffsets();
            ApplyCursor(_activeIconIntent);
        }

        private void RebuildFromProfile()
        {
            cursorProfile.PopulateCatalog(_iconCatalog);
            _iconResolver.Configure(_iconCatalog, _hotspotResolver, cursorProfile.AutoDetectAlphaThreshold);
            cursorProfile.ConfigureCalibrationInput(_calibrationInput);
        }

        private void ApplyCursor(CursorIconIntent intent)
        {
            _activeIconIntent = intent;
            CursorService.SetActiveIconIntent(intent);

            var resolved = _iconResolver.Resolve(intent);
            var hotspotOffset = cursorProfile.GetHotspotOffset(intent);
            var hotspot = CursorHotspotResolver.ClampHotspot(
                resolved.Texture,
                resolved.Hotspot + hotspotOffset);

            CursorService.RegisterAppliedHotspot(hotspot);
            CursorService.ApplyCursorIcon(resolved.Texture, hotspot, CursorMode.Auto);
        }

        private void CacheProfileOffsets()
        {
            _cachedDefaultOffset = cursorProfile.GetHotspotOffset(CursorIconIntent.Default);
            _cachedAttackOffset = cursorProfile.GetHotspotOffset(CursorIconIntent.Attack);
        }

        private bool ProfileOffsetsChanged()
        {
            var defaultOffset = cursorProfile.GetHotspotOffset(CursorIconIntent.Default);
            if ((defaultOffset - _cachedDefaultOffset).sqrMagnitude > 0.001f) return true;

            var attackOffset = cursorProfile.GetHotspotOffset(CursorIconIntent.Attack);
            return (attackOffset - _cachedAttackOffset).sqrMagnitude > 0.001f;
        }
    }
}
