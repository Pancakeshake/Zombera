using UnityEngine;

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Swaps between detail (multi-piece, destructible) and proxy (single mesh)
    ///     representations of a building's roof, walls, and props based on distance,
    ///     combat events, and interaction.
    ///
    ///     Attach to the root of a generated building prefab.
    ///     By default the detail hierarchy is active and the proxy is hidden.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingLODController : MonoBehaviour
    {
        [Header("Children")]
        [Tooltip("The detail parent transforms under this root. These are hidden when proxy is active.")]
        [SerializeField]
        private Transform[] detailParents;

        [Tooltip("The proxy GameObject child (single combined mesh). Shown when detail is hidden.")]
        [SerializeField]
        private GameObject proxyRoot;

        [Header("Distance")]
        [Tooltip("Beyond this distance (metres from main camera), switch to proxy.")]
        [SerializeField]
        [Min(1f)]
        private float proxyDistance = 50f;

        [Tooltip("Hysteresis margin so we don't flip every frame at the boundary.")]
        [SerializeField]
        [Min(1f)]
        private float hysteresisMeters = 5f;

        [Header("Combat")]
        [Tooltip("Force detail mode for this many seconds after the building takes damage.")]
        [SerializeField]
        [Min(0f)]
        private float combatDetailDuration = 8f;

        [Tooltip("Force detail mode for this many seconds after a door opens/closes.")]
        [SerializeField]
        [Min(0f)]
        private float interactionDetailDuration = 4f;

        private Transform _cameraTransform;
        private float _combatDetailUntil;
        private float _interactionDetailUntil;
        private bool _isProxyActive;

        private void Awake()
        {
            // Default: detail active, proxy hidden
            SetProxyActive(false);
        }

        private void Start()
        {
            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            if (_cameraTransform == null)
            {
                if (Camera.main != null)
                    _cameraTransform = Camera.main.transform;
                return;
            }

            var dist = Vector3.Distance(_cameraTransform.position, transform.position);
            var wantProxy = ShouldUseProxy(dist);

            if (wantProxy != _isProxyActive)
                SetProxyActive(wantProxy);
        }

        private bool ShouldUseProxy(float distance)
        {
            // Combat / interaction override: force detail
            if (Time.unscaledTime < _combatDetailUntil || Time.unscaledTime < _interactionDetailUntil)
                return false;

            // Distance check with hysteresis
            if (_isProxyActive)
                return distance > proxyDistance - hysteresisMeters;
            else
                return distance > proxyDistance + hysteresisMeters;
        }

        private void SetProxyActive(bool active)
        {
            _isProxyActive = active;

            if (proxyRoot != null)
                proxyRoot.SetActive(active);

            foreach (var dp in detailParents)
            {
                if (dp != null)
                    dp.gameObject.SetActive(!active);
            }
        }

        /// <summary>
        ///     Call from StructureHealth.Damaged event to force detail mode temporarily.
        /// </summary>
        public void OnBuildingDamaged()
        {
            _combatDetailUntil = Time.unscaledTime + combatDetailDuration;
            if (_isProxyActive)
                SetProxyActive(false);
        }

        /// <summary>
        ///     Call from DoorController / DoorInteractor when a door is used.
        /// </summary>
        public void OnPlayerInteracted()
        {
            _interactionDetailUntil = Time.unscaledTime + interactionDetailDuration;
            if (_isProxyActive)
                SetProxyActive(false);
        }

        /// <summary>
        ///     Force a specific mode. Useful for cutscenes or scripted events.
        /// </summary>
        public void ForceDetail(bool detail)
        {
            if (detail && _isProxyActive)
                SetProxyActive(false);
            else if (!detail && !_isProxyActive)
                SetProxyActive(true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (hysteresisMeters >= proxyDistance)
                hysteresisMeters = proxyDistance * 0.5f;
        }
#endif
    }
}
