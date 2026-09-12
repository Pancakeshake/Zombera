using System;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>Integer tile coordinate in the world grid.</summary>
    public readonly struct WorldTileCoord : IEquatable<WorldTileCoord>
    {
        public int X { get; }
        public int Z { get; }

        public WorldTileCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(WorldTileCoord other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldTileCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z);
        public override string ToString() => $"({X},{Z})";

        public static bool operator ==(WorldTileCoord left, WorldTileCoord right) => left.Equals(right);
        public static bool operator !=(WorldTileCoord left, WorldTileCoord right) => !left.Equals(right);
    }
}
