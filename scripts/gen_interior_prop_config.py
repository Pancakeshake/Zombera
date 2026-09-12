"""Scan prop prefabs, extract GUIDs, generate InteriorPropConfig.asset YAML."""
import os, re, json

BASE = r"C:\Zombera"
PROPS_ROOT = os.path.join(BASE, "Assets", "02_Shared", "Prefabs", "Props")
ASSET_PATH = os.path.join(BASE, "Assets", "02_Shared", "ScriptableObjects", "Buildings", "Props", "InteriorPropConfig.asset")

# ── Room assignments: (folder, filename-prefix) → [(roomType_enum_value, weight_hint)] ──
# RoomType enum: LivingRoom=0, Kitchen=1, Bedroom=2, Bathroom=3, Hallway=4, Dining=5, Storage=6, Garage=7, Entry=8, Utility=9

ROOM_RULES = {
    # Kitchen
    ("Tables", None):           [(1, 1.0)],
    ("Chair", None):            [(0, 1.5), (1, 1.5), (2, 0.5), (5, 0.5)],  # mostly living/kitchen
    ("Cans", None):             [(1, 1.0), (6, 0.5)],
    ("Shelves", "Shelf"):       [(1, 0.8), (6, 1.0), (4, 0.5), (2, 0.3), (7, 0.5), (9, 0.5)],
    ("Water", "SM_WaterCan"):   [(1, 0.5), (3, 0.5), (9, 0.5)],
    ("Water", "SM_Waterfilter"):[(9, 1.0), (1, 0.3)],
    ("Water", "SM_WaterTank"):  [(7, 0.5), (9, 0.5)],
    ("Tools", "SM_Sponge"):     [(1, 1.0), (3, 1.0)],
    ("Tools", "SM_Scissors"):   [(1, 0.5), (2, 0.3)],
    ("Bins", None):             [(1, 1.0), (3, 1.0), (4, 0.3), (6, 0.5), (7, 0.5)],
    ("Fabric", "Rag"):          [(1, 0.5), (3, 0.5), (6, 0.5)],
    ("Fire Exstinguishers", "Extinguisher"): [(1, 0.3), (7, 0.5)],

    # LivingRoom
    ("Living Modular", "Canopy"):      [(0, 0.5), (4, 0.5)],
    ("Living Modular", "MetalSheet"):  [(0, 0.3), (4, 0.5), (6, 0.3)],
    ("Living Modular", "Dish"):        [(0, 0.5), (5, 0.5)],
    ("Living Modular", "SymbolBoards"):[(0, 0.5)],
    ("Benches", None):          [(0, 0.5), (5, 0.5), (4, 0.3)],
    ("Misc", "SM_Umbrella"):    [(0, 0.5), (4, 0.5), (8, 0.5)],

    # Bedroom
    ("Fabric", "Fabric"):       [(2, 1.0), (6, 0.5)],  # cloth bolts
    ("Boxes", "SM_Cardboardboxes"): [(2, 0.5), (6, 1.0), (4, 0.3)],
    ("Boxes", "SM_BoxesPack"):  [(2, 0.3), (6, 1.0)],
    ("Boxes", "SM_RacksBox"):   [(6, 1.0), (7, 0.5)],
    ("Boxes", "WoodBox"):       [(6, 1.0), (7, 0.5), (2, 0.3)],
    ("Misc", "SM_Balcony"):     [(2, 0.5)],

    # Bathroom
    ("Toilets", None):          [(3, 1.0)],
    ("Tools", "SM_Sponge"):     [(3, 1.0)],

    # Hallway
    ("Misc", "SM_Umbrella"):    [(4, 0.5)],
    ("Boxes", "SM_PropsPacked"):[(4, 0.3), (6, 0.5)],

    # Dining
    ("Tables", None):           [(5, 1.0)],
    ("Chair", None):            [(5, 1.0)],

    # Storage
    ("Boxes", None):            [(6, 1.0)],
    ("Racks", None):            [(6, 1.0), (7, 0.5)],
    ("Cans", None):             [(6, 0.5)],
    ("Carts", None):            [(6, 0.5), (7, 0.5)],
    ("Tools", None):            [(7, 1.0), (6, 0.3)],
    ("Storage", None):          [(6, 1.0), (7, 0.5)],

    # Garage
    ("Tyres", None):            [(7, 1.0)],
    ("Fuel", None):             [(7, 0.5)],
    ("Gas_Bottles", None):      [(7, 0.5)],
    ("WorkBenches", None):      [(7, 0.5)],
    ("Work Equipment", None):   [(7, 0.5)],
    ("Fire Exstinguishers", None):[(7, 0.5)],

    # Utility
    ("FuseBoxes", None):        [(9, 1.0)],
    ("Ventilation", "SM_Radiator"): [(9, 0.5)],
    ("Pipes", None):            [(9, 0.5)],

    # Entry
    ("Misc", "SM_Umbrella"):    [(8, 0.5)],
}

