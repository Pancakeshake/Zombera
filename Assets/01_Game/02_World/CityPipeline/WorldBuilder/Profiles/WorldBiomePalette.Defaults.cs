using System.Collections.Generic;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public sealed partial class WorldBiomePalette
    {
        /// <summary>Rebuilds biome records with tuned curves and surface weights.</summary>
        public void ApplyProgrammaticDefaults()
        {
            biomes = BuildDefaultBiomes();
        }

        private static List<WorldBiomeRecord> BuildDefaultBiomes()
        {
            return new List<WorldBiomeRecord>
            {
                Make(
                    "Ocean",
                    4f,
                    0f,
                    99f,
                    WorldBiomeCurvePresets.LowBelow(0.99f),
                    WorldBiomeCurvePresets.Bell(0.5f, 1f),
                    WorldBiomeCurvePresets.LowBelow(0.2f),
                    WorldBiomeCurvePresets.Bell(0.1f, 0.4f),
                    ("Sand", 0.7f),
                    ("WetSand", 0.2f),
                    ("Dirt", 0.1f)),
                Make(
                    "Shore",
                    2.2f,
                    0.35f,
                    1.2f,
                    WorldBiomeCurvePresets.Bell(0.55f, 0.5f),
                    WorldBiomeCurvePresets.Bell(0.65f, 0.45f),
                    WorldBiomeCurvePresets.Bell(0.2f, 0.35f),
                    WorldBiomeCurvePresets.SlopeLowToMid(),
                    ("Sand", 0.7f),
                    ("BlackSand", 0.15f),
                    ("Dirt", 0.15f)),
                Make(
                    "Plains",
                    1f,
                    1f,
                    1f,
                    WorldBiomeCurvePresets.Bell(0.55f, 0.4f),
                    WorldBiomeCurvePresets.Bell(0.45f, 0.4f),
                    WorldBiomeCurvePresets.Bell(0.25f, 0.35f),
                    WorldBiomeCurvePresets.SlopeLowToMid(),
                    ("GrassGreen", 0.55f),
                    ("GrassYellow", 0.45f)),
                Make(
                    "Hills",
                    1.15f,
                    0.75f,
                    1.15f,
                    WorldBiomeCurvePresets.Bell(0.5f, 0.45f),
                    WorldBiomeCurvePresets.Bell(0.45f, 0.5f),
                    WorldBiomeCurvePresets.Bell(0.42f, 0.28f),
                    WorldBiomeCurvePresets.Bell(0.45f, 0.35f),
                    ("GrassGreen", 0.5f),
                    ("GrassYellow", 0.5f)),
                Make(
                    "Mountains",
                    1.5f,
                    0.15f,
                    2.2f,
                    WorldBiomeCurvePresets.Bell(0.35f, 0.55f),
                    WorldBiomeCurvePresets.Bell(0.4f, 0.6f),
                    WorldBiomeCurvePresets.HighAbove(0.55f),
                    WorldBiomeCurvePresets.SlopeMidToHigh(),
                    ("CliffDark", 0.4f),
                    ("SnowRock", 0.35f),
                    ("Snow", 0.25f)),
                Make(
                    "Desert",
                    1.1f,
                    0.4f,
                    1.5f,
                    WorldBiomeCurvePresets.HighAbove(0.62f),
                    WorldBiomeCurvePresets.LowBelow(0.32f),
                    WorldBiomeCurvePresets.Bell(0.35f, 0.45f),
                    WorldBiomeCurvePresets.SlopeLowToMid(),
                    ("Sand", 0.55f),
                    ("DesertSand", 0.35f),
                    ("SandCracks", 0.1f)),
                Make(
                    "Snow",
                    1.4f,
                    0.2f,
                    1.8f,
                    WorldBiomeCurvePresets.LowBelow(0.28f),
                    WorldBiomeCurvePresets.Bell(0.45f, 0.7f),
                    WorldBiomeCurvePresets.HighAbove(0.72f),
                    WorldBiomeCurvePresets.SlopeMidToHigh(0.75f),
                    ("Snow", 0.5f),
                    ("SnowRock", 0.3f),
                    ("CliffDark", 0.2f)),
                Make(
                    "Forest",
                    1.2f,
                    0.85f,
                    1.1f,
                    WorldBiomeCurvePresets.Bell(0.52f, 0.35f),
                    WorldBiomeCurvePresets.HighAbove(0.55f),
                    WorldBiomeCurvePresets.Bell(0.38f, 0.35f),
                    WorldBiomeCurvePresets.SlopeLowToMid(0.85f),
                    ("JungleFloor", 0.5f),
                    ("GrassGreen", 0.35f),
                    ("Dirt", 0.15f)),
                Make(
                    "Wetland",
                    1.5f,
                    0.25f,
                    1.6f,
                    WorldBiomeCurvePresets.Bell(0.5f, 0.45f),
                    WorldBiomeCurvePresets.HighAbove(0.78f),
                    WorldBiomeCurvePresets.Bell(0.18f, 0.3f),
                    WorldBiomeCurvePresets.SlopeLowToMid(0.7f),
                    ("WetSand", 0.4f),
                    ("SwampMud", 0.35f),
                    ("GrassGreen", 0.25f)),
                Make(
                    "Badlands",
                    1.25f,
                    0.25f,
                    1.7f,
                    WorldBiomeCurvePresets.HighAbove(0.58f),
                    WorldBiomeCurvePresets.LowBelow(0.28f),
                    WorldBiomeCurvePresets.Bell(0.45f, 0.35f),
                    WorldBiomeCurvePresets.SlopeMidToHigh(),
                    ("CliffRed", 0.45f),
                    ("BlackDirt", 0.35f),
                    ("CliffDark", 0.2f)),
                Make(
                    "Dunes",
                    1.15f,
                    0.35f,
                    1.4f,
                    WorldBiomeCurvePresets.HighAbove(0.68f),
                    WorldBiomeCurvePresets.LowBelow(0.18f),
                    WorldBiomeCurvePresets.Bell(0.22f, 0.3f),
                    WorldBiomeCurvePresets.Bell(0.35f, 0.4f),
                    ("DesertSand", 0.65f),
                    ("Sand", 0.25f),
                    ("SandCracks", 0.1f)),
                Make(
                    "AlpineSnow",
                    1.45f,
                    0.15f,
                    2f,
                    WorldBiomeCurvePresets.LowBelow(0.22f),
                    WorldBiomeCurvePresets.Bell(0.42f, 0.5f),
                    WorldBiomeCurvePresets.HighAbove(0.78f),
                    WorldBiomeCurvePresets.SlopeMidToHigh(),
                    ("Snow", 0.45f),
                    ("SnowRock", 0.35f),
                    ("CliffDark", 0.2f)),
                Make(
                    "Savanna",
                    1.05f,
                    0.9f,
                    1.05f,
                    WorldBiomeCurvePresets.HighAbove(0.62f),
                    WorldBiomeCurvePresets.Bell(0.28f, 0.25f),
                    WorldBiomeCurvePresets.Bell(0.25f, 0.35f),
                    WorldBiomeCurvePresets.SlopeLowToMid(),
                    ("MeadowGrass", 0.55f),
                    ("SparseGrass", 0.45f)),
                Make(
                    "Taiga",
                    1.25f,
                    0.7f,
                    1.2f,
                    WorldBiomeCurvePresets.LowBelow(0.32f),
                    WorldBiomeCurvePresets.Bell(0.52f, 0.45f),
                    WorldBiomeCurvePresets.Bell(0.35f, 0.35f),
                    WorldBiomeCurvePresets.SlopeLowToMid(),
                    ("DryForestFloor", 0.6f),
                    ("SparseGrass", 0.4f)),
                Make(
                    "Swamp",
                    1.55f,
                    0.2f,
                    1.75f,
                    WorldBiomeCurvePresets.Bell(0.58f, 0.35f),
                    WorldBiomeCurvePresets.HighAbove(0.82f),
                    WorldBiomeCurvePresets.LowBelow(0.22f),
                    WorldBiomeCurvePresets.SlopeLowToMid(0.6f),
                    ("SwampMud", 0.65f),
                    ("JungleFloor", 0.35f)),
                Make(
                    "Scrubland",
                    1.1f,
                    0.55f,
                    1.35f,
                    WorldBiomeCurvePresets.HighAbove(0.55f),
                    WorldBiomeCurvePresets.LowBelow(0.35f),
                    WorldBiomeCurvePresets.Bell(0.38f, 0.4f),
                    WorldBiomeCurvePresets.Bell(0.42f, 0.35f),
                    ("MeadowGrass", 0.35f),
                    ("SparseGrass", 0.35f),
                    ("BlackDirt", 0.3f)),
                Make(
                    "Ashlands",
                    1.6f,
                    0.3f,
                    1.9f,
                    WorldBiomeCurvePresets.Bell(0.48f, 0.12f),
                    WorldBiomeCurvePresets.LowBelow(0.25f),
                    WorldBiomeCurvePresets.Bell(0.5f, 0.25f),
                    WorldBiomeCurvePresets.SlopeMidToHigh(0.85f),
                    ("LavaRock", 0.5f),
                    ("LavaGround", 0.35f),
                    ("BlackDirt", 0.15f)),
                Make(
                    "CityArea",
                    3f,
                    1f,
                    1f,
                    WorldBiomeCurvePresets.Bell(0.5f, 1f),
                    WorldBiomeCurvePresets.Bell(0.5f, 1f),
                    WorldBiomeCurvePresets.Bell(0.5f, 1f),
                    WorldBiomeCurvePresets.Bell(0.2f, 0.8f))
            };
        }

        private static WorldBiomeRecord Make(
            string stableId,
            float priority,
            float buildability,
            float roadCost,
            AnimationCurve temp,
            AnimationCurve moisture,
            AnimationCurve elevation,
            AnimationCurve slope,
            params (string semantic, float weight)[] surfaces)
        {
            var record = new WorldBiomeRecord
            {
                StableId = stableId,
                Priority = priority,
                BuildabilityMultiplier = buildability,
                RoadCostMultiplier = roadCost,
                TemperatureCurve = temp,
                MoistureCurve = moisture,
                ElevationCurve = elevation,
                SlopeCurve = slope,
                IsCityAreaOverlay = stableId == "CityArea"
            };

            for (var i = 0; i < surfaces.Length; i++)
            {
                record.SurfaceWeights.Add(new WorldBiomeSurfaceWeight
                {
                    SurfaceSemantic = surfaces[i].semantic,
                    Weight = surfaces[i].weight
                });
            }

            return record;
        }
    }
}
