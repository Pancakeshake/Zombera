using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public sealed class WorldSiteDiagnostic
    {
        public string Code;
        public string Message;
        public Vector2 WorldXZ;
        public bool IsError;

        public WorldSiteDiagnostic()
        {
        }

        public WorldSiteDiagnostic(string code, string message, Vector2 worldXZ, bool isError = false)
        {
            Code = code;
            Message = message;
            WorldXZ = worldXZ;
            IsError = isError;
        }
    }
}
