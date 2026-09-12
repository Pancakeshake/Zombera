#region

using UnityEngine;
using UnityEngine.EventSystems;

#endregion

namespace Zombera.UI
{
    [DisallowMultipleComponent]
    internal sealed class SquadTabDropInteraction : MonoBehaviour, IDropHandler
    {
        private SquadPortraitStrip _strip;
        private int _tabIndex;

        public void Configure(SquadPortraitStrip strip, int tabIndex)
        {
            _strip = strip;
            _tabIndex = tabIndex;
        }

        public void OnDrop(PointerEventData eventData)
        {
            _ = eventData;
            _strip?.HandlePortraitDropOnSquadTab(_tabIndex);
        }
    }
}
