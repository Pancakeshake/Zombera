#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;
using Zombera.BuildingSystem;
using Zombera.Debugging.DebugTools;
using Zombera.Inventory;
using Zombera.Inventory.Crafting;
using Zombera.Systems;
using Zombera.UI;
using Zombera.World;
using Zombera.World.CityPipeline.WorldBuilder;
using Zombera.World.CityPipeline.WorldBuilder.State;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.Editor.SystemDev
{
    /// <summary>
    /// One-shot builder for system-dev labs 5–13. Menu: Zombera/System Dev/Build Labs 5-13.
    /// </summary>
    public static class SystemDevLabBatchBuilder
    {
        private const string Root = "Assets/00_Scenes/02_System Dev Scenes";
        private const string GmPrefab = "Assets/02_Shared/Prefabs/Systems/GameManager/[GameManager].prefab";
        private const string PlayerPrefab = "Assets/Player.prefab";
        private const string ZombiePrefab = "Assets/02_Shared/Prefabs/Systems/Zombies/Zombie Type1.prefab";
        private const string DebugMenuPrefab = "Assets/01_Game/02_World/Debugging/DebugMenu/DebugMenuUI.prefab";
        private const string ProfileAsset = "Assets/02_Shared/ScriptableObjects/World/WorldGenerationProfile.asset";

        [MenuItem("Zombera/System Dev/Build Labs 5-13")]
        public static void BuildAll()
        {
            BuildFlatLab("5_Squad_RTS", WireSquadRts);
            BuildFlatLab("6_Inventory_Loot", WireInventoryLoot);
            BuildFlatLab("7_Crafting_Bench", WireCraftingBench);
            BuildFlatLab("8_Base_Building", WireBaseBuilding);
            BuildFlatLab("9_Zombie_AI", WireZombieAi);
            BuildFlatLab("10_Interaction_World", WireInteractionWorld);
            BuildFlatLab("11_Save_Load", WireSaveLoad);
            BuildFlatLab("12_HUD_Gameplay", WireHudGameplay);
            BuildTerrainNavMeshLab();
            Debug.Log("[SystemDevLabBatchBuilder] Labs 5-13 complete.");
        }

        private static void BuildFlatLab(string folderName, Action<LabRoots> wire)
        {
            var scenePath = $"{Root}/{folderName}/{folderName}.unity";
            EnsureFolder($"{Root}/{folderName}");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var roots = CreateFlatShell();
            PlacePlayer(roots);
            PlaceDebug(roots);
            wire?.Invoke(roots);
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[SystemDevLabBatchBuilder] Saved {scenePath}");
        }

        private static void BuildTerrainNavMeshLab()
        {
            const string folderName = "13_Terrain_NavMesh";
            var scenePath = $"{Root}/{folderName}/{folderName}.unity";
            EnsureFolder($"{Root}/{folderName}");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var systems = new GameObject("[SYSTEMS]");
            var content = new GameObject("Content");
            var debug = new GameObject("Debug");
            new GameObject("_LabNote_StreamingNavMesh_NoSurface_NoMapMagic")
                .transform.SetParent(systems.transform, false);

            var gmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GmPrefab);
            if (gmPrefab != null)
            {
                var gm = (GameObject)PrefabUtility.InstantiatePrefab(gmPrefab);
                gm.name = "[GameManager]";
                gm.transform.SetParent(systems.transform, false);
            }

            var worldManagerGo = new GameObject("WorldManager");
            worldManagerGo.transform.SetParent(systems.transform, false);
            var worldManager = worldManagerGo.AddComponent<WorldManager>();

            var worldBuilderGo = new GameObject("WorldBuilder");
            worldBuilderGo.transform.SetParent(systems.transform, false);
            var service = worldBuilderGo.AddComponent<WorldBuilderService>();
            var catalog = worldBuilderGo.AddComponent<WorldTileCatalog>();
            var state = worldBuilderGo.AddComponent<WorldStateManager>();

            var navGo = new GameObject("StreamingNavMeshTileService");
            navGo.transform.SetParent(systems.transform, false);
            var nav = navGo.AddComponent<StreamingNavMeshTileService>();

            var serviceSo = new SerializedObject(service);
            var profile = AssetDatabase.LoadAssetAtPath<WorldGenerationProfile>(ProfileAsset);
            if (profile == null)
            {
                var guids = AssetDatabase.FindAssets("t:WorldGenerationProfile");
                if (guids.Length > 0)
                    profile = AssetDatabase.LoadAssetAtPath<WorldGenerationProfile>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            serviceSo.FindProperty("_profile").objectReferenceValue = profile;
            serviceSo.FindProperty("_tileCatalog").objectReferenceValue = catalog;
            serviceSo.FindProperty("_stateManager").objectReferenceValue = state;
            serviceSo.ApplyModifiedPropertiesWithoutUndo();

            var wmSo = new SerializedObject(worldManager);
            SetBool(wmSo, "useProceduralStreamingWorld", true);
            SetBool(wmSo, "enableEasyRoadsRoadBridge", false);
            wmSo.FindProperty("tileStreamBridge").objectReferenceValue = catalog;
            wmSo.FindProperty("_worldGenerationBackendSource").objectReferenceValue = service;
            wmSo.FindProperty("navMeshTileService").objectReferenceValue = nav;
            wmSo.ApplyModifiedPropertiesWithoutUndo();

            PlaceDebug(new LabRoots
            {
                Systems = systems.transform,
                Content = content.transform,
                Debug = debug.transform
            });

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (playerPrefab != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.name = "Player";
                player.transform.SetParent(content.transform, false);
                player.transform.position = new Vector3(0f, 1f, 0f);
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[SystemDevLabBatchBuilder] Saved {scenePath} profile={(profile != null)}");
        }

        private static LabRoots CreateFlatShell()
        {
            var env = new GameObject("Environment");
            var spawns = new GameObject("Spawns");
            var systems = new GameObject("[SYSTEMS]");
            var content = new GameObject("Content");
            var debug = new GameObject("Debug");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(env.transform, false);
            ground.transform.localScale = new Vector3(5f, 1f, 5f);

            CreateWall(env.transform, "Wall_N", new Vector3(0, 1, 25), new Vector3(50, 2, 0.5f));
            CreateWall(env.transform, "Wall_S", new Vector3(0, 1, -25), new Vector3(50, 2, 0.5f));
            CreateWall(env.transform, "Wall_E", new Vector3(25, 1, 0), new Vector3(0.5f, 2, 50));
            CreateWall(env.transform, "Wall_W", new Vector3(-25, 1, 0), new Vector3(0.5f, 2, 50));

            CreateMarker(spawns.transform, "PlayerSpawn", new Vector3(0, 0.05f, -12), new Color(0.2f, 0.7f, 1f));

            var gmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GmPrefab);
            if (gmPrefab != null)
            {
                var gm = (GameObject)PrefabUtility.InstantiatePrefab(gmPrefab);
                gm.name = "[GameManager]";
                gm.transform.SetParent(systems.transform, false);
            }
            else
            {
                new GameObject("_Missing_GameManager_prefab").transform.SetParent(systems.transform, false);
            }

            new GameObject("_LabNote_Flat_NoStreaming_NoMapMagic_NoEasyRoads")
                .transform.SetParent(systems.transform, false);

            var navMesh = new GameObject("NavMesh");
            var surface = navMesh.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;

            return new LabRoots
            {
                Environment = env.transform,
                Spawns = spawns.transform,
                Systems = systems.transform,
                Content = content.transform,
                Debug = debug.transform,
                NavMesh = navMesh.transform
            };
        }

        private static void PlacePlayer(LabRoots roots)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var spawn = roots.Spawns != null ? roots.Spawns.Find("PlayerSpawn") : null;
            if (playerPrefab == null) return;
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player";
            player.transform.SetParent(roots.Content, false);
            if (spawn != null)
                player.transform.position = spawn.position + Vector3.up * 0.1f;
        }

        private static void PlaceDebug(LabRoots roots)
        {
            if (roots.Debug == null) return;
            var menuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DebugMenuPrefab);
            if (menuPrefab != null)
            {
                var menu = (GameObject)PrefabUtility.InstantiatePrefab(menuPrefab);
                menu.name = "DebugMenuUI";
                menu.transform.SetParent(roots.Debug, false);
            }

            var tools = new GameObject("SpawnDebugTools");
            tools.transform.SetParent(roots.Debug, false);
            tools.AddComponent<SpawnDebugTools>();
        }

        private static void WireSquadRts(LabRoots roots)
        {
            var rts = new GameObject("RTS");
            rts.transform.SetParent(roots.Systems, false);
            rts.AddComponent<SelectionManager>();
            rts.AddComponent<CommandManager>();

            for (var i = 1; i <= 4; i++)
            {
                var ally = CreateMarker(roots.Content, $"AllyMarker_{i:00}",
                    new Vector3(-6f + i * 3f, 0.05f, -6f), new Color(0.2f, 0.9f, 0.4f));
                ally.transform.localScale = new Vector3(1.2f, 0.2f, 1.2f);
            }

            CreateMarker(roots.Spawns, "SquadRally", new Vector3(0, 0.05f, 0), new Color(0.9f, 0.9f, 0.2f));
        }

        private static void WireInventoryLoot(LabRoots roots)
        {
            TryInstantiatePrefab(
                "Assets/02_Shared/Prefabs/Systems/UI/Menus/SquadTabs/InventoryMenu.prefab",
                "InventoryMenu", roots.Content);

            for (var i = 1; i <= 3; i++)
            {
                var crate = CreateMarker(roots.Content, $"LootCrate_{i:00}",
                    new Vector3(-4f + i * 4f, 0.5f, 6f), new Color(0.85f, 0.55f, 0.15f));
                crate.transform.localScale = new Vector3(1.5f, 1f, 1.5f);
                crate.AddComponent<LootContainer>();
            }
        }

        private static void WireCraftingBench(LabRoots roots)
        {
            var crafting = new GameObject("CraftingService");
            crafting.transform.SetParent(roots.Systems, false);
            crafting.AddComponent<CraftingService>();

            TryInstantiatePrefab(
                "Assets/02_Shared/Prefabs/Systems/UI/Menus/SquadTabs/CraftingMenu.prefab",
                "CraftingMenu", roots.Content);

            TryInstantiatePrefab(
                "Assets/02_Shared/Prefabs/Props/WorkBenches/WorkbenchTable_URP Variant.prefab",
                "Workbench", roots.Content, new Vector3(0f, 0f, 4f));
        }

        private static void WireBaseBuilding(LabRoots roots)
        {
            var ground = roots.Environment != null ? roots.Environment.Find("Ground") : null;
            if (ground != null) ground.localScale = new Vector3(8f, 1f, 8f);

            var binder = new GameObject("EasyBuildCursorPlacementBinder");
            binder.transform.SetParent(roots.Systems, false);
            binder.AddComponent<EasyBuildCursorPlacementBinder>();

            CreateMarker(roots.Spawns, "BuildOrigin", new Vector3(0, 0.05f, 0), new Color(0.6f, 0.4f, 1f));
            new GameObject("_LabNote_EasyBuild_BuildingManager_via_package")
                .transform.SetParent(roots.Systems, false);
        }

        private static void WireZombieAi(LabRoots roots)
        {
            var zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefab);
            if (zombiePrefab == null) return;

            for (var i = 1; i <= 8; i++)
            {
                var angle = (i / 8f) * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(angle) * 14f, 0.05f, Mathf.Sin(angle) * 14f);
                CreateMarker(roots.Spawns, $"ZombieSpawn_{i:00}", pos, new Color(0.9f, 0.2f, 0.2f));
                var z = (GameObject)PrefabUtility.InstantiatePrefab(zombiePrefab);
                z.name = $"Zombie_{i:00}";
                z.transform.SetParent(roots.Content, false);
                z.transform.position = pos + Vector3.up * 0.1f;
            }
        }

        private static void WireInteractionWorld(LabRoots roots)
        {
            TryInstantiatePrefab(
                "Assets/02_Shared/Prefabs/Props/Lockers/DoorBigL_URP Variant.prefab",
                "DoorBigL", roots.Content, new Vector3(-6f, 0f, 4f));
            TryInstantiatePrefab(
                "Assets/02_Shared/Prefabs/Props/Lockers/DoorBigR_URP Variant.prefab",
                "DoorBigR", roots.Content, new Vector3(-3f, 0f, 4f));
            TryInstantiatePrefab(
                "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts/Doorway.prefab",
                "Doorway", roots.Content, new Vector3(2f, 0f, 4f));

            for (var i = 1; i <= 2; i++)
            {
                var crate = CreateMarker(roots.Content, $"InteractCrate_{i:00}",
                    new Vector3(6f + i * 2f, 0.5f, 4f), new Color(0.5f, 0.7f, 0.9f));
                crate.AddComponent<LootContainer>();
            }
        }

        private static void WireSaveLoad(LabRoots roots)
        {
            TryInstantiatePrefab(
                "Assets/01_Game/08_UI/Prefabs/LoadSavePanel_Modern.prefab",
                "LoadSavePanel_Modern", roots.Content);
            new GameObject("_LabNote_SaveSystem_on_GameManager")
                .transform.SetParent(roots.Systems, false);
        }

        private static void WireHudGameplay(LabRoots roots)
        {
            var hud = new GameObject("WorldHUD");
            hud.transform.SetParent(roots.Content, false);
            hud.AddComponent<WorldHUDController>();

            TryInstantiatePrefab(
                "Assets/02_Shared/Prefabs/Systems/UI/Menus/SquadTabs/InventoryMenu.prefab",
                "InventoryMenu", roots.Content);
            TryInstantiatePrefab(
                "Assets/02_Shared/Prefabs/Systems/UI/Menus/SquadTabs/CraftingMenu.prefab",
                "CraftingMenu", roots.Content);
        }

        private static void TryInstantiatePrefab(string path, string name, Transform parent, Vector3? pos = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                new GameObject($"_Missing_{name}").transform.SetParent(parent, false);
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (pos.HasValue) go.transform.position = pos.Value;
        }

        private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
        }

        private static GameObject CreateMarker(Transform parent, string name, Vector3 pos, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.position = pos;
            marker.transform.localScale = new Vector3(0.8f, 0.1f, 0.8f);
            var renderer = marker.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    var mat = new Material(shader) { color = color };
                    renderer.sharedMaterial = mat;
                }
            }

            return marker;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetBool(SerializedObject so, string propertyName, bool value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null) prop.boolValue = value;
        }

        private sealed class LabRoots
        {
            public Transform Environment;
            public Transform Spawns;
            public Transform Systems;
            public Transform Content;
            public Transform Debug;
            public Transform NavMesh;
        }
    }
}
#endif
