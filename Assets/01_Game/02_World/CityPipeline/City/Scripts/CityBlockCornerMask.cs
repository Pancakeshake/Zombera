using System;

namespace Zombera.World.City
{
    [Flags]
    public enum CityBlockCornerMask
    {
        None = 0,
        BottomLeft = 1 << 0,
        BottomRight = 1 << 1,
        TopRight = 1 << 2,
        TopLeft = 1 << 3
    }
}
