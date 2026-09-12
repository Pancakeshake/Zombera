using System;

namespace Zombera.Core
{
    public interface ISaveManagerGateway
    {
        string ActiveSlotId { get; }
        bool IsInitialized { get; }
        void Initialize();
        void SetActiveSlot(string slotId);
        bool SaveGame(string slotId);
    }

    public static class SaveManagerGateway
    {
        public static ISaveManagerGateway ResolveActive() => Resolver?.Invoke();

        /// <summary>Set only by the owning SaveManager (World assembly) during its lifecycle.</summary>
        public static Func<ISaveManagerGateway> Resolver { get; set; }
    }
}
