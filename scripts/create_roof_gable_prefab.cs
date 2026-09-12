using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.Shapes;

namespace Zombera.Editor
{
    /// <summary>
    /// Creates a fresh ProBuilder Roof_Gable prefab.
    /// Run via Unity MCP: Unity_RunCommand with this script.
    /// Then strip ProBuilder with: Tools → Build → Mod Kits → Strip ProBuilder From Roof Kit
    /// </summary>
    internal class CreateRoofGablePrefab : IRunCommand
    {
        // ── Tune these to match your desired default gable size ──
        private const float GableWidth  = 3f;   // matches CellSize (one cell wide)
        private const float GableHeight = 1.5f; // peak height (base to peak)
        private const float GableDepth  = 0.1f; // thickness

        private const string OutputPath =
            "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Roof_Gable.prefab";

        public void Execute(ExecutionResult result)
        {
            // 1. Create the ProBuilder triangle shape
            var go = ShapeGenerator.CreateShape<Door>(ShapeType.Cube);
            // ProBuilder doesn't have a built-in Triangle ShapeType, so we use
            // a Plane and extrude, or a Cube and delete vertices.
            // Simpler: create a Cube, then taper it into a triangle.

            // Alternative: build the gable mesh procedurally via ProBuilderMesh API
            var mesh = ProBuilderMesh.Create();
            mesh.name = "Roof_Gable";

            // Triangle vertices for a gable (facing Z, in X-Y plane):
            //  (0, 0, 0) bottom-left
            //  (w, 0, 0) bottom-right
            //  (w/2, h, 0) peak
            // Duplicate at depth for thickness
            var hw = GableWidth * 0.5f;
            var verts = new Vector3[]
            {
                new(-hw, 0f,        0f),            // 0: bottom-left front
                new( hw, 0f,        0f),            // 1: bottom-right front
                new(0f,  GableHeight, 0f),          // 2: peak front
                new(-hw, 0f,        GableDepth),    // 3: bottom-left back
                new( hw, 0f,        GableDepth),    // 4: bottom-right back
                new(0f,  GableHeight, GableDepth),  // 5: peak back
            };

            var faces = new Face[]
            {
                // Front triangle (0,1,2) — normal faces -Z
                new Face(new[] { 0, 1, 2 }, null, null, null, -1),
                // Back triangle (5,4,3) — normal faces +Z
                new Face(new[] { 5, 4, 3 }, null, null, null, -1),
                // Bottom edge (0,3,4,1)
                new Face(new[] { 0, 3, 4, 1 }, null, null, null, -1),
                // Left edge (0,2,5,3)
                new Face(new[] { 0, 2, 5, 3 }, null, null, null, -1),
                // Right edge (1,4,5,2)
                new Face(new[] { 1, 4, 5, 2 }, null, null, null, -1),
            };

            mesh.RebuildWithPositionsAndFaces(verts, faces);

            // Auto UV unwrap
            mesh.ToMesh();
            mesh.Refresh();
            mesh.ToMesh();
            mesh.Refresh();

            // 2. Create the GameObject hierarchy
            var root = new GameObject("Roof_Gable");
            var pbGo = mesh.gameObject;
            pbGo.transform.SetParent(root.transform, false);
            pbGo.name = "Gable_Mesh";

            // Move MeshFilter + MeshRenderer up to root if needed
            // (ProBuilderMesh creates them on its own GameObject)

            // 3. Add required components
            var buildPiece = root.AddComponent<Zombera.BuildingSystem.BuildPiece>();
            var structureHealth = root.AddComponent<Zombera.BuildingSystem.StructureHealth>();

            // 4. Save as prefab
            // Backup existing
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OutputPath) != null)
            {
                var backupPath = OutputPath.Replace(".prefab", "_backup.prefab");
                AssetDatabase.CopyAsset(OutputPath, backupPath);
                AssetDatabase.DeleteAsset(OutputPath);
                result.Log("Backed up existing to {0}", backupPath);
            }

            PrefabUtility.SaveAsPrefabAsset(root, OutputPath);

            // 5. Cleanup
            Object.DestroyImmediate(root);

            result.RegisterObjectCreation(root);
            result.Log("Created Roof_Gable.prefab at {0} ({1:F1}x{2:F1}x{3:F1})",
                OutputPath, GableWidth, GableHeight, GableDepth);
            result.Log("Next: Tools → Build → Mod Kits → Strip ProBuilder From Roof Kit");
        }
    }
}
