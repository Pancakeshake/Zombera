using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     Terrain surface preset for one district type.
    ///     Selects a texture by index from the shared TextureArrayConfig.
    /// </summary>
    [Serializable]
    public sealed class DistrictTerrainPreset
    {
        [Header("Texture Layer")]
        [Tooltip("Index into the TextureArrayConfig texture list (0-based).")]
        [Range(0, 31)]
        public int textureLayerIndex;

        [Header("Settings")]
        [Min(0f)]
        [Tooltip("Height above the lot ground plane.")]
        public float heightOffsetMeters = 0.05f;
    }

    /// <summary>
    ///     Maps a <see cref="City.LotSurfaceType"/> to a texture layer index for
    ///     per-sub-zone material assignment. Falls back to the district-wide
    ///     <see cref="DistrictTerrainPreset"/> when no override exists.
    /// </summary>
    [Serializable]
    public sealed class LotSurfaceTextureOverride
    {
        [Tooltip("Sub-zone surface type to match.")]
        public City.LotSurfaceType surfaceType;

        [Tooltip("Index into the TextureArrayConfig texture list (0-based).")]
        [Range(0, 31)]
        public int textureLayerIndex;

        [Tooltip("Height offset above the lot ground plane for this surface type.")]
        [Min(0f)]
        public float heightOffsetMeters = 0.005f;
    }

    /// <summary>
    ///     Per-district terrain surface catalog.
    ///     References a TextureArrayConfig and each district picks a texture
    ///     by its index. The diffuse/normal textures are read directly —
    ///     no Material cloning, no MicroSplat shader needed.
    /// </summary>
    /// <remarks>
    ///     The config only supplies LAYER INDICES and TerrainLayer prototypes.
    ///     The rendered pixels come from the baked texture arrays referenced by
    ///     the terrain template material
    ///     <c>Assets/01_Game/02_World/Terrain/Materials/Microsplat/MicroSplat.mat</c>.
    ///     After editing textures in the config, click MicroSplat's
    ///     "Update Splat Maps" and verify MicroSplat.mat's _Diffuse/_NormalSAO/
    ///     _AntiTileArray still point at the config's re-baked arrays.
    ///     IMPORTANT: the baked texture-array assets must never be edited
    ///     directly — make all texture changes from within the MicroSplat
    ///     material/config UI ("Update Splat Maps"), never in the .asset files.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Zombera/World/District Lot Terrain Config",
        fileName = "DistrictLotTerrainConfig",
        order = 315)]
    public sealed class DistrictLotTerrainConfig : ScriptableObject
    {
        [Header("Source")]
        [Tooltip("The TextureArrayConfig asset that holds all terrain textures.")]
        public JBooth.MicroSplat.TextureArrayConfig textureArrayConfig;

        [Header("Residential")]
        public DistrictTerrainPreset residential = new();

        [Header("Commercial")]
        public DistrictTerrainPreset commercial = new();

        [Header("Industrial")]
        public DistrictTerrainPreset industrial = new();

        [Header("Hospital")]
        public DistrictTerrainPreset hospital = new();

        [Header("Military")]
        public DistrictTerrainPreset military = new();

        [Header("City Core")]
        public DistrictTerrainPreset cityCore = new();

        [Header("Park")]
        public DistrictTerrainPreset park = new();

        [Header("Mixed")]
        public DistrictTerrainPreset mixed = new();

        [Header("Surface Texture Overrides")]
        [Tooltip("Per-surface-type texture assignments that override the district-wide preset. " +
                 "When a LotSurfaceType has no entry here, the district's main texture is used.")]
        public List<LotSurfaceTextureOverride> surfaceTextureOverrides = new();

        /// <summary>
        ///     Resolves the texture layer index for a specific sub-zone surface type.
        ///     Checks <see cref="surfaceTextureOverrides"/> first, then falls back
        ///     to the district-wide <paramref name="fallbackPreset"/>.
        /// </summary>
        public int ResolveSurfaceTextureIndex(City.LotSurfaceType surfaceType, DistrictTerrainPreset fallbackPreset)
        {
            if (surfaceTextureOverrides != null)
            {
                for (var i = 0; i < surfaceTextureOverrides.Count; i++)
                {
                    if (surfaceTextureOverrides[i].surfaceType == surfaceType)
                        return surfaceTextureOverrides[i].textureLayerIndex;
                }
            }

            return fallbackPreset?.textureLayerIndex ?? 0;
        }

        /// <summary>
        ///     Resolves the height offset for a specific sub-zone surface type.
        /// </summary>
        public float ResolveSurfaceHeightOffset(City.LotSurfaceType surfaceType, DistrictTerrainPreset fallbackPreset)
        {
            if (surfaceTextureOverrides != null)
            {
                for (var i = 0; i < surfaceTextureOverrides.Count; i++)
                {
                    if (surfaceTextureOverrides[i].surfaceType == surfaceType)
                        return surfaceTextureOverrides[i].heightOffsetMeters;
                }
            }

            return fallbackPreset?.heightOffsetMeters ?? 0.005f;
        }

        public DistrictTerrainPreset GetPreset(City.CityDistrictType type)
        {
            return type switch
            {
                City.CityDistrictType.Residential => residential,
                City.CityDistrictType.Commercial  => commercial,
                City.CityDistrictType.Industrial  => industrial,
                City.CityDistrictType.Hospital    => hospital,
                City.CityDistrictType.Military    => military,
                City.CityDistrictType.CityCore    => cityCore,
                City.CityDistrictType.Park        => park,
                City.CityDistrictType.Mixed       => mixed,
                _ => null
            };
        }
    }
}
