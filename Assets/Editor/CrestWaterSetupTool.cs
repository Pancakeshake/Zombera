#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CrestWaterSetupTool
{
    private const string OceanMaterialPath = "Assets/02_Shared/Materials/Water/ZomberaOcean-Underwater.mat";

    [MenuItem("Tools/World/Water/Setup Crest Water System In Active Scene", priority = -500)]
    public static void SetupCrestWaterInActiveScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid())
        {
            EditorUtility.DisplayDialog("No Active Scene", "Open a scene before running Crest setup.", "OK");
            return;
        }

        var oceanRendererType = FindType("Crest.OceanRenderer");
        if (oceanRendererType == null)
        {
            EditorUtility.DisplayDialog("Crest Not Available", "Crest runtime types are not available to this assembly. Check Crest install and asmdef references.", "OK");
            return;
        }

        var oceanRenderer = EnsureOceanRendererExists(oceanRendererType);
        if (oceanRenderer == null)
        {
            EditorUtility.DisplayDialog("Crest Setup Failed", "Could not create or locate a Crest OceanRenderer in the active scene.", "OK");
            return;
        }

        var oceanComponent = (Component)oceanRenderer;
        Undo.RegisterCompleteObjectUndo(oceanComponent.gameObject, "Setup Crest Water System");

        EnsureOceanRendererConfiguration(oceanRenderer);
        var disabledOpaqueDownsamplingCount = DisableUrpOpaqueDownsampling();
        EnsureWaveShape(oceanComponent.transform);
        EnsureUnderwaterRenderer();
        EnsureWaterBody(oceanComponent.transform.position.y);

        EditorUtility.SetDirty(oceanComponent.gameObject);
        EditorSceneManager.MarkSceneDirty(oceanComponent.gameObject.scene);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Crest Setup Complete",
            "Crest ocean renderer, wave shape input, underwater renderer, and a default water body are configured in the active scene. " +
            "Disabled URP Opaque Downsampling on " + disabledOpaqueDownsamplingCount + " pipeline asset(s).",
            "OK");
    }

    [MenuItem("Tools/World/Water/Report Crest Water Status", priority = -500)]
    public static void ReportCrestWaterStatus()
    {
        var oceanRendererType = FindType("Crest.OceanRenderer");
        if (oceanRendererType == null)
        {
            Debug.LogWarning("[Crest Setup] Crest runtime types are not available to this assembly.");
            return;
        }

        var oceanRenderer = FindFirstComponentInOpenScenes(oceanRendererType);
        if (oceanRenderer == null)
        {
            Debug.LogWarning("[Crest Setup] No OceanRenderer found in loaded scenes.");
            return;
        }

        var oceanComponent = (Component)oceanRenderer;
        var oceanMaterial = GetMemberValue(oceanRenderer, "_material") as Material;
        var oceanCamera = GetMemberValue(oceanRenderer, "_camera") as Camera;
        var oceanPrimaryLight = GetMemberValue(oceanRenderer, "_primaryLight") as Light;
        var seaLevel = GetMemberValue(oceanRenderer, "SeaLevel") is float level ? level : oceanComponent.transform.position.y;

        var shapeType = FindType("Crest.ShapeGerstner") ?? FindType("Crest.ShapeGerstnerBatched");
        var shapeCount = shapeType != null ? CountComponentsInOpenScenes(shapeType) : 0;

        var underwaterType = FindType("Crest.UnderwaterRenderer");
        var underwaterCount = underwaterType != null ? CountComponentsInOpenScenes(underwaterType) : 0;

        var waterBodyType = FindType("Crest.WaterBody");
        var waterBodyCount = waterBodyType != null ? CountComponentsInOpenScenes(waterBodyType) : 0;

        Debug.Log(
            "[Crest Setup] Ocean: " + oceanComponent.name +
            " | Ocean Material: " + (oceanMaterial != null ? oceanMaterial.name : "<null>") +
            " | View Camera: " + (oceanCamera != null ? oceanCamera.name : "<null>") +
            " | Primary Light: " + (oceanPrimaryLight != null ? oceanPrimaryLight.name : "<null>") +
            " | Sea Level: " + seaLevel.ToString("0.##") +
            " | Wave Shapes: " + shapeCount +
            " | Underwater Renderers: " + underwaterCount +
            " | Water Bodies: " + waterBodyCount,
            oceanComponent.gameObject);
    }

    private static object EnsureOceanRendererExists(Type oceanRendererType)
    {
        var existing = FindFirstComponentInOpenScenes(oceanRendererType);
        if (existing != null)
            return existing;

        var go = new GameObject("Crest Ocean");
        Undo.RegisterCreatedObjectUndo(go, "Create Crest Ocean");
        return Undo.AddComponent(go, oceanRendererType);
    }

    private static void EnsureOceanRendererConfiguration(object oceanRenderer)
    {
        var viewCamera = ResolvePrimaryCamera();
        if (viewCamera == null)
            viewCamera = CreateFallbackGameplayCamera();

        if (viewCamera != null)
            SetMemberValue(oceanRenderer, "_camera", viewCamera);

        var directionalLight = ResolvePrimaryDirectionalLight();
        if (directionalLight != null)
            SetMemberValue(oceanRenderer, "_primaryLight", directionalLight);

        if (GetMemberValue(oceanRenderer, "_material") == null)
        {
            var oceanMaterial = AssetDatabase.LoadAssetAtPath<Material>(OceanMaterialPath);
            if (oceanMaterial != null)
                SetMemberValue(oceanRenderer, "_material", oceanMaterial);
            else
                Debug.LogWarning("[Crest Setup] Could not find default ocean material at: " + OceanMaterialPath);
        }
    }

    private static void EnsureWaveShape(Transform oceanTransform)
    {
        var shapeType = FindType("Crest.ShapeGerstner") ?? FindType("Crest.ShapeGerstnerBatched");
        if (shapeType == null)
        {
            Debug.LogWarning("[Crest Setup] Could not find Crest wave shape type (ShapeGerstner / ShapeGerstnerBatched).");
            return;
        }

        var existing = oceanTransform.GetComponentsInChildren(shapeType, true);
        if (existing != null && existing.Length > 0)
            return;

        var shapeGo = new GameObject("Crest Wave Shape");
        Undo.RegisterCreatedObjectUndo(shapeGo, "Create Crest Wave Shape");
        shapeGo.transform.SetParent(oceanTransform, false);
        Undo.AddComponent(shapeGo, shapeType);
    }

    private static void EnsureUnderwaterRenderer()
    {
        var underwaterType = FindType("Crest.UnderwaterRenderer");
        if (underwaterType == null)
            return;

        var existing = FindFirstComponentInOpenScenes(underwaterType);
        if (existing != null)
            return;

        var cam = ResolvePrimaryCamera();
        if (cam == null)
            cam = CreateFallbackGameplayCamera();

        if (cam == null)
            return;

        if (cam.GetComponent(underwaterType) == null)
            Undo.AddComponent(cam.gameObject, underwaterType);
    }

    private static void EnsureWaterBody(float seaLevel)
    {
        var waterBodyType = FindType("Crest.WaterBody");
        if (waterBodyType == null)
            return;

        if (CountComponentsInOpenScenes(waterBodyType) > 0)
            return;

        var waterBodyGo = new GameObject("Crest Water Body");
        Undo.RegisterCreatedObjectUndo(waterBodyGo, "Create Crest Water Body");
        waterBodyGo.transform.position = new Vector3(0f, seaLevel, 0f);
        waterBodyGo.transform.rotation = Quaternion.identity;
        waterBodyGo.transform.localScale = new Vector3(2000f, 1f, 2000f);
        Undo.AddComponent(waterBodyGo, waterBodyType);
    }

    private static Camera ResolvePrimaryCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        var sceneCamera = FindAllObjectsOfType<Camera>()
            .FirstOrDefault(cam => cam != null && cam.enabled && cam.cameraType == CameraType.Game);

        return sceneCamera;
    }

    private static Light ResolvePrimaryDirectionalLight()
    {
        if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional)
            return RenderSettings.sun;

        return FindAllObjectsOfType<Light>()
            .Where(light => light != null && light.enabled && light.type == LightType.Directional)
            .OrderByDescending(light => light.intensity)
            .FirstOrDefault();
    }

    private static Camera CreateFallbackGameplayCamera()
    {
        var cameraGo = new GameObject("Crest Runtime Camera");
        Undo.RegisterCreatedObjectUndo(cameraGo, "Create Crest Runtime Camera");

        if (UnityEditorInternal.InternalEditorUtility.tags.Contains("MainCamera"))
            cameraGo.tag = "MainCamera";

        var cam = cameraGo.AddComponent<Camera>();
        cam.enabled = true;
        cam.cameraType = CameraType.Game;
        cam.targetDisplay = 0;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 3000f;

        if (cameraGo.GetComponent<AudioListener>() == null)
            cameraGo.AddComponent<AudioListener>();

        return cam;
    }

    private static int DisableUrpOpaqueDownsampling()
    {
        var changedCount = 0;

        var candidates = QualitySettings.names
            .Select(name => QualitySettings.GetRenderPipelineAssetAt(QualitySettings.names.ToList().IndexOf(name)))
            .Where(asset => asset != null)
            .ToList();

        if (GraphicsSettings.currentRenderPipeline != null)
            candidates.Add(GraphicsSettings.currentRenderPipeline);

        if (GraphicsSettings.defaultRenderPipeline != null)
            candidates.Add(GraphicsSettings.defaultRenderPipeline);

        foreach (var pipelineAsset in candidates.Distinct())
        {
            if (pipelineAsset == null) continue;

            var serialized = new SerializedObject(pipelineAsset);
            var property = serialized.FindProperty("m_OpaqueDownsampling");
            if (property == null || property.propertyType != SerializedPropertyType.Enum) continue;

            if (property.enumValueIndex == 0) continue;

            property.enumValueIndex = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipelineAsset);
            changedCount++;
        }

        if (changedCount > 0) AssetDatabase.SaveAssets();

        return changedCount;
    }

    private static int CountComponentsInOpenScenes(Type type)
    {
        var all = FindAllObjectsOfType(type);
        var count = 0;

        for (var i = 0; i < all.Length; i++)
        {
            if (all[i] is not Component component)
                continue;

            if (!component.gameObject.scene.IsValid() || !component.gameObject.scene.isLoaded)
                continue;

            if (EditorUtility.IsPersistent(component))
                continue;

            count++;
        }

        return count;
    }

    private static object FindFirstComponentInOpenScenes(Type type)
    {
        var all = FindAllObjectsOfType(type);

        for (var i = 0; i < all.Length; i++)
        {
            if (all[i] is not Component component)
                continue;

            if (!component.gameObject.scene.IsValid() || !component.gameObject.scene.isLoaded)
                continue;

            if (EditorUtility.IsPersistent(component))
                continue;

            return component;
        }

        return null;
    }

    private static Type FindType(string fullName)
    {
        var type = Type.GetType(fullName);
        if (type != null)
            return type;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            type = assemblies[i].GetType(fullName);
            if (type != null)
                return type;
        }

        return null;
    }

    private static object GetMemberValue(object instance, string memberName)
    {
        return instance == null ? null : GetMemberValue(instance.GetType(), instance, memberName);
    }

    private static object GetMemberValue(Type type, object instance, string memberName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        var field = type.GetField(memberName, flags);
        if (field != null)
            return field.GetValue(instance);

        var property = type.GetProperty(memberName, flags);
        return property != null ? property.GetValue(instance) : null;
    }

    private static void SetMemberValue(object instance, string memberName, object value)
    {
        if (instance == null)
            return;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var type = instance.GetType();

        var field = type.GetField(memberName, flags);
        if (field != null)
        {
            field.SetValue(instance, value);
            return;
        }

        var property = type.GetProperty(memberName, flags);
        if (property != null && property.CanWrite)
            property.SetValue(instance, value);
    }

    private static UnityEngine.Object[] FindAllObjectsOfType(Type type)
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType(type, true);
#endif
    }

    private static T[] FindAllObjectsOfType<T>() where T : UnityEngine.Object
    {
#if UNITY_2022_2_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
    }
}
#endif
