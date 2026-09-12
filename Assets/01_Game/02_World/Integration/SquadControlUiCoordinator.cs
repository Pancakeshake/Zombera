#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Systems;
using Zombera.UI;
using Object = UnityEngine.Object;

#endregion

namespace Zombera.Characters
{
    public sealed class SquadControlUiCoordinator
    {
        private const float PortraitRebindIntervalSeconds = 2f;
        private const float PortraitSelectionSyncIntervalSeconds = 0.15f;
        private readonly Action<Unit> _activateControlledUnit;

        private readonly List<SquadPortraitStrip> _boundPortraitStrips = new(4);
        private readonly Func<Unit> _getActiveControlledUnit;
        private readonly Func<Unit, bool> _isControllableSquadUnit;
        private float _nextPortraitRebindAt;
        private float _nextPortraitSelectionSyncAt;
        private Unit _lastPortraitSelectionTarget;
        private bool _refreshingPortraitBindings;
        private bool _syncingPortraitSelection;

        public SquadControlUiCoordinator(
            Func<Unit, bool> isControllableSquadUnit,
            Action<Unit> activateControlledUnit,
            Func<Unit> getActiveControlledUnit)
        {
            _isControllableSquadUnit = isControllableSquadUnit;
            _activateControlledUnit = activateControlledUnit;
            _getActiveControlledUnit = getActiveControlledUnit;
        }

        public void TickBindPortraitStrips()
        {
            TryBindPortraitStrips();
        }

        public void UnbindPortraitStripCallbacks()
        {
            foreach (var strip in _boundPortraitStrips)
            {
                if (strip == null) continue;
                strip.OnPortraitClicked -= HandlePortraitStripClicked;
            }

            _boundPortraitStrips.Clear();
            _refreshingPortraitBindings = false;
            _syncingPortraitSelection = false;
            _nextPortraitRebindAt = 0f;
            _nextPortraitSelectionSyncAt = 0f;
            _lastPortraitSelectionTarget = null;
        }

        public void TryBindPortraitStrips()
        {
            var active = _getActiveControlledUnit?.Invoke();
            if (active == null) return;

            PruneBoundPortraitStrips();
            if (_boundPortraitStrips.Count > 0)
            {
                if (Time.unscaledTime >= _nextPortraitRebindAt)
                {
                    _refreshingPortraitBindings = true;
                    try
                    {
                        foreach (var strip in _boundPortraitStrips)
                        {
                            if (strip == null) continue;
                            strip.RefreshBindings();
                        }
                    }
                    finally
                    {
                        _refreshingPortraitBindings = false;
                    }

                    _nextPortraitRebindAt = Time.unscaledTime + PortraitRebindIntervalSeconds;
                }

                if (active != _lastPortraitSelectionTarget || Time.unscaledTime >= _nextPortraitSelectionSyncAt)
                    SyncPortraitSelection(active);
                return;
            }

            var strips =
                Object.FindObjectsByType<SquadPortraitStrip>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (strips == null || strips.Length == 0) return;

            foreach (var strip in strips)
            {
                if (strip == null) continue;

                strip.OnPortraitClicked -= HandlePortraitStripClicked;
                strip.OnPortraitClicked += HandlePortraitStripClicked;
                _boundPortraitStrips.Add(strip);
                _refreshingPortraitBindings = true;
                try
                {
                    strip.RefreshBindings();
                }
                finally
                {
                    _refreshingPortraitBindings = false;
                }
            }

            _nextPortraitRebindAt = Time.unscaledTime + PortraitRebindIntervalSeconds;

            SyncPortraitSelection(active);
        }

        private void HandlePortraitStripClicked(Unit selectedUnit)
        {
            if (_syncingPortraitSelection || _refreshingPortraitBindings) return;
            if (selectedUnit == null) return;

            var activeUnit = _getActiveControlledUnit?.Invoke();
            if (activeUnit != selectedUnit)
            {
                if (_isControllableSquadUnit != null && !_isControllableSquadUnit(selectedUnit)) return;

                _activateControlledUnit?.Invoke(selectedUnit);
            }
        }

        public void SyncPortraitSelection(Unit unit)
        {
            if (unit == null || _boundPortraitStrips.Count == 0) return;

            _syncingPortraitSelection = true;
            try
            {
                foreach (var strip in _boundPortraitStrips)
                {
                    if (strip == null) continue;
                    if (strip.SelectedUnit != unit) strip.TrySelectUnit(unit, false);
                }
            }
            finally
            {
                _syncingPortraitSelection = false;
            }

            _lastPortraitSelectionTarget = unit;
            _nextPortraitSelectionSyncAt = Time.unscaledTime + PortraitSelectionSyncIntervalSeconds;
        }

        private void PruneBoundPortraitStrips()
        {
            _boundPortraitStrips.RemoveAll(static strip => strip == null);
        }
    }
}