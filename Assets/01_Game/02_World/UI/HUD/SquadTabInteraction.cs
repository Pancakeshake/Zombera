#region

using UnityEngine;
using UnityEngine.EventSystems;

#endregion

namespace Zombera.UI
{
    [DisallowMultipleComponent]
    internal sealed class SquadTabInteraction : MonoBehaviour, IPointerClickHandler
    {
        private const float DoubleClickThresholdSeconds = 0.32f;

        private SquadPortraitStrip _strip;
        private int _tabIndex;
        private float _lastClickAt = -999f;

        public void Configure(SquadPortraitStrip strip, int tabIndex)
        {
            _strip = strip;
            _tabIndex = tabIndex;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_strip == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                _strip.ShowSquadTabContextMenu(_tabIndex, eventData);
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left) return;

            _strip.HideSquadTabContextMenu();

            var now = Time.unscaledTime;
            if (now - _lastClickAt <= DoubleClickThresholdSeconds)
            {
                _lastClickAt = -999f;
                _strip.BeginRenameSquadTab(_tabIndex);
                return;
            }

            _lastClickAt = now;
            _strip.SetActiveSquadTab(_tabIndex);
        }
    }
}
