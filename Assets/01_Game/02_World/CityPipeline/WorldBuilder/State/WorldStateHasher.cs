using System;
using System.IO;
using System.Security.Cryptography;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    public static class WorldStateHasher
    {
        private static readonly char[] Hex = "0123456789abcdef".ToCharArray();

        public static string ComputeHash(WorldState state)
        {
            var bytes = ComputeHashBytes(state);
            return ToLowerHex(bytes);
        }

        public static byte[] ComputeHashBytes(WorldState state)
        {
            var canonical = WorldStateCanonicalizer.CanonicalizeCopy(state, out var report);
            if (!report.IsValid)
                throw new InvalidOperationException("Cannot hash invalid WorldState: " + string.Join("; ", report.Errors));

            using var stream = new MemoryStream();
            var writer = new WorldStateCanonicalHashWriter(stream);
            writer.Write(canonical);
            using var sha = SHA256.Create();
            return sha.ComputeHash(stream.ToArray());
        }

        private static string ToLowerHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var chars = new char[bytes.Length * 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                var value = bytes[i];
                chars[i * 2] = Hex[value >> 4];
                chars[i * 2 + 1] = Hex[value & 0x0F];
            }

            return new string(chars);
        }
    }
}
