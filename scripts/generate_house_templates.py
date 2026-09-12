#!/usr/bin/env python3
"""
Generate ResidentialHouseTemplate .asset files with real-world house data.
Each template models a real housing archetype with appropriate room counts,
footprint dimensions (1 cell = 3m × 3m), adjacency rules, and door counts.
"""

import os

OUTPUT_DIR = "Assets/02_Shared/ScriptableObjects/World/Buildings/Templates"

# The GUID of ResidentialHouseTemplate.cs
SCRIPT_GUID = "d1153882a1c31aa479830f76066dec2f"

# RoomType enum values (int index matches enum order)
# LivingRoom=0, Kitchen=1, Bedroom=2, Bathroom=3, Hallway=4,
# Dining=5, Storage=6, Garage=7, Entry=8, Utility=9
ROOM = {
    "LivingRoom": 0,
    "Kitchen": 1,
    "Bedroom": 2,
    "Bathroom": 3,
    "Hallway": 4,
    "Dining": 5,
    "Storage": 6,
    "Garage": 7,
    "Entry": 8,
    "Utility": 9,
}

# AdjacencyType enum: Required=0, Preferred=1, Avoid=2
ADJ = {"Required": 0, "Preferred": 1, "Avoid": 2}

# CityDistrictType — Residential=0 (from Zombera.World.City.CityDistrictType)
DISTRICT_RESIDENTIAL = 0


def room_entry(room_type, min_count, max_count, min_w, max_w, min_d, max_d):
    """Return a RoomTemplateEntry YAML block."""
    return (
        f"  - RoomType: {ROOM[room_type]}\n"
        f"    MinCount: {min_count}\n"
        f"    MaxCount: {max_count}\n"
        f"    MinWidthCells: {min_w}\n"
        f"    MaxWidthCells: {max_w}\n"
        f"    MinDepthCells: {min_d}\n"
        f"    MaxDepthCells: {max_d}"
    )


def adjacency_rule(room_a, room_b, adj_type):
    """Return a RoomAdjacencyRule YAML block."""
    return (
        f"  - RoomA: {ROOM[room_a]}\n"
        f"    RoomB: {ROOM[room_b]}\n"
        f"    Adjacency: {ADJ[adj_type]}"
    )


def build_template(name, display_name, prefer_open, floors, min_w, max_w, min_d, max_d,
                   required, optional, adjacency, min_doors, max_doors):
    """Build the complete .asset YAML content."""
    req_entries = "\n".join(required)
    opt_entries = "\n".join(optional) if optional else "[]"
    adj_entries = "\n".join(adjacency) if adjacency else "[]"

    return (
        f"%YAML 1.1\n"
        f"%TAG !u! tag:unity3d.com,2011:\n"
        f"--- !u!114 &11400000\n"
        f"MonoBehaviour:\n"
        f"  m_ObjectHideFlags: 0\n"
        f"  m_CorrespondingSourceObject: {{fileID: 0}}\n"
        f"  m_PrefabInstance: {{fileID: 0}}\n"
        f"  m_PrefabAsset: {{fileID: 0}}\n"
        f"  m_GameObject: {{fileID: 0}}\n"
        f"  m_Enabled: 1\n"
        f"  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}\n"
        f"  m_Name: {name}\n"
        f"  m_EditorClassIdentifier: Zombera.Editor::Zombera.Editor.ResidentialHouseTemplate\n"
        f"  DisplayName: {display_name}\n"
        f"  LotType: {DISTRICT_RESIDENTIAL}\n"
        f"  PreferOpenPlan: {1 if prefer_open else 0}\n"
        f"  PreferredFloorCount: {floors}\n"
        f"  MinFootprintWidth: {min_w}\n"
        f"  MaxFootprintWidth: {max_w}\n"
        f"  MinFootprintDepth: {min_d}\n"
        f"  MaxFootprintDepth: {max_d}\n"
        f"  RequiredRooms:\n"
        f"{req_entries}\n"
        f"  OptionalRooms:\n"
        f"{opt_entries}\n"
        f"  AdjacencyRules:\n"
        f"{adj_entries}\n"
        f"  MinExteriorDoors: {min_doors}\n"
        f"  MaxExteriorDoors: {max_doors}\n"
    )


