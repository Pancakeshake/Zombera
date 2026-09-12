import subprocess, json, sys

CODE = r'''using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;

namespace Zombera.Editor
{
    internal class CommandScript : IRunCommand
    {
        public void Execute(ExecutionResult result)
        {
            var hw = 1.5f;
            var positions = new Vector3[]
            {
                new(-hw, 0f, 0f), new(hw, 0f, 0f), new(0f, 1.5f, 0f),
                new(-hw, 0f, 0.1f), new(hw, 0f, 0.1f), new(0f, 1.5f, 0.1f)
            };
            var pb = ProBuilderMesh.Create();
            pb.name = "Roof_Gable_1";
            pb.GeometryWithPoints(positions);
            pb.faces = new List<Face>
            {
                new Face(new[] { 0, 2, 1 }), new Face(new[] { 3, 4, 5 }),
                new Face(new[] { 0, 1, 4, 3 }), new Face(new[] { 0, 3, 5, 2 }),
                new Face(new[] { 2, 5, 4, 1 })
            };
            pb.ToMesh(); pb.Refresh(); pb.Refresh(RefreshMask.UV);
            pb.gameObject.AddComponent<Zombera.BuildingSystem.BuildPiece>();
            pb.gameObject.AddComponent<Zombera.BuildingSystem.StructureHealth>();
            var path = "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Roof_Gable_1.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            PrefabUtility.SaveAsPrefabAsset(pb.gameObject, path);
            Object.DestroyImmediate(pb.gameObject);
            result.Log("Created Roof_Gable_1.prefab (3x1.5x0.1m)");
        }
    }
}
'''

PROJECT = r"c:\Zombera"

def main():
    tool_input = {"code": CODE}
    args = [
        "npx", "unity-mcp-cli", "run-tool", "Unity_RunCommand",
        "--path", PROJECT,
        "--input", json.dumps(tool_input),
        "--timeout", "60000"
    ]
    print(f"Running: {' '.join(args[:4])} ...")
    result = subprocess.run(args, capture_output=True, text=True, timeout=90)
    print("STDOUT:", result.stdout)
    if result.stderr:
        print("STDERR:", result.stderr)
    print(f"Exit code: {result.returncode}")

if __name__ == "__main__":
    main()
