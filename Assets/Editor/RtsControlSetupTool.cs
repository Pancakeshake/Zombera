#region

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Zombera.Systems;

#endregion

namespace Zombera.Editor
{
    /// <summary>
    ///     Idempotent active-scene setup for RTS selection and command routing.
    /// </summary>
    public static class RtsControlSetupTool
    {
        private const string MenuPath = "Tools/World/RTS/Setup RTS Control Stack In Active Scene";
        private const string RootObjectName = "RTS Control Stack";

        private static Type s_inputSystemUiModuleType;
        private static bool s_resolvedInputSystemUiModuleType;

        [MenuItem(MenuPath, priority = -500)]
        private static void SetupRtsControlStackInActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[RtsControlSetupTool] No loaded active scene to configure.");
                return;
            }

            var root = GameObject.Find(RootObjectName);
            if (root == null)
            {
                root = new GameObject(RootObjectName);
                Undo.RegisterCreatedObjectUndo(root, "Create RTS Control Stack");
            }

            var eventSystem = EnsureEventSystem();
            var selectionManager = EnsureComponent<SelectionManager>(root);
            var commandManager = EnsureComponent<CommandManager>(root);
            var selectionBoxUi = EnsureComponent<SelectionBoxUI>(root);

            var worldCamera = Camera.main;
            var squadManager = SquadManager.Instance != null
                ? SquadManager.Instance
                : UnityEngine.Object.FindFirstObjectByType<SquadManager>();

            WireSelectionManager(selectionManager, worldCamera, squadManager, selectionBoxUi);
            WireCommandManager(commandManager, worldCamera, squadManager);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log("[RtsControlSetupTool] RTS control stack ready: " +
                      "SelectionManager + CommandManager + SelectionBoxUI configured on '" +
                      RootObjectName + "'. EventSystem: " +
                      (eventSystem != null ? eventSystem.gameObject.name : "<none>"));
        }

        private static EventSystem EnsureEventSystem()
        {
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                EnsureInputModule(eventSystem.gameObject);
                return eventSystem;
            }

            var eventSystemGo = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");

            eventSystem = eventSystemGo.AddComponent<EventSystem>();
            EnsureInputModule(eventSystemGo);

            return eventSystem;
        }

        private static void EnsureInputModule(GameObject eventSystemGo)
        {
            if (eventSystemGo == null) return;
            if (eventSystemGo.GetComponent<BaseInputModule>() != null) return;

            var inputSystemUiModuleType = GetInputSystemUiInputModuleType();
            if (inputSystemUiModuleType != null && typeof(BaseInputModule).IsAssignableFrom(inputSystemUiModuleType))
            {
                _ = Undo.AddComponent(eventSystemGo, inputSystemUiModuleType);
                return;
            }

            _ = Undo.AddComponent<StandaloneInputModule>(eventSystemGo);
        }

        private static Type GetInputSystemUiInputModuleType()
        {
            if (s_resolvedInputSystemUiModuleType) return s_inputSystemUiModuleType;

            s_resolvedInputSystemUiModuleType = true;
            s_inputSystemUiModuleType =
                Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            return s_inputSystemUiModuleType;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            if (target == null) return null;

            var existing = target.GetComponent<T>();
            if (existing != null) return existing;

            return Undo.AddComponent<T>(target);
        }

        private static void WireSelectionManager(SelectionManager manager, Camera worldCamera, SquadManager squadManager,
            SelectionBoxUI selectionBoxUi)
        {
            if (manager == null) return;

            var serialized = new SerializedObject(manager);
            SetObjectReference(serialized, "worldCamera", worldCamera);
            SetObjectReference(serialized, "squadManager", squadManager);
            SetObjectReference(serialized, "selectionBoxUi", selectionBoxUi);
            SetBool(serialized, "overrideLegacyPlayerMouseInput", true);
            SetBool(serialized, "autoResolveLegacyPlayerInputControllers", true);
            SetBool(serialized, "hybridSingleCharacterAndRtsMode", true);
            SetBool(serialized, "autoSwitchToRtsWhenMultipleSelected", true);
            SetBool(serialized, "requireRtsModifierForMouseCapture", false);
            SetBool(serialized, "useAltAsRtsModifierFallback", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(manager);
        }

        private static void WireCommandManager(CommandManager manager, Camera worldCamera, SquadManager squadManager)
        {
            if (manager == null) return;

            var serialized = new SerializedObject(manager);
            SetObjectReference(serialized, "worldCamera", worldCamera);
            SetObjectReference(serialized, "squadManager", squadManager);
            SetBool(serialized, "hybridSingleCharacterAndRtsMode", true);
            SetBool(serialized, "autoSwitchToRtsWhenMultipleSelected", true);
            SetBool(serialized, "requireRtsModifierForCommandMouseInput", false);
            SetBool(serialized, "useAltAsRtsModifierFallback", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(manager);
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName,
            UnityEngine.Object referenceValue)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = referenceValue;
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null) property.boolValue = value;
        }
    }
}