using JBooth.MicroSplat;
using UnityEngine;

namespace Zombera.World.City
{
    /// <summary>
    ///     Alphamap / TextureArrayConfig layer index resolution and MicroSplat
    ///     terrain-layer bootstrap for <see cref="CityLotTerrainPainter"/>.
    /// </summary>
    public static partial class CityLotTerrainPainter
    {
        /// <summary>
        ///     Resolves the alphamap layer index for a surface type by searching
        ///     the TextureArrayConfig's terrain layers directly — no fragile
        ///     name matching against the terrain's current layer set.
        ///     Falls back to the legacy name-based search when no config is provided.
        /// </summary>
        public static int ResolveLayerFromConfig(TextureArrayConfig cfg, LotSurfaceType surfaceType)
        {
            if (cfg?.sourceTextures == null || cfg.sourceTextures.Count == 0)
                return ResolveSurfaceLayerFallback(surfaceType);

            var terms = GetSearchTerms(surfaceType);
            for (var i = 0; i < cfg.sourceTextures.Count; i++)
            {
                var entry = cfg.sourceTextures[i];
                var layerName = entry?.terrainLayer?.name ?? entry?.diffuse?.name;
                if (string.IsNullOrEmpty(layerName)) continue;

                for (var s = 0; s < terms.Length; s++)
                {
                    if (layerName.IndexOf(terms[s], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return i;
                }
            }

            return ResolveSurfaceLayerFallback(surfaceType);
        }

        private static int ResolveSurfaceLayerFallback(LotSurfaceType surfaceType)
        {
            var terrains = Terrain.activeTerrains;
            for (var t = 0; t < terrains.Length; t++)
            {
                var layer = ResolveSurfaceLayerIndex(terrains[t]?.terrainData, surfaceType);
                if (layer >= 0) return layer;
            }
            return -1;
        }

        private static int ResolveSurfaceLayer(LotSurfaceType surfaceType)
        {
            return ResolveSurfaceLayerFallback(surfaceType);
        }

        private static int ResolveSurfaceLayerIndex(TerrainData td, LotSurfaceType surfaceType)
        {
            if (td == null) return -1;
            var layers = td.terrainLayers;
            if (layers == null || layers.Length == 0) return -1;

            var terms = GetSearchTerms(surfaceType);
            var exact = FindLayerIndex(layers, terms, MatchesExact);
            return exact >= 0 ? exact : FindLayerIndex(layers, terms, MatchesPartial);
        }

        private static bool MatchesExact(TerrainLayer layer, string term)
        {
            return string.Equals(layer.name, term, System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesPartial(TerrainLayer layer, string term)
        {
            return layer.name.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int FindLayerIndex(
            TerrainLayer[] layers, string[] terms, System.Func<TerrainLayer, string, bool> matches)
        {
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null) continue;
                if (MatchesAnyTerm(layer, terms, matches))
                    return i;
            }
            return -1;
        }

        private static bool MatchesAnyTerm(
            TerrainLayer layer, string[] terms, System.Func<TerrainLayer, string, bool> matches)
        {
            for (var s = 0; s < terms.Length; s++)
            {
                if (matches(layer, terms[s]))
                    return true;
            }
            return false;
        }

        private static int ResolveConcreteLayerIndex(TerrainData td) =>
            ResolveSurfaceLayerIndex(td, LotSurfaceType.Concrete);

        private static string[] GetSearchTerms(LotSurfaceType surfaceType) => surfaceType switch
        {
            LotSurfaceType.Grass    => new[] { "Grass", "Ground", "ShortGrass", "Grass_01", "Meadow" },
            LotSurfaceType.Concrete => new[] { "Concrete", "ConcreteSlab", "RoughConcrete", "Concrete_01", "Slab" },
            LotSurfaceType.Asphalt  => new[] { "Asphalt", "Road", "Tarmac", "Asphalt_01" },
            LotSurfaceType.Gravel   => new[] { "Gravel", "Dirt", "Aggregate", "Gravel_01" },
            LotSurfaceType.Paver    => new[] { "Paver", "Brick", "Cobble", "Cobblestone", "Stone" },
            LotSurfaceType.Garden   => new[] { "Garden", "Mulch", "FlowerBed", "Soil", "Dirt" },
            _ => new[] { "Grass" }
        };

        /// <summary>
        ///     Creates terrain layers from the MicroSplat TextureArrayConfig when
        ///     MapMagic's Textures mode leaves terrainLayers empty.
        /// </summary>
        private static void EnsureTerrainLayers(Terrain terrain, TextureArrayConfig cfg)
        {
            if (cfg == null || terrain?.terrainData == null) return;

            var count = Mathf.Min(cfg.sourceTextures.Count, 32);
            if (count <= 0) return;

            // Already populated — only refresh if any layer is null.
            if (HasCompleteLayers(terrain.terrainData.terrainLayers, count))
                return;

            terrain.terrainData.terrainLayers = BuildLayers(cfg, count);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(terrain.terrainData);
#endif
        }

        private static bool HasCompleteLayers(TerrainLayer[] current, int count)
        {
            if (current == null || current.Length < count) return false;
            for (var i = 0; i < count; i++)
            {
                if (current[i] == null)
                    return false;
            }
            return true;
        }

        private static TerrainLayer[] BuildLayers(TextureArrayConfig cfg, int count)
        {
            var layers = new TerrainLayer[count];
            for (var i = 0; i < count; i++)
                layers[i] = cfg.sourceTextures[i].terrainLayer ?? CreateLayer(cfg, i);
            return layers;
        }

        private static TerrainLayer CreateLayer(TextureArrayConfig cfg, int i)
        {
            var src = cfg.sourceTextures[i];
            var tl = new TerrainLayer { name = src.diffuse?.name ?? $"Layer_{i}" };
            tl.diffuseTexture = src.diffuse;
            src.terrainLayer = tl;
            return tl;
        }
    }
}
