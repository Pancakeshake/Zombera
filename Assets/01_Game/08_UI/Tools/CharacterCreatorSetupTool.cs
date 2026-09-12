#if UNITY_EDITOR
#region

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Zombera.Characters;
using Zombera.UI.Menus;
using Zombera.UI.Menus.CharacterCreation;

#endregion

namespace Zombera.Editor
{
    public static class CharacterCreatorSetupTool
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string PreviewRoomName = "CharacterPreviewRoom";
        private const string RenderTexturePath = "Assets/Art/UI/RenderTextures/RT_CharacterPreview.renderTexture";

        [MenuItem("Tools/Items/Character Creation/Setup Character Creator in MainMenu", priority = -500)]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var currentScene = EditorSceneManager.GetActiveScene().path;
            if (currentScene != MainMenuScenePath)
            {
                EditorSceneManager.OpenScene(MainMenuScenePath);
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Setup Character Creator");
            var undoGroup = Undo.GetCurrentGroup();

            // 1. Setup Preview Room
            var room = GameObject.Find(PreviewRoomName);
            if (room != null) Object.DestroyImmediate(room);
            
            room = new GameObject(PreviewRoomName);
            Undo.RegisterCreatedObjectUndo(room, "Create Preview Room");
            room.transform.position = new Vector3(500f, 0f, 0f);

            // 2. Setup Avatar
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError($"[CharacterCreatorSetupTool] Player prefab not found at {PlayerPrefabPath}");
                return;
            }

            var avatar = PrefabUtility.InstantiatePrefab(playerPrefab, room.transform) as GameObject;
            avatar.name = "Player_Preview";
            avatar.transform.localPosition = Vector3.zero;
            avatar.transform.localRotation = Quaternion.Euler(0, 180, 0);

            // 3. Setup Camera
            var camGo = new GameObject("Preview_Camera");
            camGo.transform.SetParent(room.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.2f, 2.5f);
            camGo.transform.localRotation = Quaternion.Euler(5f, 180f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);
            cam.fieldOfView = 35f;

            // 4. Setup Light
            var lightGo = new GameObject("Preview_Light");
            lightGo.transform.SetParent(room.transform, false);
            lightGo.transform.localPosition = new Vector3(1f, 2f, 2f);
            lightGo.transform.localRotation = Quaternion.Euler(30f, 210f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;

            // 5. Setup RenderTexture
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (rt == null)
            {
                var dir = Path.GetDirectoryName(RenderTexturePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                
                rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
                AssetDatabase.CreateAsset(rt, RenderTexturePath);
                AssetDatabase.SaveAssets();
            }
            cam.targetTexture = rt;

            // 6. Wire to Controller
            var controller = Object.FindFirstObjectByType<CharacterCreatorController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[CharacterCreatorSetupTool] CharacterCreatorController not found in scene.");
                return;
            }

            Undo.RecordObject(controller, "Wire Controller");
            var creatorRefs = controller.GetComponent<CharacterCreatorRefs>();
            if (creatorRefs == null) creatorRefs = controller.GetComponentInChildren<CharacterCreatorRefs>(true);
            
            if (creatorRefs != null)
            {
                Undo.RecordObject(creatorRefs, "Wire Refs");
                creatorRefs.previewAvatar = avatar;
                if (creatorRefs.previewDisplay != null)
                {
                    creatorRefs.previewDisplay.texture = rt;
                }
                
                if (creatorRefs.customizationController != null)
                {
                    Undo.RecordObject(creatorRefs.customizationController, "Wire Customization");
                    // Assuming the customization controller has a previewCamera field based on plan
                    var so = new SerializedObject(creatorRefs.customizationController);
                    var camProp = so.FindProperty("previewCamera");
                    if (camProp != null)
                    {
                        camProp.objectReferenceValue = cam;
                        so.ApplyModifiedProperties();
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            
            Debug.Log("<color=green>[CharacterCreatorSetupTool] Successfully set up character creator in MainMenu scene.</color>");
        }
    }
}
#endif