def make_meta(guid):
    """Generate a .meta file for a .asset file."""
    return (
        f"fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        f"NativeFormatImporter:\n"
        f"  externalObjects: {{}}\n"
        f"  mainObjectFileID: 11400000\n"
        f"  userData: \n"
        f"  assetBundleName: \n"
        f"  assetBundleVariant: \n"
    )


# Per-file GUIDs — deterministic stable IDs so Unity doesn't think these are new files
# on each regeneration. These are synthetically generated.
FILE_GUIDS = {
    "Residential_MicroStudio": "a1000000000000000000000000000001",
    "Residential_Studio": "a1000000000000000000000000000002",
    "Residential_StudioPlus": "a1000000000000000000000000000003",
    "Residential_1BR": "a1000000000000000000000000000004",
    "Residential_1BRPlusDen": "a1000000000000000000000000000005",
    "Residential_2BR": "a1000000000000000000000000000006",
    "Residential_2BRPlusOffice": "a1000000000000000000000000000007",
    "Residential_3BR": "a1000000000000000000000000000008",
    "Residential_3BRPlusBonus": "a1000000000000000000000000000009",
    "Residential_4BR": "a100000000000000000000000000000a",
    "Residential_Bungalow": "a100000000000000000000000000000b",
    "Residential_Ranch": "a100000000000000000000000000000c",
    "Residential_SplitLevel": "a100000000000000000000000000000d",
    "Residential_Townhouse": "a100000000000000000000000000000e",
    "Residential_DuplexHalf": "a100000000000000000000000000000f",
}


# =============================================================================
# Template Definitions — real-world housing archetypes
# =============================================================================

TEMPLATES = []

