using UnityEngine;
using Zombera.Core;

namespace Zombera.Systems
{
    /// <summary>
    ///     Handles transformation of legacy save data formats into current versions.
    /// </summary>
    public static class SaveMigrationService
    {
        public const int CurrentVersion = 5;

        public static string Migrate(string payload, int fromVersion)
        {
            if (fromVersion >= CurrentVersion) return payload;

            Debug.Log($"[SaveMigrationService] Migrating save data from version {fromVersion} to {CurrentVersion}.");

            var currentPayload = payload;
            
            // Sequential migration chain
            if (fromVersion < 2) currentPayload = MigrateV1ToV2(currentPayload);
            if (fromVersion < 3) currentPayload = MigrateV2ToV3(currentPayload);
            if (fromVersion < 4) currentPayload = MigrateV3ToV4(currentPayload);
            if (fromVersion < 5) currentPayload = MigrateV4ToV5(currentPayload);

            return currentPayload;
        }

        private static string MigrateV1ToV2(string payload)
        {
            // V2 introduced itemInstances in InventorySaveData
            // JsonUtility handles this by populating with defaults if missing
            return Normalize(payload);
        }

        private static string MigrateV2ToV3(string payload)
        {
            // Placeholder for V3 (e.g., metadata or equipment expansion)
            return payload;
        }

        private static string MigrateV3ToV4(string payload)
        {
            // V4 introduced placedPieces in BaseSaveData
            return Normalize(payload);
        }

        private static string MigrateV4ToV5(string payload)
        {
            // V5 introduced the optional authoritative WorldState payload envelope.
            return Normalize(payload);
        }

        private static string Normalize(string payload)
        {
            try
            {
                var data = JsonUtility.FromJson<GameSaveData>(payload) ?? new GameSaveData();
                NormalizeV5References(data);
                return JsonUtility.ToJson(data);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveMigrationService] Normalization failed: {e.Message}");
                return payload;
            }
        }

        private static void NormalizeV5References(GameSaveData data)
        {
            if (data == null) return;
            data.proceduralWorld ??= new ProceduralWorldSaveData();
            data.proceduralWorld.worldState ??= new WorldStatePayloadSaveData();
            data.proceduralWorld.chunkDeltas ??= new System.Collections.Generic.List<ChunkProceduralDeltaSaveData>();
        }
    }
}