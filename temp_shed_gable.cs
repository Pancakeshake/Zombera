using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // Create a right-angle triangle: vertices at (0,0,0), (0,1,0), (1,0,0)
        // 1m tall leg along Y, 1m wide leg along X — XY plane triangle
        var mesh = ProBuilderMesh.Create(
            new Vector3[] {
                new Vector3(0f, 0f, 0f),  // right angle
                new Vector3(0f, 1f, 0f),  // top of vertical leg
                new Vector3(1f, 0f, 0f),  // end of horizontal leg
            },
            new Face[] {
                new Face(new int[] { 0, 1, 2 })
            }
        );

        mesh.ToMesh();
        mesh.Refresh();

        // Save as mesh asset
        var savePath = "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Meshes/Shed_Gable.asset";
        AssetDatabase.CreateAsset(mesh.mesh, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        result.Log("Created Shed_Gable mesh at {0}", savePath);
        result.RegisterObjectCreation(mesh.gameObject);

        // Clean up the scene GO, keep the asset
        Object.DestroyImmediate(mesh.gameObject);
    }
}
