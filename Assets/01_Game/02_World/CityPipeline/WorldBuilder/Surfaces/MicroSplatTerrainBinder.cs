using JBooth.MicroSplat;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    /// <summary>Binds MicroSplat template material, prop data and terrain layers onto a terrain.</summary>
    public static class MicroSplatTerrainBinder
    {
#if UNITY_EDITOR
        private const string DefaultTextureArrayConfigPath =
            "Assets/01_Game/02_World/Terrain/Materials/Microsplat/Microsplat_World.asset";

        private const string DefaultTemplateMaterialPath =
            "Assets/01_Game/02_World/Terrain/Materials/Microsplat/MicroSplat.mat";

        private const string DefaultKeywordsAssetPath =
            "Assets/01_Game/02_World/Terrain/Materials/Microsplat/MicroSplat_keywords.asset";

        private const string DefaultPropDataAssetPath =
            "Assets/01_Game/02_World/Terrain/Materials/Microsplat/MicroSplat_propdata.asset";

        private static Material _cachedDefaultTemplate;
        private static MicroSplatKeywords _cachedDefaultKeywords;
        private static MicroSplatPropData _cachedDefaultPropData;
#endif

        /// <summary>
        ///     Binds the canonical <c>MicroSplat.mat</c> template when no palette is available
        ///     (city lot terrain paint, hub Reset, etc.).
        /// </summary>
        public static void BindDefault(Terrain terrain) => Bind(terrain, null);

        public static void Bind(Terrain terrain, WorldSurfacePalette palette) =>
            Bind(terrain, palette, syncMicroSplat: true);

        /// <summary>
        ///     Binds template/prop/keywords and terrain layers. When <paramref name="syncMicroSplat"/>
        ///     is false, skips full MicroSplatTerrain.Sync (use for warm rebinds; flush controls later).
        /// </summary>
        public static void Bind(Terrain terrain, WorldSurfacePalette palette, bool syncMicroSplat)
        {
            if (terrain == null) return;

            var micro = terrain.GetComponent<MicroSplatTerrain>();
            if (micro == null)
                micro = terrain.gameObject.AddComponent<MicroSplatTerrain>();

            var template = ResolveTemplateMaterial(palette);
            if (template != null)
                micro.templateMaterial = template;

            var propData = ResolvePropData(palette);
            if (propData != null)
                micro.propData = propData;

            var keywords = ResolveKeywords(palette);
            if (keywords != null)
                micro.keywordSO = keywords;

            if (syncMicroSplat || terrain.materialTemplate == null)
                micro.Sync();

            // Textures / Sync can leave terrainLayers empty — restore before alphamap paint.
            EnsureTerrainLayers(terrain, palette);
        }

        /// <summary>Flushes painted alphamaps into MicroSplat control textures for one terrain.</summary>
        public static void SyncControls(Terrain terrain)
        {
            if (terrain == null) return;
            var micro = terrain.GetComponent<MicroSplatTerrain>();
            micro?.Sync();
        }

        /// <summary>
        ///     Lightweight control-texture push for already-bound terrains.
        ///     Avoids MicroSplatTerrain.Sync editor AssetDatabase/material rebuild work.
        /// </summary>
        public static void SyncControlTexturesOnly(Terrain terrain)
        {
            if (terrain?.terrainData == null)
                return;

            var mat = terrain.materialTemplate;
            if (mat == null)
            {
                SyncControls(terrain);
                return;
            }

            var controls = terrain.terrainData.alphamapTextures;
            if (controls == null || controls.Length == 0)
                return;

            ApplyControlTextures(mat, controls);
        }

        private static void ApplyControlTextures(Material mat, Texture2D[] controls)
        {
            mat.SetTexture("_Control0", controls[0]);
            mat.SetTexture("_Control1", controls.Length > 1 ? controls[1] : Texture2D.blackTexture);
            mat.SetTexture("_Control2", controls.Length > 2 ? controls[2] : Texture2D.blackTexture);
            mat.SetTexture("_Control3", controls.Length > 3 ? controls[3] : Texture2D.blackTexture);
            mat.SetTexture("_Control4", controls.Length > 4 ? controls[4] : Texture2D.blackTexture);
            mat.SetTexture("_Control5", controls.Length > 5 ? controls[5] : Texture2D.blackTexture);
            mat.SetTexture("_Control6", controls.Length > 6 ? controls[6] : Texture2D.blackTexture);
            mat.SetTexture("_Control7", controls.Length > 7 ? controls[7] : Texture2D.blackTexture);
        }

        /// <summary>True when MicroSplat material, component, and splat layers are present.</summary>
        public static bool IsBound(Terrain terrain, WorldSurfacePalette palette)
        {
            if (terrain?.terrainData == null)
                return false;

            var micro = terrain.GetComponent<MicroSplatTerrain>();
            if (micro == null || micro.templateMaterial == null || terrain.materialTemplate == null)
                return false;

            var required = ResolveRequiredLayerCount(palette);
            return HasCompleteLayers(terrain.terrainData.terrainLayers, required);
        }

        private static Material ResolveTemplateMaterial(WorldSurfacePalette palette)
        {
            if (palette?.MicroSplatTemplateMaterial is Material template)
                return template;

            return LoadDefaultTemplateMaterial();
        }

        private static MicroSplatPropData ResolvePropData(WorldSurfacePalette palette)
        {
            if (palette?.MicroSplatPropData is MicroSplatPropData propData)
                return propData;

            return LoadDefaultPropData();
        }

        private static MicroSplatKeywords ResolveKeywords(WorldSurfacePalette palette)
        {
            if (palette?.MicroSplatWorldAsset is MicroSplatKeywords keywords)
                return keywords;

            return LoadDefaultKeywords();
        }

        /// <summary>
        ///     Guarantees <see cref="TerrainData.alphamapLayers"/> &gt; 0 so
        ///     <c>SetAlphamaps</c> accepts a matching float[,,] array.
        /// </summary>
        public static bool EnsureTerrainLayers(Terrain terrain, WorldSurfacePalette palette)
        {
            if (terrain?.terrainData == null)
                return false;

            var data = terrain.terrainData;
            var required = ResolveRequiredLayerCount(palette);
            if (HasCompleteLayers(data.terrainLayers, required) && IsBound(terrain, palette))
                return true;

            if (TryApplyTextureArrayConfig(data, required))
                return data.alphamapLayers > 0;

            if (palette?.TerrainLayers != null && palette.TerrainLayers.Length > 0)
            {
                data.terrainLayers = CloneTerrainLayers(palette.TerrainLayers);
                if (data.alphamapLayers > 0)
                    return true;
            }

            data.terrainLayers = BuildPlaceholderLayers(required);
            return data.alphamapLayers > 0;
        }

        private static int ResolveRequiredLayerCount(WorldSurfacePalette palette)
        {
            var count = WorldSurfacePalette.MaxLayerIndex + 1;
            if (palette?.TerrainLayers != null && palette.TerrainLayers.Length > count)
                count = palette.TerrainLayers.Length;

            if (palette?.Mappings == null)
                return count;

            for (var i = 0; i < palette.Mappings.Count; i++)
            {
                var mapping = palette.Mappings[i];
                if (mapping == null) continue;
                count = Mathf.Max(count, mapping.LayerIndex + 1);
            }

            return Mathf.Clamp(count, 1, 32);
        }

        private static bool HasCompleteLayers(TerrainLayer[] current, int count)
        {
            if (current == null || current.Length < count)
                return false;
            for (var i = 0; i < count; i++)
            {
                if (current[i] == null)
                    return false;
                if (current[i].diffuseTexture == null)
                    return false;
            }

            return true;
        }

        private static bool TryApplyTextureArrayConfig(TerrainData data, int required)
        {
            var cfg = TryLoadDefaultTextureArrayConfig();
            if (cfg?.sourceTextures == null || cfg.sourceTextures.Count == 0)
                return false;

            var count = Mathf.Clamp(Mathf.Max(required, cfg.sourceTextures.Count), 1, 32);
            var layers = new TerrainLayer[count];
            for (var i = 0; i < count; i++)
            {
                if (i < cfg.sourceTextures.Count)
                {
                    var entry = cfg.sourceTextures[i];
                    layers[i] = CreateLayerFromEntry(entry, i);
                }
                else
                {
                    layers[i] = new TerrainLayer { name = "SurfaceLayer_" + i };
                }
            }

            data.terrainLayers = layers;
            return data.alphamapLayers > 0;
        }

        private static TerrainLayer[] BuildPlaceholderLayers(int count)
        {
            var layers = new TerrainLayer[count];
            for (var i = 0; i < count; i++)
                layers[i] = new TerrainLayer { name = "SurfaceLayer_" + i };
            return layers;
        }

        private static TerrainLayer CreateLayerFromEntry(
            TextureArrayConfig.TextureEntry entry, int index)
        {
            var tl = new TerrainLayer
            {
                name = entry.diffuse != null ? entry.diffuse.name : "Layer_" + index,
                diffuseTexture = entry.diffuse
            };
            return tl;
        }

        private static TerrainLayer[] CloneTerrainLayers(TerrainLayer[] source)
        {
            if (source == null || source.Length == 0)
                return null;

            var layers = new TerrainLayer[source.Length];
            for (var i = 0; i < source.Length; i++)
                layers[i] = CloneTerrainLayer(source[i], i);

            return layers;
        }

        private static TerrainLayer CloneTerrainLayer(TerrainLayer source, int index)
        {
            if (source == null)
                return new TerrainLayer { name = "SurfaceLayer_" + index };

#if UNITY_EDITOR
            var clone = Object.Instantiate(source);
            clone.name = source.name;
            return clone;
#else
            return new TerrainLayer
            {
                name = source.name,
                diffuseTexture = source.diffuseTexture,
                normalMapTexture = source.normalMapTexture,
                maskMapTexture = source.maskMapTexture,
                tileSize = source.tileSize,
                tileOffset = source.tileOffset
            };
#endif
        }

        private static TextureArrayConfig TryLoadDefaultTextureArrayConfig()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<TextureArrayConfig>(
                DefaultTextureArrayConfigPath);
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        private static Material LoadDefaultTemplateMaterial()
        {
            if (_cachedDefaultTemplate == null)
                _cachedDefaultTemplate =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(DefaultTemplateMaterialPath);
            return _cachedDefaultTemplate;
        }

        private static MicroSplatKeywords LoadDefaultKeywords()
        {
            if (_cachedDefaultKeywords == null)
                _cachedDefaultKeywords =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<MicroSplatKeywords>(DefaultKeywordsAssetPath);
            return _cachedDefaultKeywords;
        }

        private static MicroSplatPropData LoadDefaultPropData()
        {
            if (_cachedDefaultPropData == null)
                _cachedDefaultPropData =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<MicroSplatPropData>(DefaultPropDataAssetPath);
            return _cachedDefaultPropData;
        }
#else
        private static Material LoadDefaultTemplateMaterial() => null;
        private static MicroSplatKeywords LoadDefaultKeywords() => null;
        private static MicroSplatPropData LoadDefaultPropData() => null;
#endif
    }
}
