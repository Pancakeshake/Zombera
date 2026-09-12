using Zombera.Core;

namespace Zombera.Systems
{
    /// <summary>
    ///     Modular component that contributes to or restores from the GameSaveData.
    /// </summary>
    public interface ISaveProvider
    {
        /// <summary>
        ///     Higher priority providers are executed first during restoration.
        /// </summary>
        int Priority { get; }

        /// <summary>
        ///     Called when building a save snapshot.
        /// </summary>
        void OnSave(GameSaveData saveData);

        /// <summary>
        ///     Called when restoring from a save file.
        /// </summary>
        void OnLoad(GameSaveData saveData);
    }
}