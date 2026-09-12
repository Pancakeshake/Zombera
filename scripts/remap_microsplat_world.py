#!/usr/bin/env python3
"""Reorder Microsplat_World.asset sourceTextures to match WorldSurfacePalette semantics."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(r"c:\Zombera")
ASSET = ROOT / "Assets/01_Game/02_World/Terrain/Materials/Microsplat/Microsplat_World.asset"
LAYER_DIR = ROOT / "Assets/01_Game/02_World/Terrain/Materials/Microsplat"

TARGET_LAYERS = [
    "microsplat_layer_grass_ground_68_22_basecolor_diffuse_0",
    "microsplat_layer_grass_ground_68_26_basecolor_diffuse_1",
    "microsplat_layer_lake_pebbles_66_01_basecolor_diffuse_10",
    "microsplat_layer_grass_albedo_3",
    "microsplat_layer_sparse_grass_diff_2k_4",
    "microsplat_layer_grey_rocky_cliff_66_74_basecolor_diffuse_6",
    "microsplat_layer_beach_sand_59_52_basecolor_diffuse_15",
    "microsplat_layer_dark_rock_68_28_basecolor_diffuse_7",
    "microsplat_layer_red_canyon_cliff_66_57_basecolor_diffuse_9",
    "microsplat_layer_yellow_rocky_cliff_66_77_basecolor_diffuse_8",
    "microsplat_layer_snow_66_31_basecolor_diffuse_4",
    "microsplat_layer_sandy_cracked_rock_69_17_basecolor_diffuse_31",
    "microsplat_layer_dirt_albedo_12",
    "microsplat_layer_Asphalt_13",
    "microsplat_layer_fresh_concrete_67_46_basecolor_diffuse_14",
    "microsplat_layer_ConcreteTiles_15",
    "microsplat_layer_clean_asphalt_diff_2k_16",
    "microsplat_layer_desert_sand_58_32_basecolor_diffuse_16",
    "microsplat_layer_lava_rock_60_47_basecolor_diffuse_17",
    "microsplat_layer_lava_ground_58_28_basecolor_diffuse_18",
    "microsplat_layer_black_ground_dirt_60_87_basecolor_diffuse_19",
    "microsplat_layer_black_beach_sand_60_86_basecolor_diffuse_20",
    "microsplat_layer_frozen_lake_60_50_basecolor_diffuse_20",
    "microsplat_layer_jungle_root_ground_58_38_basecolor_diffuse_21",
    "microsplat_layer_dark_rock_with_snow_68_29_basecolor_diffuse_22",
    "microsplat_layer_swamp_62_40_basecolor_diffuse_23",
    "microsplat_layer_dry_forest_ground_60_59_basecolor_diffuse_24",
    "microsplat_layer_grass_ground_68_23_basecolor_diffuse_25",
    "microsplat_layer_wet_wavy_rock_68_86_basecolor_diffuse_26",
    "microsplat_layer_wet_river_sand_66_98_basecolor_diffuse_27",
    "microsplat_layer_ice_66_50_basecolor_diffuse_28",
    "microsplat_layer_wet_sand_66_29_basecolor_diffuse_30",
]

ENTRY_START = re.compile(r"^  - terrainLayer:", re.M)
GUID_RE = re.compile(r"guid: ([0-9a-f]+)")
TL_GUID_RE = re.compile(
    r"terrainLayer: \{fileID: \d+, guid: ([0-9a-f]+), type: 2\}"
)


def load_guid_map() -> dict[str, str]:
    mapping: dict[str, str] = {}
    for meta in LAYER_DIR.glob("*.terrainlayer.meta"):
        text = meta.read_text(encoding="utf-8")
        m = re.search(r"^guid: (\S+)", text, re.M)
        if m:
            mapping[m.group(1)] = meta.name.replace(".meta", "")
    return mapping


def split_entries(block: str) -> list[str]:
    parts = ENTRY_START.split(block)
    return ["  - terrainLayer:" + p for p in parts[1:]]


def entry_guid(entry: str) -> str | None:
    m = TL_GUID_RE.search(entry)
    return m.group(1) if m else None


def blank_entry() -> str:
    return """  - terrainLayer: {fileID: 0}
    diffuse: {fileID: 0}
    height: {fileID: 0}
    heightChannel: 1
    normal: {fileID: 0}
    smoothness: {fileID: 0}
    smoothnessChannel: 1
    isRoughness: 0
    ao: {fileID: 0}
    aoChannel: 1
    emis: {fileID: 0}
    metal: {fileID: 0}
    metalChannel: 1
    specular: {fileID: 0}
    noiseNormal: {fileID: 0}
    detailNoise: {fileID: 0}
    detailChannel: 1
    distanceNoise: {fileID: 0}
    distanceChannel: 1
    traxDiffuse: {fileID: 0}
    traxHeight: {fileID: 0}
    traxHeightChannel: 1
    traxNormal: {fileID: 0}
    traxSmoothness: {fileID: 0}
    traxSmoothnessChannel: 1
    traxIsRoughness: 0
    traxAO: {fileID: 0}
    traxAOChannel: 1
    splat: {fileID: 0}"""


def main() -> None:
    text = ASSET.read_text(encoding="utf-8")
    marker = "  sourceTextures:\n"
    idx = text.index(marker)
    tail_marker = "\n  sourceTextures2:"
    end = text.index(tail_marker, idx)

    head = text[: idx + len(marker)]
    block = text[idx + len(marker) : end]
    tail = text[end:]

    entries = split_entries(block)
    by_guid: dict[str, str] = {}
    by_name: dict[str, str] = {}
    guid_to_name = load_guid_map()

    for entry in entries:
        g = entry_guid(entry)
        if not g:
            continue
        by_guid[g] = entry
        name = guid_to_name.get(g)
        if name:
            by_name[name] = entry

    ordered: list[str] = []
    missing: list[str] = []
    for i, layer_name in enumerate(TARGET_LAYERS):
        layer_guid = None
        meta = LAYER_DIR / f"{layer_name}.terrainlayer.meta"
        if meta.exists():
            m = re.search(r"^guid: (\S+)", meta.read_text(encoding="utf-8"), re.M)
            if m:
                layer_guid = m.group(1)
        entry = None
        if layer_guid and layer_guid in by_guid:
            entry = by_guid[layer_guid]
        elif layer_name in by_name:
            entry = by_name[layer_name]
        if entry is None:
            missing.append(f"{i}: {layer_name}")
            entry = blank_entry()
        ordered.append(entry.rstrip("\n"))

    if missing:
        print("WARNING missing entries (blank placeholders used):")
        for line in missing:
            print(" ", line)

    # Pad to 32 slots if source had more placeholder capacity
    while len(ordered) < 32:
        ordered.append(blank_entry().rstrip("\n"))

    new_block = "\n".join(ordered) + "\n"
    ASSET.write_text(head + new_block + tail, encoding="utf-8")
    print(f"Wrote {len(TARGET_LAYERS)} mapped slots (+ padding) to {ASSET}")


if __name__ == "__main__":
    main()
