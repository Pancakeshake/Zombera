namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Seedable deterministic RNG (xorshift64*).</summary>
    public sealed class DeterministicRng
    {
        private ulong _state;

        public ulong State => _state;
        public ulong Stream { get; private set; }

        public DeterministicRng(ulong seed)
        {
            _state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;
            Stream = 0;
        }

        public DeterministicRng(int seed)
            : this(unchecked((ulong)(uint)seed) ^ 0xA5A5A5A5A5A5A5A5UL)
        {
        }

        public static DeterministicRng FromState(ulong state, ulong stream)
        {
            var rng = new DeterministicRng(state == 0UL ? 0x9E3779B97F4A7C15UL : state)
            {
                Stream = stream
            };
            return rng;
        }

        public int NextInt()
        {
            var value = NextULong();
            return unchecked((int)(value >> 32));
        }

        public float NextFloat01()
        {
            // 24 bits of mantissa for uniform [0,1)
            var bits = (NextULong() >> 40) & 0xFFFFFFUL;
            return bits / 16777216f;
        }

        public DeterministicRng CreateStream(int streamId)
        {
            var hasher = new StableHash64(_state);
            hasher.Append(streamId);
            var child = new DeterministicRng(hasher.Finalize())
            {
                Stream = unchecked((ulong)(uint)streamId)
            };
            return child;
        }

        private ulong NextULong()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 7;
            x ^= x << 17;
            _state = x;
            return x;
        }
    }
}
