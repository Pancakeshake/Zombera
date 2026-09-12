using System.Reflection;
using DoorScript;
using UnityEngine;

namespace Zombera.BuildingSystem
{
    /// <summary>
    ///     Typed bridge for DoorScript.Door (Free Wood Door Pack) support.
    ///     Lives in its own adapter assembly so game code never references the
    ///     third-party type directly; callers keep working with opaque Components.
    /// </summary>
    public static class ThirdPartyDoorBridge
    {
        // DoorOpenAngle is a private field on DoorScript.Door, so this single member
        // still needs reflection; everything else is compile-checked.
        private static readonly FieldInfo DoorOpenAngleField = typeof(Door).GetField(
            "DoorOpenAngle", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool IsThirdPartyDoor(Component component)
        {
            return component is Door;
        }

        public static bool TryGetOnTransform(Transform t, out Component door)
        {
            door = null;
            if (t == null) return false;

            door = t.GetComponent<Door>();
            return door != null;
        }

        public static bool TryGetInChildren(Transform t, out Component door)
        {
            door = null;
            if (t == null) return false;

            door = t.GetComponentInChildren<Door>(true);
            return door != null;
        }

        public static bool TryGetIsOpen(Component door, out bool isOpen)
        {
            if (door is Door typedDoor)
            {
                isOpen = typedDoor.open;
                return true;
            }

            isOpen = false;
            return false;
        }

        public static bool TrySetOpenAngle(Component door, float angle)
        {
            if (door is not Door typedDoor || DoorOpenAngleField == null) return false;

            DoorOpenAngleField.SetValue(typedDoor, angle);
            return true;
        }

        public static bool TryOpenDoor(Component door)
        {
            if (door is not Door typedDoor) return false;

            typedDoor.OpenDoor();
            return true;
        }
    }
}
