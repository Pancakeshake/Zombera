#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zombera.Environment;

public static class Enviro3WeatherSetupTool
{
    private const string EnviroPrefabPath = "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Enviro 3.prefab";
    private const string EnviroConfigPath = "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Profiles/Configurations/Default Enviro Configuration 3_3_2.asset";
    private const string EnviroWeatherTypesFolder = "Assets/03_ThirdParty/Enviro 3 - Sky and Weather/Profiles/Weather Types";

    [MenuItem("Tools/World/Weather/Setup Enviro3 Weather System In Active Scene", priority = -500)]
    public static void SetupEnviroWeatherInActiveScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid())
        {
            EditorUtility.DisplayDialog("No Active Scene", "Open a scene before running Enviro setup.", "OK");
            return;
        }

        var managerType = FindType("Enviro.EnviroManager");
        if (managerType == null)
        {
            EditorUtility.DisplayDialog("Enviro Not Available", "Enviro runtime types are not available to this assembly. Check Enviro install and asmdef references.", "OK");
            return;
        }

        EnsureUrpDefineForActiveBuildTarget();

        var manager = EnsureManagerExists(managerType);
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Enviro Prefab Missing", "Could not find Enviro prefab at:\n" + EnviroPrefabPath, "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo((UnityEngine.Object)manager, "Setup Enviro Weather System");

        EnsureConfiguration(manager);
        EnsurePrimaryCamera(manager);
        EnsureAdditionalCameras(manager);
        EnsureWeatherModuleReady(manager);
        EnsureRuntimeWeatherCycle(manager);

        InvokeMethod(manager, "LoadConfiguration");
        InvokeMethod(manager, "LoadAllModules");

        EditorUtility.SetDirty((UnityEngine.Object)manager);
        var managerGo = ((Component)manager).gameObject;
        EditorSceneManager.MarkSceneDirty(managerGo.scene);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Enviro Weather Setup Complete",
            "Enviro manager, configuration, camera wiring, weather profiles, and runtime weather cycle are now configured in the active scene.",
            "OK");
    }

    [MenuItem("Tools/World/Weather/Report Enviro3 Weather Status", priority = -500)]
    public static void ReportEnviroWeatherStatus()
    {
        var managerType = FindType("Enviro.EnviroManager");
        if (managerType == null)
        {
            Debug.LogWarning("[Enviro Setup] Enviro runtime types are not available to this assembly.");
            return;
        }

        var manager = FindManagerInOpenScenes(managerType);
        if (manager == null)
        {
            Debug.LogWarning("[Enviro Setup] No EnviroManager found in the open scene.");
            return;
        }

        var weather = GetMemberValue(manager, "Weather");
        var weatherSettings = weather != null ? GetMemberValue(weather, "Settings") : null;
        var weatherTypes = weatherSettings != null ? GetMemberValue(weatherSettings, "weatherTypes") as IList : null;
        var weatherCount = weatherTypes != null ? weatherTypes.Count : 0;

        var targetWeather = weather != null ? GetMemberValue(weather, "targetWeatherType") : null;
        var currentWeatherObject = targetWeather as UnityEngine.Object;
        var currentWeather = currentWeatherObject != null ? currentWeatherObject.name : "<none>";

        var cycle = ((Component)manager).GetComponent<EnviroWeatherCycleController>();
        var config = GetMemberValue(manager, "configuration") as UnityEngine.Object;
        var camera = GetMemberValue(manager, "Camera") as Camera;

        var camerasList = GetMemberValue(manager, "Cameras") as IList;
        var additionalCameraCount = camerasList != null ? camerasList.Count : 0;

        Debug.Log(
            "[Enviro Setup] Manager: " + ((Component)manager).name +
            " | Config: " + (config != null ? config.name : "<null>") +
            " | Camera: " + (camera != null ? camera.name : "<null>") +
            " | Additional Cameras: " + additionalCameraCount +
            " | Weather Types: " + weatherCount +
            " | Current Weather: " + currentWeather +
            " | Runtime Cycle: " + (cycle != null ? "Attached" : "Missing"));
    }

    private static object EnsureManagerExists(Type managerType)
    {
        var manager = FindManagerInOpenScenes(managerType);
        if (manager != null)
            return manager;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnviroPrefabPath);
        if (prefab == null)
            return null;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Create Enviro 3");
        instance.name = "Enviro 3";
        return instance.GetComponent(managerType);
    }

    private static object FindManagerInOpenScenes(Type managerType)
    {
        var managers = FindAllObjectsOfType(managerType);
        if (managers != null && managers.Length > 0)
            return managers[0];

        return null;
    }

    private static void EnsureConfiguration(object manager)
    {
        var current = GetMemberValue(manager, "configuration") as UnityEngine.Object;
        if (current != null)
            return;

        var configuration = AssetDatabase.LoadMainAssetAtPath(EnviroConfigPath);
        if (configuration == null)
            return;

        SetMemberValue(manager, "configuration", configuration);
    }

    private static void EnsurePrimaryCamera(object manager)
    {
        var primary = Camera.main;
        if (primary == null)
        {
            primary = FindAllObjectsOfType<Camera>()
                .FirstOrDefault(cam => cam.enabled && cam.cameraType == CameraType.Game);
        }

        if (primary == null)
            primary = CreateFallbackGameplayCamera();

        if (primary != null)
            InvokeMethod(manager, "ChangeCamera", primary);
    }

    private static void EnsureAdditionalCameras(object manager)
    {
        var primary = GetMemberValue(manager, "Camera") as Camera;
        var sceneCameras = FindAllObjectsOfType<Camera>()
            .Where(cam => cam.cameraType == CameraType.Game)
            .ToList();

        for (var i = 0; i < sceneCameras.Count; i++)
        {
            var cam = sceneCameras[i];
            if (cam == null || cam == primary)
                continue;

            InvokeMethod(manager, "AddAdditionalCamera", cam);
        }
    }

    private static void EnsureWeatherModuleReady(object manager)
    {
        var weather = GetMemberValue(manager, "Weather");
        if (weather == null)
        {
            AddWeatherModule(manager);
            weather = GetMemberValue(manager, "Weather");
        }

        if (weather == null)
            return;

        InvokeMethod(weather, "CleanupList");

        var settings = GetMemberValue(weather, "Settings");
        if (settings == null)
            return;

        var weatherTypes = GetMemberValue(settings, "weatherTypes") as IList;
        if (weatherTypes == null)
            return;

        if (weatherTypes.Count == 0)
            AddDefaultWeatherTypes(weatherTypes);

        var targetWeather = GetMemberValue(weather, "targetWeatherType");
        if (targetWeather == null && weatherTypes.Count > 0)
            InvokeMethod(weather, "ChangeWeatherInstant", weatherTypes[0]);

        var defaultZone = GetMemberValue(manager, "defaultZone");
        var currentZone = GetMemberValue(manager, "currentZone");
        if (defaultZone == null && currentZone != null)
            SetMemberValue(manager, "defaultZone", currentZone);
    }

    private static void AddWeatherModule(object manager)
    {
        var moduleTypeEnum = FindType("Enviro.EnviroManagerBase+ModuleType");
        if (moduleTypeEnum == null)
            return;

        var weatherEnumValue = Enum.Parse(moduleTypeEnum, "Weather");
        InvokeMethod(manager, "AddModule", weatherEnumValue);
    }

    private static void AddDefaultWeatherTypes(IList weatherTypes)
    {
        var guids = AssetDatabase.FindAssets("t:EnviroWeatherType", new[] { EnviroWeatherTypesFolder });
        var assets = new List<UnityEngine.Object>();

        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
                assets.Add(asset);
        }

        assets = assets.OrderBy(a => a.name).ToList();

        for (var i = 0; i < assets.Count; i++)
        {
            if (!weatherTypes.Contains(assets[i]))
                weatherTypes.Add(assets[i]);
        }
    }

    private static void EnsureRuntimeWeatherCycle(object manager)
    {
        var managerGo = ((Component)manager).gameObject;
        var cycle = managerGo.GetComponent<EnviroWeatherCycleController>();
        if (cycle == null)
            cycle = Undo.AddComponent<EnviroWeatherCycleController>(managerGo);

        cycle.enabled = true;
        EditorUtility.SetDirty(cycle);
    }

    private static Camera CreateFallbackGameplayCamera()
    {
        var cameraGo = new GameObject("Enviro Runtime Camera");
        Undo.RegisterCreatedObjectUndo(cameraGo, "Create Enviro Runtime Camera");

        if (!UnityEditorInternal.InternalEditorUtility.tags.Contains("MainCamera"))
        {
            // Keep going even if tag setup is non-standard; camera can still render without the tag.
            Debug.LogWarning("[Enviro Setup] MainCamera tag is missing from TagManager. Created fallback camera without tag assignment.");
        }
        else
        {
            cameraGo.tag = "MainCamera";
        }

        var cam = cameraGo.AddComponent<Camera>();
        cam.enabled = true;
        cam.cameraType = CameraType.Game;
        cam.targetDisplay = 0;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 2000f;

        if (cameraGo.GetComponent<AudioListener>() == null)
            cameraGo.AddComponent<AudioListener>();

        return cam;
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
        return GetMemberValue(instance.GetType(), instance, memberName);
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

    private static object InvokeMethod(object instance, string methodName, params object[] args)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var type = instance.GetType();

        var methods = type.GetMethods(flags);
        for (var i = 0; i < methods.Length; i++)
        {
            if (methods[i].Name != methodName)
                continue;

            var parameters = methods[i].GetParameters();
            if (parameters.Length != args.Length)
                continue;

            var compatible = true;
            for (var p = 0; p < parameters.Length; p++)
            {
                if (args[p] == null)
                    continue;

                if (!parameters[p].ParameterType.IsInstanceOfType(args[p]))
                {
                    compatible = false;
                    break;
                }
            }

            if (!compatible)
                continue;

            return methods[i].Invoke(instance, args);
        }

        return null;
    }

    private static void EnsureUrpDefineForActiveBuildTarget()
    {
        if (GraphicsSettings.currentRenderPipeline == null)
            return;

        var pipelineTypeName = GraphicsSettings.currentRenderPipeline.GetType().Name;
        if (!pipelineTypeName.Contains("Universal"))
            return;

        var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        var defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
        if (defines.Contains("ENVIRO_URP"))
            return;

        if (string.IsNullOrWhiteSpace(defines))
            defines = "ENVIRO_URP";
        else
            defines += ";ENVIRO_URP";

        PlayerSettings.SetScriptingDefineSymbols(namedTarget, defines);
        Debug.Log("[Enviro Setup] Added ENVIRO_URP define for active build target.");
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