# Exclude these subfolders (exterior/industrial)
SKIP_FOLDERS = {
    "Barricades", "Construction", "Fences", "Fire_Hydrants", "Kiosk",
    "Ladders", "Lockers", "Mailboxes", "Nature", "Phone_Booths",
    "Rails", "Railway", "Road", "Scaffolds", "Screws_bolts",
    "Signs", "Solar Panels", "Storage_Tanks", "Traffic_Cones",
}

# ── Scan all prefabs ──
prefabs = []  # (folder_name, filename_no_ext, full_rel_path, guid)

for folder in sorted(os.listdir(PROPS_ROOT)):
    folder_path = os.path.join(PROPS_ROOT, folder)
    if not os.path.isdir(folder_path):
        continue
    if folder in SKIP_FOLDERS:
        continue

    for f in os.listdir(folder_path):
        if not f.endswith(".prefab"):
            continue
        name = f[:-7]  # strip .prefab
        meta_path = os.path.join(folder_path, f + ".meta")
        guid = ""
        if os.path.exists(meta_path):
            with open(meta_path, "r") as mf:
                for line in mf:
                    if line.startswith("guid:"):
                        guid = line.split(":", 1)[1].strip()
                        break
        rel = os.path.relpath(os.path.join(folder_path, f), BASE).replace("\\", "/")
        prefabs.append((folder, name, rel, guid))

print(f"Found {len(prefabs)} prefabs in {PROPS_ROOT}")

# ── Assign to rooms ──
from collections import defaultdict
room_props = defaultdict(list)  # roomType -> [(rel_path, guid, weight)]

for folder, name, rel, guid in prefabs:
    matched = False
    for (rule_folder, rule_prefix), assignments in ROOM_RULES.items():
        if folder != rule_folder:
            continue
        if rule_prefix is not None and not name.startswith(rule_prefix):
            continue
        for room_type, weight in assignments:
            room_props[room_type].append((name, rel, guid, weight))
        matched = True
    # If no rule matched but folder has explicit rules for other prefixes, skip
    # If no rule at all for this folder, put in Storage as catch-all
    if not matched:
        # Check if any rule targets this folder
        any_rule = any(rf == folder for rf, _ in ROOM_RULES.keys())
        if not any_rule:
            room_props[6].append((name, rel, guid, 0.3))  # Storage catch-all

# ── Room name map ──
ROOM_NAMES = {
    0: "LivingRoom", 1: "Kitchen", 2: "Bedroom", 3: "Bathroom",
    4: "Hallway", 5: "Dining", 6: "Storage", 7: "Garage",
    8: "Entry", 9: "Utility",
}

# Spawn attempts per room type (roughly proportional to room size/density)
SPAWN_ATTEMPTS = {0:6, 1:5, 2:4, 3:3, 4:3, 5:4, 6:6, 7:4, 8:2, 9:2}

# ── Print summary ──
for rt in sorted(room_props.keys()):
    name = ROOM_NAMES.get(rt, str(rt))
    # Deduplicate by guid
    seen = set()
    unique = []
    for pname, rel, guid, w in room_props[rt]:
        if guid not in seen:
            seen.add(guid)
            unique.append(pname)
    print(f"  {name} ({rt}): {len(unique)} props, {SPAWN_ATTEMPTS.get(rt,4)} attempts")
    for p in sorted(unique)[:8]:
        print(f"    - {p}")
    if len(unique) > 8:
        print(f"    ... and {len(unique)-8} more")

