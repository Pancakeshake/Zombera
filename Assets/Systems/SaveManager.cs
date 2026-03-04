using System.Collections.Generic;
using UnityEngine;
using Zombera.AI;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Inventory;
using Zombera.World;

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
        [SerializeField] private ChunkLoader chunkLoader;

        private readonly List<Unit> unitBuffer = new List<Unit>();
        private readonly List<ZombieAI> zombieBuffer = new List<ZombieAI>();
        private readonly List<LootContainer> lootContainerBuffer = new List<LootContainer>();

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

            if (saveSystem == null)
            {
                return;
            }

            GameSaveData snapshot = BuildSaveSnapshot();
            saveSystem.SaveGameData(slotId, snapshot);
        }

        public void LoadGame(string slotId)
        {
            ActiveSlotId = slotId;

            if (saveSystem == null)
            {
                return;
            }

            if (saveSystem.TryLoadGameData(slotId, out GameSaveData saveData))
            {
                saveSystem.ApplySaveData(saveData);
            }
            else
            {
                saveSystem.LoadGame(slotId);
            }

            // TODO: Push restored payloads back to active managers.
        }

        private GameSaveData BuildSaveSnapshot()
        {
            GameSaveData saveData = new GameSaveData();

            PopulateUnitData(saveData);
            PopulateChunkData(saveData);
            PopulateZombieData(saveData);
            PopulateBaseData(saveData);
            PopulateLootContainerData(saveData);

            return saveData;
        }

        private void PopulateUnitData(GameSaveData saveData)
        {
            if (unitManager == null)
            {
                return;
            }

            List<Unit> units = unitManager.GetAllActiveUnits(unitBuffer);

            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];

                if (unit == null || unit.Health == null)
                {
                    continue;
                }

                if (unit.Role == UnitRole.Player)
                {
                    saveData.Player.Position = unit.transform.position;
                    saveData.Player.Rotation = unit.transform.rotation;
                    saveData.Player.Health = unit.Health.CurrentHealth;

                    if (unit.Inventory != null)
                    {
                        saveData.Inventory = BuildInventorySaveData(unit.Inventory);
                    }
                }
                else if (unit.Role == UnitRole.SquadMember || unit.Role == UnitRole.Survivor)
                {
                    saveData.Squad.Add(new SquadMemberSaveData
                    {
                        UnitId = unit.UnitId,
                        Position = unit.transform.position,
                        Health = unit.Health.CurrentHealth
                    });
                }
            }
        }

        private void PopulateChunkData(GameSaveData saveData)
        {
            if (chunkLoader == null)
            {
                return;
            }

            foreach (KeyValuePair<Vector2Int, WorldChunk> entry in chunkLoader.LoadedChunks)
            {
                WorldChunk chunk = entry.Value;

                if (chunk == null)
                {
                    continue;
                }

                saveData.WorldChunks.Add(new WorldChunkSaveData
                {
                    Coordinates = chunk.Coordinates,
                    Seed = chunk.Seed,
                    RegionId = chunk.RegionId
                });
            }
        }

        private void PopulateZombieData(GameSaveData saveData)
        {
            if (zombieManager == null)
            {
                return;
            }

            List<ZombieAI> zombies = zombieManager.GetActiveZombies(zombieBuffer);

            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieAI zombie = zombies[i];

                if (zombie == null)
                {
                    continue;
                }

                Unit zombieUnit = zombie.GetComponent<Unit>();
                UnitHealth zombieHealth = zombie.GetComponent<UnitHealth>();
                ZombieStateMachine stateMachine = zombie.GetComponent<ZombieStateMachine>();

                saveData.Zombies.Add(new ZombieSaveData
                {
                    ZombieId = zombieUnit != null ? zombieUnit.UnitId : zombie.GetInstanceID().ToString(),
                    Position = zombie.transform.position,
                    Health = zombieHealth != null ? zombieHealth.CurrentHealth : 0f,
                    State = stateMachine != null ? stateMachine.CurrentState.ToString() : "Unknown"
                });
            }
        }

        private void PopulateBaseData(GameSaveData saveData)
        {
            if (baseManager == null)
            {
                return;
            }

            IReadOnlyList<string> completedIds = baseManager.CompletedBuildingIds;

            for (int i = 0; i < completedIds.Count; i++)
            {
                saveData.Bases.Add(new BaseSaveData
                {
                    BaseId = completedIds[i],
                    Position = Vector3.zero,
                    BuildingState = "Completed"
                });
            }

            // TODO: Capture base transform and storage contents from placed structures.
        }

        private void PopulateLootContainerData(GameSaveData saveData)
        {
            if (lootManager == null)
            {
                return;
            }

            List<LootContainer> containers = lootManager.GetTrackedContainers(lootContainerBuffer);

            for (int i = 0; i < containers.Count; i++)
            {
                LootContainer container = containers[i];

                if (container == null)
                {
                    continue;
                }

                LootContainerSaveData containerData = new LootContainerSaveData
                {
                    ContainerId = container.ContainerId,
                    Position = container.transform.position,
                    LootGenerated = container.HasGeneratedLoot
                };

                IReadOnlyList<ItemStack> generatedLoot = container.GeneratedLoot;

                for (int lootIndex = 0; lootIndex < generatedLoot.Count; lootIndex++)
                {
                    ItemStack stack = generatedLoot[lootIndex];

                    if (stack.item == null)
                    {
                        continue;
                    }

                    containerData.ItemIds.Add(stack.item.itemId);
                    containerData.Quantities.Add(stack.quantity);
                }

                saveData.LootContainers.Add(containerData);
            }
        }

        private static InventorySaveData BuildInventorySaveData(UnitInventory inventory)
        {
            InventorySaveData inventoryData = new InventorySaveData();
            IReadOnlyList<ItemStack> stacks = inventory.Items;

            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];

                if (stack.item == null)
                {
                    continue;
                }

                inventoryData.ItemIds.Add(stack.item.itemId);
                inventoryData.Quantities.Add(stack.quantity);
            }

            inventoryData.CurrentWeight = inventory.CurrentWeight;
            return inventoryData;
        }
    }
}