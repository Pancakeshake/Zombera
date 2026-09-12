# Batch script: add BuildPiece + StructureHealth to all modular building prefabs
import subprocess
import json
import sys

PREFAB_DIR = "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts"

# All prefabs and their component configs
PREFABS = {
    # Simple: BuildPiece + StructureHealth only
    "Floor.prefab": ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth"],
    "Foundation.prefab": ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth"],
    "Triangle_Floor.prefab": ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth"],
    "Triangle_Foundation.prefab": ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth"],
    "Half_Wall.prefab": ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth"],
    "Stairs.prefab": ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth"],
    "Front_Stairs.prefab": ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth"],
    "Wall.prefab": ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth"],
    "Window.prefab": ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth"],
    # Light/Torch also get the Light component
    "Light.prefab": ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth", "UnityEngine.Light"],
    "Torch.prefab": ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth", "UnityEngine.Light"],
}

def run_tool(tool_name, input_data):
    """Call unity-mcp-cli with JSON input."""
    result = subprocess.run(
        ["npx", "unity-mcp-cli", "run-tool", tool_name, "--input", json.dumps(input_data)],
        capture_output=True, text=True, timeout=30
    )
    if result.returncode != 0:
        print(f"  FAILED: {result.stderr.strip()}")
        return False
    return True

def process_prefab(filename, components):
    asset_path = f"{PREFAB_DIR}/{filename}"
    print(f"\n--- {filename} ---")

    # 1. Open prefab
    print(f"  Opening...")
    if not run_tool("assets-prefab-open", {"gameObjectRef": {"assetPath": asset_path, "instanceID": 0}}):
        return False

    # 2. Add components
    print(f"  Adding: {', '.join(components)}")
    add_input = {
        "componentNames": components,
        "gameObjectRef": {"assetPath": asset_path, "instanceID": 0}
    }
    if not run_tool("gameobject-component-add", add_input):
        # Close even on failure
        run_tool("assets-prefab-close", {"save": True})
        return False

    # 3. Close + save
    print(f"  Saving...")
    if not run_tool("assets-prefab-close", {"save": True}):
        return False

    print(f"  OK")
    return True

# Close any already-open prefab first
run_tool("assets-prefab-close", {"save": True})

success = 0
failed = 0
for filename, components in PREFABS.items():
    if process_prefab(filename, components):
        success += 1
    else:
        failed += 1

print(f"\n=== Done: {success} OK, {failed} failed ===")
sys.exit(0 if failed == 0 else 1)