# ── Generate YAML ──
# We need a new GUID for the InteriorPropConfig asset itself
import uuid
asset_guid = str(uuid.uuid4()).replace("-", "")[:32]

# Unity YAML for InteriorPropConfig
# The ScriptableObject format:
# %YAML 1.1
# %TAG !u! tag:unity3d.com,2011:
# --- !u!114 &11400000
# MonoBehaviour:
#   ...
# For ScriptableObject, the class ID is 114 and we need the script reference

# Actually, ScriptableObject assets reference their MonoScript via guid+fileID.
# We need the guid of InteriorPropConfig.cs's .meta file.
script_meta = os.path.join(BASE, "Assets", "Editor", "InteriorPropConfig.cs.meta")
script_guid = ""
if os.path.exists(script_meta):
    with open(script_meta, "r") as f:
        for line in f:
            if line.startswith("guid:"):
                script_guid = line.split(":", 1)[1].strip()
                break
print(f"\nScript GUID: {script_guid}")

if not script_guid:
    print("ERROR: Could not find InteriorPropConfig.cs.meta GUID")
    exit(1)

# Build YAML
lines = []
lines.append("%YAML 1.1")
lines.append("%TAG !u! tag:unity3d.com,2011:")
lines.append(f"--- !u!114 &11400000")
lines.append("MonoBehaviour:")
lines.append("  m_ObjectHideFlags: 0")
lines.append("  m_CorrespondingSourceObject: {fileID: 0}")
lines.append("  m_PrefabInstance: {fileID: 0}")
lines.append("  m_PrefabAsset: {fileID: 0}")
lines.append("  m_GameObject: {fileID: 0}")
lines.append("  m_Enabled: 1")
lines.append("  m_EditorHideFlags: 0")
lines.append(f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}")
lines.append("  m_Name: InteriorPropConfig")
lines.append("  m_EditorClassIdentifier: Zombera.Editor::Zombera.Editor.InteriorPropConfig")

# roomEntries array
lines.append("  roomEntries:")
for rt in sorted(room_props.keys()):
    props = room_props[rt]
    # Deduplicate by guid, keep first weight
    seen_guid = {}
    for pname, rel, guid, w in props:
        if guid not in seen_guid:
            seen_guid[guid] = (pname, w)

    attempts = SPAWN_ATTEMPTS.get(rt, 4)

    lines.append(f"  - roomType: {rt}")
    lines.append(f"    spawnAttempts: {attempts}")
    lines.append("    props:")
    for guid, (pname, w) in seen_guid.items():
        lines.append(f"    - prefab: {{fileID: 0, guid: {guid}, type: 3}}")
        lines.append(f"      weight: {w:.1f}")
        lines.append(f"      maxPerRoom: 2")
        lines.append(f"      randomiseYaw: 1")
        lines.append(f"      yOffset: 0")
        lines.append(f"      minWallDistance: 0.3")
        lines.append(f"      minDoorDistance: 0.5")

# fallbackProps (empty for now)
lines.append("  fallbackProps: []")
lines.append("  fallbackSpawnAttempts: 2")

yaml_content = "\n".join(lines) + "\n"

# Write the asset
os.makedirs(os.path.dirname(ASSET_PATH), exist_ok=True)
with open(ASSET_PATH, "w", newline="\n") as f:
    f.write(yaml_content)

# Write .meta for the asset
meta_content = f"""fileFormatVersion: 2
guid: {asset_guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
meta_path = ASSET_PATH + ".meta"
with open(meta_path, "w", newline="\n") as f:
    f.write(meta_content)

print(f"\nWrote: {ASSET_PATH}")
print(f"Asset GUID: {asset_guid}")
print(f"Total room entries: {len(room_props)}")
total_props = sum(len(set(g for _,_,g,_ in v)) for v in room_props.values())
print(f"Total unique props assigned: {total_props}")
