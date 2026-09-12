using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace Zombera.Core
{
    [Serializable]
    public sealed class GameSaveData
    {
        public SaveMetadata metadata = new();
        [FormerlySerializedAs("Player")] public PlayerSaveData player = new();
        [FormerlySerializedAs("Squad")] public List<SquadMemberSaveData> squad = new();
        [FormerlySerializedAs("Inventory")] public InventorySaveData inventory = new();
        [FormerlySerializedAs("WorldChunks")] public List<WorldChunkSaveData> worldChunks = new();
        [FormerlySerializedAs("Zombies")] public List<ZombieSaveData> zombies = new();
        [FormerlySerializedAs("Bases")] public List<BaseSaveData> bases = new();

        [FormerlySerializedAs("LootContainers")]
        public List<LootContainerSaveData> lootContainers = new();

        public List<LoosePickupSaveData> loosePickups = new();

        [FormerlySerializedAs("ProceduralWorld")]
        public ProceduralWorldSaveData proceduralWorld = new();

        public EnvironmentSaveData environment = new();

        public MapSaveData map = new();
        public CraftingSaveData crafting = new();
        public JobSystemSaveData jobs = new();
        public FactionSystemSaveData factions = new();
    }
}
