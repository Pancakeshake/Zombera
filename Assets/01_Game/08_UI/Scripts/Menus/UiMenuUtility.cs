using UnityEngine;
using UnityEngine.EventSystems;

namespace Zombera.UI.Menus
{
    public static class UiMenuUtility
    {
        public static void DeselectCurrent()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}