using System.Collections.Generic;
using UnityEngine;
using Zombera.AI;
using Zombera.BaseBuilding;
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
        [SerializeField] private WorldManager worldManager;
        [SerializeField, Min(30f)] private float autosaveIntervalSeconds = 300f;

        private readonly List<Unit> unitBuffer = new List<Unit>();
        private readonly List<ZombieAI> zombieBuffer = new List<ZombieAI>();
        private readonly List<LootContainer> lootContainerBuffer = new List<LootContainer>();
        private float _autosaveTimer;

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

            _autosaveTimer = autosaveIntervalSeconds;
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
        }

        private void Update()
        {
            if (!IsInitialized || string.IsNullOrWhiteSpace(ActiveSlotId))
            {
                return;
            }

            _autosaveTimer -= Time.deltaTime;

            if (_autosaveTimer <= 0f)
            {
                _autosaveTimer = autosaveIntervalSeconds;
                SaveGame(ActiveSlotId);
            }
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
                RestoreRuntimeState(saveData);
            }
            else
            {
                saveSystem.LoadGame(slotId);

                if (saveSystem.TryLoadGameData(slotId, out GameSaveData loadedFromDisk))
                {
                    RestoreRuntimeState(loadedFromDisk);
                }
            }
        }

        private GameSaveData BuildSaveSnapshot()
        {
            GameSaveData saveData = new GameSaveData();

            PopulateUnitData(saveData);
            PopulateChunkData(saveData);
            PopulateZombieData(saveData);
            PopulateBaseData(saveData);
            PopulateLootContainerData(saveData);
            PopulateProceduralWorldData(saveData);

            return saveData;
        }

        private void PopulateProceduralWorldData(GameSaveData saveData)
        {
            if (saveData.ProceduralWorld == null)
            {
                saveData.ProceduralWorld = new ProceduralWorldSaveData();
            }

            if (worldManager == null)
            {
                worldManager = FindFirstObjectByType<WorldManager>();
            }

            if (worldManager == null || !worldManager.UseProceduralStreamingWorld || !ProceduralWorldSession.IsActive)
            {
                saveData.ProceduralWorld.hasData = false;
                return;
            }

            if (chunkLoader != null)
            {
                foreach (KeyValuePair<Vector2Int, WorldChunk> entry in chunkLoader.LoadedChunks)
                {
                    WorldChunk chunk = entry.Value;

                    if (chunk != null && chunk.IsDirty)
                    {
                        StreamedWorldChunkState.CaptureChunkState(chunk, "autosave_loaded");
                    }
                }
            }

            saveData.ProceduralWorld.hasData = true;
            saveData.ProceduralWorld.worldSeed = ProceduralWorldSession.WorldSeed;
            saveData.ProceduralWorld.graphVersion = ProceduralWorldSession.GraphVersion ?? string.Empty;
            saveData.ProceduralWorld.formatVersion = 1;
            StreamedWorldChunkState.MergeIntoSave(saveData);
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

                    if (unit.Stats != null)
                    {
                        saveData.Player.Stats = BuildUnitStatsSaveData(unit.Stats);
                    }

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
                        Health = unit.Health.CurrentHealth,
                        Stats = BuildUnitStatsSaveData(unit.Stats)
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

        private void RestoreRuntimeState(GameSaveData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            RestoreProceduralWorldData(saveData);
            RestorePlayerState(saveData);
            RestoreSquadState(saveData);
        }

        private void RestoreProceduralWorldData(GameSaveData saveData)
        {
            if (saveData.ProceduralWorld == null || !saveData.ProceduralWorld.hasData)
            {
                return;
            }

            if (worldManager == null)
            {
                worldManager = FindFirstObjectByType<WorldManager>();
            }

            worldManager?.ApplyLoadedProceduralWorld(saveData.ProceduralWorld);
        }

        private void RestorePlayerState(GameSaveData saveData)
        {
            if (unitManager == null)
            {
                return;
            }

            Unit playerUnit = unitManager.FindFirstUnitByRole(UnitRole.Player);

            if (playerUnit == null)
            {
                return;
            }

            playerUnit.transform.SetPositionAndRotation(saveData.Player.Position, saveData.Player.Rotation);

            if (playerUnit.Stats != null)
            {
                ApplyUnitStatsSaveData(playerUnit.Stats, saveData.Player.Stats);
            }

            if (playerUnit.Health != null)
            {
                playerUnit.Health.SetHealth(saveData.Player.Health);
            }
        }

        private void RestoreSquadState(GameSaveData saveData)
        {
            if (unitManager == null || saveData.Squad == null)
            {
                return;
            }

            List<Unit> allUnits = unitManager.GetAllActiveUnits(unitBuffer);

            for (int s = 0; s < saveData.Squad.Count; s++)
            {
                SquadMemberSaveData memberData = saveData.Squad[s];

                for (int u = 0; u < allUnits.Count; u++)
                {
                    Unit unit = allUnits[u];

                    if (unit != null && unit.UnitId == memberData.UnitId)
                    {
                        unit.transform.position = memberData.Position;

                        if (unit.Stats != null)
                        {
                            ApplyUnitStatsSaveData(unit.Stats, memberData.Stats);
                        }

                        if (unit.Health != null)
                        {
                            unit.Health.SetHealth(memberData.Health);
                        }

                        break;
                    }
                }
            }
        }

        private void PopulateBaseData(GameSaveData saveData)
        {
            if (baseManager == null)
            {
                return;
            }

            IReadOnlyList<string> completedIds = baseManager.CompletedBuildingIds;
            BaseStorage storage = baseManager.GetBaseStorage();

            for (int i = 0; i < completedIds.Count; i++)
            {
                BaseSaveData entry = new BaseSaveData
                {
                    BaseId = completedIds[i],
                    Position = Vector3.zero,
                    BuildingState = "Completed"
                };

                if (storage != null)
                {
                    IReadOnlyList<MaterialStack> stacks = storage.MaterialStacks;

                    for (int j = 0; j < stacks.Count; j++)
                    {
                        MaterialStack stack = stacks[j];

                        if (stack.item == null)
                        {
                            continue;
                        }

                        entry.StorageItemIds.Add(stack.item.itemId);
                        entry.StorageQuantities.Add(stack.amount);
                    }
                }

                saveData.Bases.Add(entry);
            }
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

        private static UnitStatsSaveData BuildUnitStatsSaveData(UnitStats stats)
        {
            UnitStatsSaveData data = new UnitStatsSaveData();

            if (stats == null)
            {
                return data;
            }

            data.HasSkillProgressionData = true;
            data.Stamina = stats.Stamina;

            data.Strength = stats.Strength;
            data.StrengthXp = stats.GetCurrentExperience(UnitSkillType.Strength);
            data.Shooting = stats.Shooting;
            data.ShootingXp = stats.GetCurrentExperience(UnitSkillType.Shooting);
            data.Melee = stats.Melee;
            data.MeleeXp = stats.GetCurrentExperience(UnitSkillType.Melee);
            data.Medical = stats.Medical;
            data.MedicalXp = stats.GetCurrentExperience(UnitSkillType.Medical);
            data.Engineering = stats.Engineering;
            data.EngineeringXp = stats.GetCurrentExperience(UnitSkillType.Engineering);
            data.Toughness = stats.Toughness;
            data.ToughnessXp = stats.GetCurrentExperience(UnitSkillType.Toughness);
            data.Constitution = stats.Constitution;
            data.ConstitutionXp = stats.GetCurrentExperience(UnitSkillType.Constitution);
            data.Agility = stats.Agility;
            data.AgilityXp = stats.GetCurrentExperience(UnitSkillType.Agility);
            data.Endurance = stats.Endurance;
            data.EnduranceXp = stats.GetCurrentExperience(UnitSkillType.Endurance);
            data.Scavenging = stats.Scavenging;
            data.ScavengingXp = stats.GetCurrentExperience(UnitSkillType.Scavenging);
            data.Stealth = stats.Stealth;
            data.StealthXp = stats.GetCurrentExperience(UnitSkillType.Stealth);

            return data;
        }

        private static void ApplyUnitStatsSaveData(UnitStats stats, UnitStatsSaveData data)
        {
            if (stats == null || data == null || !data.HasSkillProgressionData)
            {
                return;
            }

            stats.SetSkillProgress(UnitSkillType.Strength, data.Strength, data.StrengthXp);
            stats.SetSkillProgress(UnitSkillType.Shooting, data.Shooting, data.ShootingXp);
            stats.SetSkillProgress(UnitSkillType.Melee, data.Melee, data.MeleeXp);
            stats.SetSkillProgress(UnitSkillType.Medical, data.Medical, data.MedicalXp);
            stats.SetSkillProgress(UnitSkillType.Engineering, data.Engineering, data.EngineeringXp);
            stats.SetSkillProgress(UnitSkillType.Toughness, data.Toughness, data.ToughnessXp);
            stats.SetSkillProgress(UnitSkillType.Constitution, data.Constitution, data.ConstitutionXp);
            stats.SetSkillProgress(UnitSkillType.Agility, data.Agility, data.AgilityXp);
            stats.SetSkillProgress(UnitSkillType.Endurance, data.Endurance, data.EnduranceXp);
            stats.SetSkillProgress(UnitSkillType.Scavenging, data.Scavenging, data.ScavengingXp);
            stats.SetSkillProgress(UnitSkillType.Stealth, data.Stealth, data.StealthXp);

            if (data.Stamina > 0f)
            {
                stats.SetStamina(data.Stamina);
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