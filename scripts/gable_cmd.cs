using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;

public static class Script
{
    public static void Main()
    {
        var hw = 1.5f;
        var positions = new Vector3[]
        {
            new(-hw, 0f, 0f), new(hw, 0f, 0f), new(0f, 1.5f, 0f),
            new(-hw, 0f, 0.1f), new(hw, 0f, 0.1f), new(0f, 1.5f, 0.1f)
        };

        var pb = ProBuilderMesh.Create();
        pb.name = "Roof_Gable_1";

        var verts = new List<Vertex>();
        for (int i = 0; i < positions.Length; i++)
            verts.Add(new Vertex { position = positions[i] });
        pb.SetVertices(verts);

        pb.faces = new List<Face>
        {
            // Front triangle
            new Face(new[] { 0, 2, 1 }),
            // Back triangle
            new Face(new[] { 3, 4, 5 }),
            // Bottom: quad (0,1,4,3) → two triangles
            new Face(new[] { 0, 1, 4 }),
            new Face(new[] { 0, 4, 3 }),
            // Left slope: quad (0,3,5,2) → two triangles
            new Face(new[] { 0, 3, 5 }),
            new Face(new[] { 0, 5, 2 }),
            // Right slope: quad (2,5,4,1) → two triangles
            new Face(new[] { 2, 5, 4 }),
            new Face(new[] { 2, 4, 1 }),
        };
        pb.ToMesh();
        pb.Refresh();
        pb.Refresh(RefreshMask.UV);

        pb.gameObject.AddComponent<Zombera.BuildingSystem.BuildPiece>();
        pb.gameObject.AddComponent<Zombera.BuildingSystem.StructureHealth>();

        var path = "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Roof_Gable_1.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);

        PrefabUtility.SaveAsPrefabAsset(pb.gameObject, path);
        Object.DestroyImmediate(pb.gameObject);

        Debug.Log("Created Roof_Gable_1.prefab (3x1.5x0.1m)");
    }
}
