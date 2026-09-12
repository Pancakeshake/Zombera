using UnityEngine;
using UnityEngine.EventSystems;

namespace Zombera.UI
{
    public sealed class InventoryContextActionButton : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        private bool _isDropAction;
        private InventoryPanelController _owner;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            TriggerAction();
            eventData?.Use();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            TriggerAction();
        }

        public void Configure(InventoryPanelController controller, bool dropAction)
        {
            _owner = controller;
            _isDropAction = dropAction;
        }

        private void TriggerAction()
        {
            if (_owner == null) return;

            if (_isDropAction)
                _owner.HandleContextDropClicked();
            else
                _owner.HandleContextEquipClicked();
        }
    }
}
