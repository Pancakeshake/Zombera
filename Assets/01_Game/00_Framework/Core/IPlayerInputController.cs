namespace Zombera.Core
{
    /// <summary>
    ///     Cross-assembly marker for the player input controller component so AI brains can
    ///     toggle player control without referencing the World assembly's concrete type.
    /// </summary>
    public interface IPlayerInputController
    {
        /// <summary>Mirrors Behaviour.enabled on the underlying component.</summary>
        bool InputEnabled { get; set; }
    }
}
