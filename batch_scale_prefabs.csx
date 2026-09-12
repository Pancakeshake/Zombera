var basePath = "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts";
var results = new System.Text.StringBuilder();

void ScaleAndSave(string name, bool scaleZ) {
    var path = basePath + "/" + name + ".prefab";
    var contents = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    if (contents == null) { results.AppendLine(name + ": FAILED load"); return; }
    var s = contents.transform.localScale;
    contents.transform.localScale = new Vector3(1.017f, s.y, scaleZ ? 1.017f : s.z);
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(contents, path);
    UnityEditor.PrefabUtility.UnloadPrefabContents(contents);
    results.AppendLine(string.Format("{0}: X {1:F3}->1.017 Z {2:F3}->{3:F3}", name, s.x, s.z, scaleZ ? 1.017f : s.z));
}

ScaleAndSave("Floor", true);
ScaleAndSave("Foundation", true);
ScaleAndSave("Window", false);
ScaleAndSave("Doorway", false);
ScaleAndSave("Half_Wall", false);
ScaleAndSave("Triangle_Floor", true);
ScaleAndSave("Triangle_Foundation", true);
ScaleAndSave("Roof_Panel", false);
ScaleAndSave("Roof_Ridge", false);
ScaleAndSave("Front_Stairs", false);
ScaleAndSave("Stairs", false);

Debug.Log(results.ToString());
