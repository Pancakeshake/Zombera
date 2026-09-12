#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.BaseBuilding;
using Zombera.BuildingSystem;

public static class PlayerBaseBuildSetupTool
{
    private const string MenuPath = "Tools/World/Buildings/Setup Player Base Build (Selected)";
    private const string RadialMenuPrefabPath = "Assets/03_ThirdParty/Mind Code Interactive/Easy Build System/Framework/Resources/Building Menus/UI_BuildingRadialMenu.prefab";
    private const string CatalogMenuPrefabPath = "Assets/03_ThirdParty/Mind Code Interactive/Easy Build System/Framework/Resources/Building Menus/UI_BuildingCatalogMenu.prefab";

    private const string BuildingManagerTypeName = "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Managers.BuildingManager";
    private const string BuildingControllerTypeName = "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.Systems.Controllers.BuildingController";
    private const string BuildingRadialMenuTypeName = "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.UI.BuildingMenus.Implementations.BuildingRadialMenuUI";
    private const string BuildingCatalogMenuTypeName = "MindCodeInteractive.EasyBuildSystem.Framework.Code.Runtime.UI.BuildingMenus.Implementations.BuildingCatalogMenuUI";

    [MenuItem(MenuPath, priority = -500)]
    public static void SetupSelectedPlayerBaseBuild()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Select a player GameObject in the Hierarchy, then run setup.", "OK");
            return;
        }

        var summary = ConfigurePlayerRoot(selected);
        if (!summary.success)
        {
            EditorUtility.DisplayDialog("Player Base Build Setup", summary.message, "OK");
            return;
        }

        EditorUtility.DisplayDialog("Player Base Build Setup Complete", summary.message, "OK");
    }

    [MenuItem(MenuPath, true, priority = -500)]
    public static bool ValidateSetupSelectedPlayerBaseBuild()
    {
        return Selection.activeGameObject != null;
    }

    private static SetupSummary ConfigurePlayerRoot(GameObject playerRoot)
    {
        if (playerRoot == null)
            return SetupSummary.Failed("Selected object is null.");

        Undo.RegisterFullObjectHierarchyUndo(playerRoot, "Setup Player Base Build");

        var scene = playerRoot.scene;
        if (scene.IsValid())
            EditorSceneManager.SetActiveScene(scene);

        var buildingController = EnsureEasyBuildController(playerRoot);
        if (buildingController == null)
            return SetupSummary.Failed("Could not add Easy Build System BuildingController to selected player.");

        var radialBridge = playerRoot.GetComponent<EasyBuildRadialMenuInputBridge>();
        if (radialBridge == null)
            radialBridge = Undo.AddComponent<EasyBuildRadialMenuInputBridge>(playerRoot);

        var cursorBinder = playerRoot.GetComponent<EasyBuildCursorPlacementBinder>();
        if (cursorBinder == null)
            cursorBinder = Undo.AddComponent<EasyBuildCursorPlacementBinder>(playerRoot);

        var buildingManager = EnsureEasyBuildManager(scene);
        var radialMenu = EnsureMenuInstance(scene, BuildingRadialMenuTypeName, RadialMenuPrefabPath);
        var catalogMenu = EnsureMenuInstance(scene, BuildingCatalogMenuTypeName, CatalogMenuPrefabPath);

        var removedLegacy = RemoveLegacyBaseBuildScripts(playerRoot);

        EditorUtility.SetDirty(radialBridge);
        if (cursorBinder != null) EditorUtility.SetDirty(cursorBinder);
        EditorUtility.SetDirty(playerRoot);

        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);

        var message =
            "Configured selected player for Easy Build System radial workflow.\n" +
            "BuildingController: " + (buildingController != null ? "ready" : "missing") + "\n" +
            "BuildingManager: " + (buildingManager != null ? "ready" : "missing") + "\n" +
            "Radial menu prefab instance: " + (radialMenu != null ? "ready" : "missing") + "\n" +
            "Catalog menu prefab instance: " + (catalogMenu != null ? "ready" : "missing") + "\n" +
            "Legacy build scripts removed: " + removedLegacy;

        return SetupSummary.Succeeded(message);
    }

    private static Component EnsureEasyBuildController(GameObject playerRoot)
    {
        var controllerType = FindType(BuildingControllerTypeName);
        if (controllerType == null)
            return null;

        var controller = playerRoot.GetComponent(controllerType);
        if (controller == null)
            controller = Undo.AddComponent(playerRoot, controllerType);

        if (controller == null)
            return null;

        InvokeMethod(controller, "Reset");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static Component EnsureEasyBuildManager(Scene scene)
    {
        var managerType = FindType(BuildingManagerTypeName);
        if (managerType == null)
            return null;

        var existing = FindFirstSceneObjectOfType(scene, managerType);
        if (existing != null)
            return existing;

        var host = new GameObject("Easy Build Manager");
        Undo.RegisterCreatedObjectUndo(host, "Create Easy Build Manager");
        if (scene.IsValid())
            SceneManager.MoveGameObjectToScene(host, scene);

        var manager = host.AddComponent(managerType);
        EditorUtility.SetDirty(host);
        if (manager != null)
            EditorUtility.SetDirty(manager);

        return manager;
    }

    private static GameObject EnsureMenuInstance(Scene scene, string menuTypeName, string prefabPath)
    {
        var menuType = FindType(menuTypeName);
        if (menuType == null)
            return null;

        var existing = FindFirstSceneObjectOfType(scene, menuType);
        if (existing != null)
            return existing.gameObject;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return null;

        var instantiated = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instantiated == null)
            return null;

        Undo.RegisterCreatedObjectUndo(instantiated, "Instantiate Easy Build Menu Prefab");
        EditorUtility.SetDirty(instantiated);
        return instantiated;
    }

    private static Component FindFirstSceneObjectOfType(Scene scene, Type targetType)
    {
        var all = Resources.FindObjectsOfTypeAll(targetType);
        for (var i = 0; i < all.Length; i++)
        {
            if (all[i] is not Component component)
                continue;

            if (!component.gameObject.scene.IsValid())
                continue;

            if (scene.IsValid() && component.gameObject.scene != scene)
                continue;

            return component;
        }

        return null;
    }

    private static int RemoveLegacyBaseBuildScripts(GameObject playerRoot)
    {
        var removed = 0;

        removed += DestroyComponents(playerRoot.GetComponentsInChildren<BuildPlacementController>(true));
        removed += DestroyComponents(playerRoot.GetComponentsInChildren<BuildGhostPreview>(true));
        removed += DestroyComponents(playerRoot.GetComponentsInChildren<Blueprint>(true));
        removed += DestroyComponents(playerRoot.GetComponentsInChildren<ConstructionJob>(true));
        removed += DestroyComponents(playerRoot.GetComponentsInChildren<WorkerAI>(true));
        removed += DestroyComponents(playerRoot.GetComponentsInChildren<BuildManager>(true));

        if (removed > 0)
            EditorUtility.SetDirty(playerRoot);

        return removed;
    }

    private static int DestroyComponents<T>(IReadOnlyList<T> components) where T : Behaviour
    {
        var removed = 0;

        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i];
            if (component == null)
                continue;

            Undo.DestroyObjectImmediate(component);
            removed++;
        }

        return removed;
    }

    private static Type FindType(string fullName)
    {
        var type = Type.GetType(fullName);
        if (type != null) return type;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            type = assemblies[i].GetType(fullName);
            if (type != null) return type;
        }

        return null;
    }

    private static void InvokeMethod(object instance, string methodName)
    {
        if (instance == null) return;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var method = instance.GetType().GetMethod(methodName, flags);
        method?.Invoke(instance, null);
    }

    private readonly struct SetupSummary
    {
        public readonly bool success;
        public readonly string message;

        private SetupSummary(bool success, string message)
        {
            this.success = success;
            this.message = message;
        }

        public static SetupSummary Failed(string message)
        {
            return new SetupSummary(false, message);
        }

        public static SetupSummary Succeeded(string message)
        {
            return new SetupSummary(true, message);
        }
    }
}
#endif
