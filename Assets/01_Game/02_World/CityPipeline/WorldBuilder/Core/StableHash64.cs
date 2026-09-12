using System;
using System.Text;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>FNV-1a 64-bit hasher for deterministic fingerprints.</summary>
    public struct StableHash64
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        private ulong _hash;

        public StableHash64(ulong seed = OffsetBasis)
        {
            _hash = seed;
        }

        public void Append(string value)
        {
            if (value == null)
            {
                Append(0);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(value);
            for (var i = 0; i < bytes.Length; i++)
                Mix(bytes[i]);
        }

        public void Append(int value)
        {
            unchecked
            {
                Mix((byte)value);
                Mix((byte)(value >> 8));
                Mix((byte)(value >> 16));
                Mix((byte)(value >> 24));
            }
        }

        public void Append(float value)
        {
            Append(BitConverter.SingleToInt32Bits(value));
        }

        public void Append(ulong value)
        {
            unchecked
            {
                Mix((byte)value);
                Mix((byte)(value >> 8));
                Mix((byte)(value >> 16));
                Mix((byte)(value >> 24));
                Mix((byte)(value >> 32));
                Mix((byte)(value >> 40));
                Mix((byte)(value >> 48));
                Mix((byte)(value >> 56));
            }
        }

        public ulong Finalize() => _hash;

        private void Mix(byte b)
        {
            unchecked
            {
                _hash ^= b;
                _hash *= Prime;
            }
        }
    }
}
