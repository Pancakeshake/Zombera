using System;
using UnityEngine;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.CityPipeline.WorldBuilder.Tiles
{
    /// <summary>
    ///     Helpers so City/World code can resolve and subscribe to tile streams
    ///     without naming Legacy concrete bridge types or MapMagic types.
    /// </summary>
    public static class WorldTileStreamSourceUtility
    {
        public static WorldTileStreamSource FindBridge()
        {
            return UnityEngine.Object.FindFirstObjectByType<WorldTileStreamSource>(FindObjectsInactive.Include);
        }

        public static IWorldTileGameplayEvents AsGameplayEvents(WorldTileStreamSource source)
        {
            return source as IWorldTileGameplayEvents;
        }

        public static void SubscribeTileApplied(
            WorldTileStreamSource source,
            Action<WorldTileInfo> handler)
        {
            var events = AsGameplayEvents(source);
            if (events != null && handler != null)
                events.TileAppliedForGameplay += handler;
        }

        public static void UnsubscribeTileApplied(
            WorldTileStreamSource source,
            Action<WorldTileInfo> handler)
        {
            var events = AsGameplayEvents(source);
            if (events != null && handler != null)
                events.TileAppliedForGameplay -= handler;
        }
    }
}
