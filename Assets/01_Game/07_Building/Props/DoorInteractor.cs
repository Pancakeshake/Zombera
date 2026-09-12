#region

using System.Collections.Generic;
using UnityEngine;

#endregion

// ReSharper disable Unity.PerformanceCriticalCodeInvocation
// ReSharper disable Unity.PerformanceCriticalCodeNullComparison

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Scans periodically for nearby doors (for interaction prompts). Supports DoorScript.Door
    ///     (Free Wood Door Pack) and the custom DoorController. Assign a narrow <see cref="doorLayerMask"/>
    ///     (e.g. Doors layer only) for best performance.
    ///     Attach to the player unit alongside ContainerInteractor.
    /// </summary>
    public sealed class DoorInteractor : MonoBehaviour
    {
        [SerializeField] [Min(0.1f)] private float interactRadius = 2.5f;

        [Tooltip("Prefer a dedicated Doors layer so overlaps skip unrelated colliders.")]
        [SerializeField]
        private LayerMask doorLayerMask = ~0;

        [Tooltip("Seconds between proximity scans. Interact() always scans immediately.")]
        [SerializeField] [Min(0.02f)]
        private float doorScanIntervalSeconds = 0.12f;

        [Tooltip("When enabled, periodically scans for nearby doors for prompt/UI support.")]
        [SerializeField]
        private bool pollNearbyDoorsForPrompt;

        [Tooltip("If the mask is Everything, auto-narrow to Door/Doors layers when they exist.")]
        [SerializeField]
        private bool autoNarrowDoorLayerMask = true;

        private const int MaxDoorLookupCacheEntries = 512;

        private readonly Collider[] _overlapBuffer = new Collider[24];
        private readonly Dictionary<int, CachedDoorLookup> _doorLookupCache = new(128);
        private DoorController _nearestCustomDoor;

        // Nearest door component this frame (either type)
        private Component _nearestThirdPartyDoor;

        private float _nextDoorScanTime;
        private float _nextDoorLookupCacheClearAt;

        private struct CachedDoorLookup
        {
            public Component thirdPartyDoor;
            public DoorController customDoor;
            public bool hasDoor;
        }

        // ReSharper disable once UnusedMember.Global
        public bool HasNearbyDoor => _nearestThirdPartyDoor != null || _nearestCustomDoor != null;

        private void Awake()
        {
            if (autoNarrowDoorLayerMask && doorLayerMask == ~0)
                TryAutoNarrowDoorLayerMask();

            _nextDoorLookupCacheClearAt = Time.time + 10f;
        }

        private void Update()
        {
            if (!pollNearbyDoorsForPrompt) return;
            if (Time.time < _nextDoorScanTime) return;

            _nextDoorScanTime = Time.time + Mathf.Max(0.02f, doorScanIntervalSeconds);
            ClearNearestDoorRefs();
            FindNearestDoor();
        }

        /// <summary>Toggles the nearest door. Returns true if a door was found.</summary>
        public bool Interact()
        {
            ClearNearestDoorRefs();
            FindNearestDoor();

            if (!_nearestThirdPartyDoor)
            {
                if (!_nearestCustomDoor) return false;

                _nearestCustomDoor.Toggle();
                return true;
            }

            // Set swing direction before toggling: door swings away from the player.
            if (ThirdPartyDoorBridge.TryGetIsOpen(_nearestThirdPartyDoor, out var isOpen) && !isOpen)
            {
                var toPlayer = transform.position - _nearestThirdPartyDoor.transform.position;
                var dot = Vector3.Dot(_nearestThirdPartyDoor.transform.right, toPlayer.normalized);
                _ = ThirdPartyDoorBridge.TrySetOpenAngle(_nearestThirdPartyDoor, dot >= 0f ? -90f : 90f);
            }

            return ThirdPartyDoorBridge.TryOpenDoor(_nearestThirdPartyDoor);
        }

        private void ClearNearestDoorRefs()
        {
            _nearestThirdPartyDoor = null;
            _nearestCustomDoor = null;
        }

        private void FindNearestDoor()
        {
            MaybeClearDoorLookupCache();

            var hitCount = Physics.OverlapSphereNonAlloc(
                transform.position, interactRadius, _overlapBuffer, doorLayerMask,
                QueryTriggerInteraction.Collide);

            var nearestSqDist = float.MaxValue;
            var selfPos = transform.position;

            for (var i = 0; i < hitCount; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;

                if (!TryFindDoorComponents(col.transform, out var tp, out var cu)) continue;

                var anchor = tp != null ? tp.transform : cu.transform;
                var dx = anchor.position.x - selfPos.x;
                var dz = anchor.position.z - selfPos.z;
                var sqDist = dx * dx + dz * dz;
                if (sqDist >= nearestSqDist) continue;

                nearestSqDist = sqDist;
                _nearestThirdPartyDoor = tp;
                _nearestCustomDoor = cu;
            }
        }

        /// <summary>
        ///     Prefer walking up from the hit collider (cheap). Fallback: search under root if layouts split collider vs door.
        /// </summary>
        private bool TryFindDoorComponents(Transform start, out Component thirdParty, out DoorController custom)
        {
            thirdParty = null;
            custom = null;

            if (start == null) return false;

            var cacheKey = start.GetInstanceID();
            if (_doorLookupCache.TryGetValue(cacheKey, out var cached))
            {
                thirdParty = cached.thirdPartyDoor;
                custom = cached.customDoor;

                if (!cached.hasDoor) return false;
                if (thirdParty != null || custom != null) return true;

                _doorLookupCache.Remove(cacheKey);
                thirdParty = null;
                custom = null;
            }

            var t = start;
            for (var depth = 0; depth < 12 && t != null; depth++)
            {
                if (thirdParty == null) _ = ThirdPartyDoorBridge.TryGetOnTransform(t, out thirdParty);
                if (custom == null) t.TryGetComponent(out custom);

                if (thirdParty != null || custom != null)
                {
                    CacheDoorLookup(cacheKey, thirdParty, custom);
                    return true;
                }

                t = t.parent;
            }

            var root = start.root;
            if (root != null)
            {
                _ = ThirdPartyDoorBridge.TryGetOnTransform(root, out thirdParty);
                root.TryGetComponent(out custom);

                if (thirdParty == null) _ = ThirdPartyDoorBridge.TryGetInChildren(root, out thirdParty);
                if (custom == null) custom = root.GetComponentInChildren<DoorController>(true);
            }

            CacheDoorLookup(cacheKey, thirdParty, custom);
            return thirdParty != null || custom != null;
        }

        private void CacheDoorLookup(int cacheKey, Component thirdParty, DoorController custom)
        {
            if (_doorLookupCache.Count >= MaxDoorLookupCacheEntries)
                _doorLookupCache.Clear();

            _doorLookupCache[cacheKey] = new CachedDoorLookup
            {
                thirdPartyDoor = thirdParty,
                customDoor = custom,
                hasDoor = thirdParty != null || custom != null
            };
        }

        private void MaybeClearDoorLookupCache()
        {
            if (_doorLookupCache.Count == 0) return;
            if (Time.time < _nextDoorLookupCacheClearAt) return;

            _doorLookupCache.Clear();
            _nextDoorLookupCacheClearAt = Time.time + 10f;
        }

        private void TryAutoNarrowDoorLayerMask()
        {
            var mask = 0;

            var doorsLayer = LayerMask.NameToLayer("Doors");
            if (doorsLayer >= 0) mask |= 1 << doorsLayer;

            var doorLayer = LayerMask.NameToLayer("Door");
            if (doorLayer >= 0) mask |= 1 << doorLayer;

            if (mask != 0) doorLayerMask = mask;
        }
    }
}