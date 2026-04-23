using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Systems;
using Zombera.UI;

namespace Zombera.Characters
{
    public sealed class SquadControlUiCoordinator
    {
        private readonly MonoBehaviour _host;
        private readonly Func<Unit> _getSpawnedPlayer;
        private readonly Func<Unit, bool> _isControllableSquadUnit;
        private readonly Func<Unit, PlayerInputController> _ensurePlayerInputController;
        private readonly Action<Unit> _activateControlledUnit;
        private readonly Func<Unit> _getActiveControlledUnit;

        private readonly List<SquadPortraitStrip> _boundPortraitStrips = new List<SquadPortraitStrip>(4);
        private bool _syncingPortraitSelection;

        public SquadControlUiCoordinator(
            MonoBehaviour host,
            Func<Unit> getSpawnedPlayer,
            Func<Unit, bool> isControllableSquadUnit,
            Func<Unit, PlayerInputController> ensurePlayerInputController,
            Action<Unit> activateControlledUnit,
            Func<Unit> getActiveControlledUnit)
        {
            _host = host;
            _getSpawnedPlayer = getSpawnedPlayer;
            _isControllableSquadUnit = isControllableSquadUnit;
            _ensurePlayerInputController = ensurePlayerInputController;
            _activateControlledUnit = activateControlledUnit;
            _getActiveControlledUnit = getActiveControlledUnit;
        }

        public void TickBindPortraitStrips()
        {
            TryBindPortraitStrips();
        }

        public void UnbindPortraitStripCallbacks()
        {
            for (int i = 0; i < _boundPortraitStrips.Count; i++)
            {
                SquadPortraitStrip strip = _boundPortraitStrips[i];
                if (strip == null)
                {
                    continue;
                }

                strip.OnPortraitClicked -= HandlePortraitStripClicked;
            }

            _boundPortraitStrips.Clear();
            _syncingPortraitSelection = false;
        }

        public void TryBindPortraitStrips()
        {
            Unit active = _getActiveControlledUnit != null ? _getActiveControlledUnit() : null;
            if (active == null)
            {
                return;
            }

            PruneBoundPortraitStrips();
            if (_boundPortraitStrips.Count > 0)
            {
                // Squad can grow over time (deferred spawns). Keep UI bindings fresh.
                for (int i = 0; i < _boundPortraitStrips.Count; i++)
                {
                    _boundPortraitStrips[i]?.RefreshBindings();
                }

                SyncPortraitSelection(active);
                return;
            }

            SquadPortraitStrip[] strips = UnityEngine.Object.FindObjectsByType<SquadPortraitStrip>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (strips == null || strips.Length == 0)
            {
                return;
            }

            for (int i = 0; i < strips.Length; i++)
            {
                SquadPortraitStrip strip = strips[i];
                if (strip == null)
                {
                    continue;
                }

                strip.OnPortraitClicked -= HandlePortraitStripClicked;
                strip.OnPortraitClicked += HandlePortraitStripClicked;
                _boundPortraitStrips.Add(strip);
                strip.RefreshBindings();
            }

            SyncPortraitSelection(active);
        }

        private void HandlePortraitStripClicked(Unit selectedUnit)
        {
            if (_syncingPortraitSelection)
            {
                return;
            }

            if (_isControllableSquadUnit != null && !_isControllableSquadUnit(selectedUnit))
            {
                return;
            }

            _activateControlledUnit?.Invoke(selectedUnit);
        }

        public void SyncPortraitSelection(Unit unit)
        {
            if (unit == null || _boundPortraitStrips.Count == 0)
            {
                return;
            }

            _syncingPortraitSelection = true;
            try
            {
                for (int i = 0; i < _boundPortraitStrips.Count; i++)
                {
                    SquadPortraitStrip strip = _boundPortraitStrips[i];
                    if (strip == null)
                    {
                        continue;
                    }

                    if (strip.SelectedUnit != unit)
                    {
                        strip.TrySelectUnit(unit);
                    }
                }
            }
            finally
            {
                _syncingPortraitSelection = false;
            }
        }

        private void PruneBoundPortraitStrips()
        {
            for (int i = _boundPortraitStrips.Count - 1; i >= 0; i--)
            {
                if (_boundPortraitStrips[i] != null)
                {
                    continue;
                }

                _boundPortraitStrips.RemoveAt(i);
            }
        }
    }
}

