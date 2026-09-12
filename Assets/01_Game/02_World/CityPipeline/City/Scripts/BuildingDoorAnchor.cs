namespace Zombera.World.City
{
    /// <summary>
    ///     Stamped on generated building prefab roots by the modular generator.
    ///     Authoritatively records the main (front) door's outward yaw offset so
    ///     placement faces the correct wall toward the road instead of guessing
    ///     which StairSocket is the front door.
    /// </summary>
    public sealed class BuildingDoorAnchor : UnityEngine.MonoBehaviour
    {
        public bool hasDoor;
        public float doorYawOffset;
    }
}
