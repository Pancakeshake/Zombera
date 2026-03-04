using UnityEngine;
using Zombera.Core;

namespace Zombera.Systems
{
    /// <summary>
    /// Central save orchestration manager that coordinates persistence across gameplay systems.
    /// </summary>
    public sealed class SaveManager : MonoBehaviour, IGameSystem
    {
        [SerializeField] private SaveSystem saveSystem;
        [SerializeField] private UnitManager unitManager;
        [SerializeField] private ZombieManager zombieManager;
        [SerializeField] private LootManager lootManager;
        [SerializeField] private BaseManager baseManager;

        public bool IsInitialized { get; private set; }
        public string ActiveSlotId { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitialized = true;

            if (saveSystem != null && !saveSystem.IsInitialized)
            {
                saveSystem.Initialize();
            }

            // TODO: Register autosave schedule based on game state.
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
        }

        public void SaveGame(string slotId)
        {
            ActiveSlotId = slotId;

            // TODO: Pull reconstruction data from managers before saveSystem call.
            // TODO: Serialize UnitManager, ZombieManager, LootManager, BaseManager state payloads.
            _ = unitManager;
            _ = zombieManager;
            _ = lootManager;
            _ = baseManager;

            saveSystem?.SaveGame(slotId);
        }

        public void LoadGame(string slotId)
        {
            ActiveSlotId = slotId;
            saveSystem?.LoadGame(slotId);

            // TODO: Push restored payloads back to active managers.
        }
    }
}