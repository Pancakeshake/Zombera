import subprocess, json

PREFABS = ["Roof_Ridge.prefab", "Roof_Panel.prefab", "Roof_Gable.prefab"]
BASE = "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts"
COMPS = ["Zombera.BuildingSystem.BuildPiece", "Zombera.BuildingSystem.StructureHealth"]

def run(tool, input_data):
    subprocess.run(["npx", "unity-mcp-cli", "run-tool", tool, "--input", json.dumps(input_data)],
                   capture_output=True, timeout=30)

for p in PREFABS:
    path = f"{BASE}/{p}"
    print(f"--- {p} ---")
    run("assets-prefab-open", {"gameObjectRef": {"assetPath": path, "instanceID": 0}})
    run("gameobject-component-add", {"componentNames": COMPS, "gameObjectRef": {"assetPath": path, "instanceID": 0}})
    run("assets-prefab-close", {"save": True})
    print("  OK")
print("Done")