# ── 1. Micro Studio (~15-25 m² / 2-3 cells) ──
TEMPLATES.append(build_template(
    name="Residential_MicroStudio",
    display_name="Micro Studio",
    prefer_open=True, floors=1,
    min_w=2, max_w=3, min_d=2, max_d=3,
    required=[
        room_entry("LivingRoom", 1, 1, 1, 2, 1, 2),
        room_entry("Kitchen", 1, 1, 1, 1, 1, 1),
        room_entry("Bathroom", 1, 1, 1, 1, 1, 1),
    ],
    optional=[
        room_entry("Storage", 0, 1, 1, 1, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
    ],
    min_doors=1, max_doors=1,
))

# ── 2. Standard Studio (~25-35 m² / 3-4 cells) ──
TEMPLATES.append(build_template(
    name="Residential_Studio",
    display_name="Studio",
    prefer_open=True, floors=1,
    min_w=3, max_w=4, min_d=3, max_d=4,
    required=[
        room_entry("LivingRoom", 1, 1, 2, 3, 2, 3),
        room_entry("Kitchen", 1, 1, 1, 2, 1, 2),
        room_entry("Bathroom", 1, 1, 1, 1, 1, 2),
    ],
    optional=[
        room_entry("Hallway", 0, 1, 1, 2, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
    ],
    min_doors=1, max_doors=1,
))

# ── 3. Studio Plus (~35-50 m² / 4-6 cells) — larger studio with dining nook ──
TEMPLATES.append(build_template(
    name="Residential_StudioPlus",
    display_name="Studio Plus",
    prefer_open=True, floors=1,
    min_w=4, max_w=5, min_d=3, max_d=5,
    required=[
        room_entry("LivingRoom", 1, 1, 2, 3, 2, 3),
        room_entry("Kitchen", 1, 1, 2, 2, 1, 2),
        room_entry("Bathroom", 1, 1, 1, 2, 1, 2),
    ],
    optional=[
        room_entry("Dining", 0, 1, 1, 2, 1, 2),
        room_entry("Hallway", 0, 1, 1, 2, 1, 1),
        room_entry("Storage", 0, 1, 1, 1, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
    ],
    min_doors=1, max_doors=1,
))

# ── 4. 1 Bedroom (~45-65 m² / 5-7 cells) ──
TEMPLATES.append(build_template(
    name="Residential_1BR",
    display_name="1 Bedroom",
    prefer_open=False, floors=1,
    min_w=4, max_w=6, min_d=4, max_d=6,
    required=[
        room_entry("LivingRoom", 1, 1, 2, 3, 2, 3),
        room_entry("Kitchen", 1, 1, 2, 2, 2, 2),
        room_entry("Bedroom", 1, 1, 2, 3, 2, 2),
        room_entry("Bathroom", 1, 1, 1, 2, 1, 2),
        room_entry("Hallway", 1, 1, 1, 1, 1, 1),
    ],
    optional=[
        room_entry("Dining", 0, 1, 2, 2, 2, 2),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
    ],
    min_doors=1, max_doors=2,
))

# ── 5. 1 Bedroom + Den (~55-75 m² / 6-8 cells) — flex space for office/nursery ──
TEMPLATES.append(build_template(
    name="Residential_1BRPlusDen",
    display_name="1 Bedroom + Den",
    prefer_open=False, floors=1,
    min_w=5, max_w=7, min_d=4, max_d=6,
    required=[
        room_entry("LivingRoom", 1, 1, 2, 3, 2, 3),
        room_entry("Kitchen", 1, 1, 2, 2, 2, 2),
        room_entry("Bedroom", 1, 1, 2, 3, 2, 3),
        room_entry("Bathroom", 1, 1, 1, 2, 1, 2),
        room_entry("Hallway", 1, 1, 1, 2, 1, 1),
    ],
    optional=[
        room_entry("Dining", 0, 1, 2, 2, 2, 2),
        room_entry("Storage", 0, 1, 1, 2, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
    ],
    min_doors=1, max_doors=2,
))

# ── 6. 2 Bedroom (~65-90 m² / 7-10 cells) ──
TEMPLATES.append(build_template(
    name="Residential_2BR",
    display_name="2 Bedroom",
    prefer_open=False, floors=1,
    min_w=5, max_w=8, min_d=5, max_d=8,
    required=[
        room_entry("LivingRoom", 1, 1, 3, 3, 2, 3),
        room_entry("Kitchen", 1, 1, 2, 3, 2, 3),
        room_entry("Bedroom", 2, 2, 2, 3, 2, 3),
        room_entry("Bathroom", 1, 1, 1, 2, 1, 2),
        room_entry("Hallway", 1, 1, 1, 2, 1, 1),
    ],
    optional=[
        room_entry("Dining", 0, 1, 2, 3, 2, 2),
        room_entry("Storage", 0, 1, 1, 1, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
    ],
    min_doors=1, max_doors=2,
))

# ── 7. 2 Bedroom + Office (~75-100 m² / 8-11 cells) ──
TEMPLATES.append(build_template(
    name="Residential_2BRPlusOffice",
    display_name="2 Bedroom + Office",
    prefer_open=False, floors=1,
    min_w=6, max_w=9, min_d=5, max_d=8,
    required=[
        room_entry("LivingRoom", 1, 1, 3, 3, 2, 3),
        room_entry("Kitchen", 1, 1, 2, 3, 2, 3),
        room_entry("Bedroom", 2, 2, 2, 3, 2, 3),
        room_entry("Bathroom", 1, 1, 1, 2, 1, 2),
        room_entry("Hallway", 1, 1, 1, 2, 1, 1),
    ],
    optional=[
        room_entry("Dining", 0, 1, 2, 3, 2, 3),
        room_entry("Storage", 0, 1, 1, 2, 1, 1),
        room_entry("Utility", 0, 1, 1, 2, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
    ],
    min_doors=1, max_doors=2,
))

# ── 8. 3 Bedroom (~90-130 m² / 10-14 cells) ──
TEMPLATES.append(build_template(
    name="Residential_3BR",
    display_name="3 Bedroom",
    prefer_open=False, floors=2,
    min_w=6, max_w=10, min_d=6, max_d=10,
    required=[
        room_entry("LivingRoom", 1, 1, 3, 4, 3, 4),
        room_entry("Kitchen", 1, 1, 2, 3, 2, 3),
        room_entry("Bedroom", 3, 3, 2, 3, 2, 3),
        room_entry("Bathroom", 2, 2, 1, 2, 1, 2),
        room_entry("Hallway", 1, 1, 1, 2, 1, 1),
    ],
    optional=[
        room_entry("Dining", 0, 1, 2, 3, 2, 3),
        room_entry("Storage", 0, 1, 1, 2, 1, 1),
        room_entry("Garage", 0, 1, 2, 3, 3, 4),
        room_entry("Utility", 0, 1, 1, 2, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
        adjacency_rule("Garage", "Entry", "Required"),
    ],
    min_doors=2, max_doors=3,
))

# ── 9. 3 Bedroom + Bonus (~110-160 m² / 12-18 cells) — bonus room above garage ──
TEMPLATES.append(build_template(
    name="Residential_3BRPlusBonus",
    display_name="3 Bedroom + Bonus",
    prefer_open=False, floors=2,
    min_w=7, max_w=12, min_d=7, max_d=10,
    required=[
        room_entry("LivingRoom", 1, 1, 3, 5, 3, 4),
        room_entry("Kitchen", 1, 1, 3, 4, 2, 3),
        room_entry("Bedroom", 3, 3, 2, 3, 2, 3),
        room_entry("Bathroom", 3, 3, 1, 2, 1, 2),
        room_entry("Hallway", 1, 2, 1, 2, 1, 2),
    ],
    optional=[
        room_entry("Dining", 0, 1, 2, 3, 2, 3),
        room_entry("Storage", 0, 2, 1, 2, 1, 2),
        room_entry("Garage", 0, 1, 2, 4, 3, 5),
        room_entry("Utility", 0, 1, 1, 2, 1, 2),
        room_entry("Entry", 0, 1, 1, 2, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
        adjacency_rule("Garage", "Entry", "Required"),
        adjacency_rule("Garage", "Utility", "Preferred"),
    ],
    min_doors=2, max_doors=4,
))

# ── 10. 4 Bedroom (~130-180 m² / 14-20 cells) ──
TEMPLATES.append(build_template(
    name="Residential_4BR",
    display_name="4 Bedroom",
    prefer_open=False, floors=2,
    min_w=8, max_w=14, min_d=7, max_d=12,
    required=[
        room_entry("LivingRoom", 1, 1, 4, 5, 3, 5),
        room_entry("Kitchen", 1, 1, 3, 4, 2, 3),
        room_entry("Bedroom", 4, 4, 2, 4, 2, 3),
        room_entry("Bathroom", 3, 3, 1, 2, 1, 2),
        room_entry("Hallway", 1, 2, 1, 2, 1, 2),
    ],
    optional=[
        room_entry("Dining", 0, 1, 3, 4, 3, 4),
        room_entry("Storage", 0, 2, 1, 2, 1, 2),
        room_entry("Garage", 0, 2, 3, 5, 4, 6),
        room_entry("Utility", 0, 1, 2, 3, 1, 2),
        room_entry("Entry", 0, 1, 1, 2, 1, 2),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
        adjacency_rule("Garage", "Entry", "Required"),
        adjacency_rule("Garage", "Utility", "Preferred"),
        adjacency_rule("Kitchen", "Utility", "Preferred"),
    ],
    min_doors=2, max_doors=4,
))

# ── 11. Bungalow (~70-100 m² / 8-11 cells) — compact single-story ──
TEMPLATES.append(build_template(
    name="Residential_Bungalow",
    display_name="Bungalow",
    prefer_open=False, floors=1,
    min_w=5, max_w=8, min_d=5, max_d=8,
    required=[
        room_entry("LivingRoom", 1, 1, 3, 4, 2, 3),
        room_entry("Kitchen", 1, 1, 2, 3, 2, 3),
        room_entry("Bedroom", 2, 3, 2, 3, 2, 3),
        room_entry("Bathroom", 1, 2, 1, 2, 1, 2),
        room_entry("Hallway", 1, 1, 1, 2, 1, 1),
    ],
    optional=[
        room_entry("Dining", 0, 1, 2, 3, 2, 2),
        room_entry("Storage", 0, 1, 1, 2, 1, 1),
        room_entry("Entry", 0, 1, 1, 1, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
    ],
    min_doors=1, max_doors=2,
))

# ── 12. Ranch (~100-150 m² / 11-17 cells) — wide single-story ──
TEMPLATES.append(build_template(
    name="Residential_Ranch",
    display_name="Ranch",
    prefer_open=True, floors=1,
    min_w=7, max_w=12, min_d=5, max_d=8,
    required=[
        room_entry("LivingRoom", 1, 1, 4, 5, 3, 4),
        room_entry("Kitchen", 1, 1, 3, 4, 2, 3),
        room_entry("Bedroom", 3, 4, 2, 3, 2, 3),
        room_entry("Bathroom", 2, 3, 1, 2, 1, 2),
        room_entry("Hallway", 1, 2, 1, 2, 1, 2),
    ],
    optional=[
        room_entry("Dining", 0, 1, 3, 4, 2, 3),
        room_entry("Storage", 0, 2, 1, 2, 1, 1),
        room_entry("Garage", 0, 2, 3, 5, 4, 6),
        room_entry("Utility", 0, 1, 2, 3, 1, 2),
        room_entry("Entry", 0, 1, 1, 2, 1, 2),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Garage", "Entry", "Required"),
        adjacency_rule("Garage", "Utility", "Preferred"),
    ],
    min_doors=2, max_doors=4,
))

# ── 13. Split-Level (~100-140 m² / 11-16 cells) — 1.5 story with basement ──
TEMPLATES.append(build_template(
    name="Residential_SplitLevel",
    display_name="Split-Level",
    prefer_open=False, floors=2,
    min_w=6, max_w=10, min_d=6, max_d=9,
    required=[
        room_entry("LivingRoom", 1, 1, 3, 4, 3, 4),
        room_entry("Kitchen", 1, 1, 2, 3, 2, 3),
        room_entry("Bedroom", 3, 4, 2, 3, 2, 3),
        room_entry("Bathroom", 2, 3, 1, 2, 1, 2),
        room_entry("Hallway", 1, 2, 1, 2, 1, 2),
    ],
    optional=[
        room_entry("Dining", 0, 1, 2, 3, 2, 3),
        room_entry("Storage", 0, 1, 1, 2, 1, 2),
        room_entry("Utility", 0, 1, 1, 2, 1, 2),
        room_entry("Entry", 0, 1, 1, 2, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
    ],
    min_doors=2, max_doors=3,
))

# ── 14. Townhouse (~80-120 m² / 9-13 cells) — narrow deep 2-3 story ──
TEMPLATES.append(build_template(
    name="Residential_Townhouse",
    display_name="Townhouse",
    prefer_open=False, floors=3,
    min_w=3, max_w=5, min_d=6, max_d=10,
    required=[
        room_entry("LivingRoom", 1, 1, 3, 4, 3, 4),
        room_entry("Kitchen", 1, 1, 2, 3, 2, 3),
        room_entry("Bedroom", 2, 3, 2, 3, 2, 3),
        room_entry("Bathroom", 2, 3, 1, 2, 1, 2),
        room_entry("Hallway", 1, 2, 1, 1, 1, 2),
    ],
    optional=[
        room_entry("Dining", 0, 1, 2, 3, 2, 2),
        room_entry("Storage", 0, 1, 1, 2, 1, 1),
        room_entry("Garage", 0, 1, 2, 3, 3, 4),
        room_entry("Entry", 0, 1, 1, 2, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Garage", "Entry", "Required"),
    ],
    min_doors=2, max_doors=3,
))

# ── 15. Duplex Half (~60-85 m² / 7-9 cells) — one side of a duplex ──
TEMPLATES.append(build_template(
    name="Residential_DuplexHalf",
    display_name="Duplex Half",
    prefer_open=False, floors=2,
    min_w=3, max_w=5, min_d=5, max_d=8,
    required=[
        room_entry("LivingRoom", 1, 1, 2, 3, 2, 3),
        room_entry("Kitchen", 1, 1, 2, 2, 2, 2),
        room_entry("Bedroom", 2, 3, 2, 3, 2, 2),
        room_entry("Bathroom", 1, 2, 1, 2, 1, 2),
        room_entry("Hallway", 1, 1, 1, 1, 1, 1),
    ],
    optional=[
        room_entry("Dining", 0, 1, 2, 2, 2, 2),
        room_entry("Storage", 0, 1, 1, 1, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Bathroom", "Kitchen", "Avoid"),
    ],
    min_doors=1, max_doors=2,
))


# ── Extra: Fix the Townhouse (it had a bad max_w_d=0 field) and rebuild list ──
# Remove the broken townhouse and rebuild properly
# Actually let me just fix the townhouse call directly.

# We rebuild TEMPLATES properly, re-creating the townhouse:
del TEMPLATES[13]  # remove the broken townhouse

TEMPLATES.insert(13, build_template(
    name="Residential_Townhouse",
    display_name="Townhouse",
    prefer_open=False, floors=3,
    min_w=3, max_w=5, min_d=6, max_d=10,
    required=[
        room_entry("LivingRoom", 1, 1, 3, 4, 3, 4),
        room_entry("Kitchen", 1, 1, 2, 3, 2, 3),
        room_entry("Bedroom", 2, 3, 2, 3, 2, 3),
        room_entry("Bathroom", 2, 3, 1, 2, 1, 2),
        room_entry("Hallway", 1, 2, 1, 1, 1, 2),
    ],
    optional=[
        room_entry("Dining", 0, 1, 2, 3, 2, 2),
        room_entry("Storage", 0, 1, 1, 2, 1, 1),
        room_entry("Garage", 0, 1, 2, 3, 3, 4),
        room_entry("Entry", 0, 1, 1, 2, 1, 1),
    ],
    adjacency=[
        adjacency_rule("LivingRoom", "Kitchen", "Required"),
        adjacency_rule("Kitchen", "Dining", "Preferred"),
        adjacency_rule("Bedroom", "Bathroom", "Preferred"),
        adjacency_rule("Bathroom", "Hallway", "Required"),
        adjacency_rule("Garage", "Entry", "Required"),
    ],
    min_doors=2, max_doors=3,
))


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    count = 0
    for template in TEMPLATES:
        # Extract name from first few lines (cheap parse)
        lines = template.split("\n")
        name = None
        for i, line in enumerate(lines):
            if line.strip().startswith("m_Name:"):
                name = line.split(":", 1)[1].strip()
                break

        if name is None:
            print("ERROR: could not find m_Name in template")
            continue

        asset_path = os.path.join(OUTPUT_DIR, f"{name}.asset")
        meta_path = os.path.join(OUTPUT_DIR, f"{name}.asset.meta")

        with open(asset_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(template)

        guid = FILE_GUIDS.get(name, f"a10000000000000000000000000000ff")
        meta_content = make_meta(guid)
        with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(meta_content)

        print(f"  Created: {asset_path}")
        count += 1

    print(f"\nDone. Generated {count} template assets in {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
