using System;
using System.IO;
using System.Text;
using UnityEngine;
using Zombera.Systems;

namespace Zombera.Core
{
    internal static class SaveFileCodec
    {
        public static byte[] Serialize(GameSaveData saveData)
        {
            var envelope = new SaveEnvelope
            {
                saveVersion = SaveMigrationService.CurrentVersion,
                gameVersion = Application.version,
                timestamp = DateTime.UtcNow.ToString("o"),
                payload = JsonUtility.ToJson(saveData, false)
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope, false));
        }

        public static GameSaveData Deserialize(string json)
        {
            try
            {
                var envelope = JsonUtility.FromJson<SaveEnvelope>(json);
                if (envelope != null && !string.IsNullOrEmpty(envelope.payload))
                {
                    var migratedPayload = SaveMigrationService.Migrate(envelope.payload, envelope.saveVersion);
                    return JsonUtility.FromJson<GameSaveData>(migratedPayload);
                }

                return JsonUtility.FromJson<GameSaveData>(json);
            }
            catch
            {
                return JsonUtility.FromJson<GameSaveData>(json);
            }
        }

        public static bool TryWriteAtomic(string path, byte[] bytes)
        {
            var tmpPath = path + ".tmp";
            var bakPath = path + ".bak";

            File.WriteAllBytes(tmpPath, bytes);
            if (File.Exists(path))
            {
                if (File.Exists(bakPath)) File.Delete(bakPath);
                File.Move(path, bakPath);
            }

            File.Move(tmpPath, path);
            return true;
        }
    }
}
