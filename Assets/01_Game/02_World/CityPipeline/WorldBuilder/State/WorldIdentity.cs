using System;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.State
{
    [Serializable]
    public struct WorldEntityId : IEquatable<WorldEntityId>, IComparable<WorldEntityId>, IComparable
    {
        public WorldEntityKind kind;
        public string value;

        public WorldEntityId(WorldEntityKind kind, string value)
        {
            this.kind = kind;
            this.value = value ?? string.Empty;
        }

        public int CompareTo(WorldEntityId other)
        {
            var kindComparison = kind.CompareTo(other.kind);
            if (kindComparison != 0)
                return kindComparison;

            return string.CompareOrdinal(value ?? string.Empty, other.value ?? string.Empty);
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;
            if (obj is WorldEntityId other)
                return CompareTo(other);

            throw new ArgumentException($"Object must be of type {nameof(WorldEntityId)}.", nameof(obj));
        }

        public bool Equals(WorldEntityId other)
        {
            return kind == other.kind &&
                string.Equals(value ?? string.Empty, other.value ?? string.Empty, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is WorldEntityId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (int)kind;
                hash = hash * 31 + StableOrdinalHash(value);
                return hash;
            }
        }

        public override string ToString()
        {
            var idValue = value ?? string.Empty;
            return kind == WorldEntityKind.None && idValue.Length == 0
                ? WorldEntityKind.None.ToString()
                : $"{kind}:{idValue}";
        }

        public static bool operator ==(WorldEntityId left, WorldEntityId right) => left.Equals(right);
        public static bool operator !=(WorldEntityId left, WorldEntityId right) => !left.Equals(right);
        public static bool operator <(WorldEntityId left, WorldEntityId right) => left.CompareTo(right) < 0;
        public static bool operator >(WorldEntityId left, WorldEntityId right) => left.CompareTo(right) > 0;
        public static bool operator <=(WorldEntityId left, WorldEntityId right) => left.CompareTo(right) <= 0;
        public static bool operator >=(WorldEntityId left, WorldEntityId right) => left.CompareTo(right) >= 0;

        private static int StableOrdinalHash(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            unchecked
            {
                var hash = unchecked((int)2166136261);
                for (var i = 0; i < text.Length; i++)
                    hash = (hash ^ text[i]) * 16777619;
                return hash;
            }
        }
    }

    [Serializable]
    public struct WorldTileKey : IEquatable<WorldTileKey>, IComparable<WorldTileKey>, IComparable
    {
        public int x;
        public int z;

        public WorldTileKey(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public static WorldTileKey FromCoord(WorldTileCoord coord) => new(coord.X, coord.Z);

        public WorldTileCoord ToCoord() => new(x, z);

        public int CompareTo(WorldTileKey other)
        {
            var zComparison = z.CompareTo(other.z);
            return zComparison != 0 ? zComparison : x.CompareTo(other.x);
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;
            if (obj is WorldTileKey other)
                return CompareTo(other);

            throw new ArgumentException($"Object must be of type {nameof(WorldTileKey)}.", nameof(obj));
        }

        public bool Equals(WorldTileKey other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is WorldTileKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ z;
            }
        }

        public override string ToString() => $"({x},{z})";

        public static bool operator ==(WorldTileKey left, WorldTileKey right) => left.Equals(right);
        public static bool operator !=(WorldTileKey left, WorldTileKey right) => !left.Equals(right);
        public static bool operator <(WorldTileKey left, WorldTileKey right) => left.CompareTo(right) < 0;
        public static bool operator >(WorldTileKey left, WorldTileKey right) => left.CompareTo(right) > 0;
        public static bool operator <=(WorldTileKey left, WorldTileKey right) => left.CompareTo(right) <= 0;
        public static bool operator >=(WorldTileKey left, WorldTileKey right) => left.CompareTo(right) >= 0;
    }
}
