using UnityEditor;
using UnityEngine;
using Zombera.BuildingSystem;

namespace Zombera.Editor
{
    /// <summary>
    /// Batch-configures every door prefab in Assets/BuildingSystem/Prefab_Modular
    /// to use DoorScript.Door (Free Wood Door Pack) as the interaction target.
    ///
    /// Per prefab it:
    ///   1. Removes any stale DoorController components
    ///   2. Removes CameraOpenDoor (door pack's own camera raycast — we use DoorInteractor)
    ///   3. Ensures the child named "Door" has DoorScript.Door (adds it if missing)
    ///   4. Ensures DoorScript.Door has an AudioSource (required by [RequireComponent])
    ///
    /// Usage:  Tools → Zombera → Building → Setup Door Prefabs
    /// Idempotent — safe to run multiple times.
    /// </summary>
    public static class DoorPrefabSetupTool
    {
        private const string ModularFolder = "Assets/BuildingSystem/Prefab_Modular";

        [MenuItem("Tools/Zombera/Building/Setup Door Prefabs")]
        private static void RunSetup()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ModularFolder });

            int processed = 0;
            int skipped   = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);

                if (!name.Contains("Door", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                bool changed = false;

                try
                {
                    // ── 1. Remove all stale DoorController components anywhere ──
                    foreach (var dc in root.GetComponentsInChildren<DoorController>(true))
                    {
                        Object.DestroyImmediate(dc);
                        changed = true;
                        Debug.Log($"[DoorSetup] {name}: removed DoorController");
                    }

                    // ── 2. Remove CameraOpenDoor (door pack's own system) ──
                    foreach (var cod in root.GetComponentsInChildren<CameraDoorScript.CameraOpenDoor>(true))
                    {
                        Object.DestroyImmediate(cod);
                        changed = true;
                        Debug.Log($"[DoorSetup] {name}: removed CameraOpenDoor");
                    }

                    // ── 3. Find the "Door" child and ensure DoorScript.Door is on it ──
                    Transform doorChild = FindDeepChild(root.transform, "Door");

                    if (doorChild == null)
                    {
                        Debug.LogWarning($"[DoorSetup] {name}: no child named 'Door' found — skipping DoorScript.Door setup. Add it manually.");
                    }
                    else
                    {
                        // Ensure AudioSource (required by DoorScript.Door's [RequireComponent])
                        if (doorChild.GetComponent<AudioSource>() == null)
                        {
                            doorChild.gameObject.AddComponent<AudioSource>();
                            changed = true;
                        }

                        // Add DoorScript.Door if not already present
                        if (doorChild.GetComponent<DoorScript.Door>() == null)
                        {
                            doorChild.gameObject.AddComponent<DoorScript.Door>();
                            changed = true;
                            Debug.Log($"[DoorSetup] {name}: added DoorScript.Door to '{doorChild.name}'");
                        }
                        else
                        {
                            Debug.Log($"[DoorSetup] {name}: DoorScript.Door already present on '{doorChild.name}'");
                        }

                        // ── 4. Add DoorBlocker (physical passage blocker) ──
                        // IMPORTANT: The BoxCollider MUST be on a static non-rotating node.
                        // Placing it on the rotating Door child would sweep it into the player
                        // during the swing animation and generate explosive PhysX forces.

                        // Remove any stale BoxCollider on the door child from a previous run.
                        foreach (var staleBox in doorChild.GetComponents<BoxCollider>())
                        {
                            Object.DestroyImmediate(staleBox);
                            changed = true;
                            Debug.Log($"[DoorSetup] {name}: removed stale BoxCollider from rotating Door child");
                        }

                        // Find or create a static 'DoorBlockerNode' as a DIRECT child of the root.
                        // Root has no runtime rotation/scale changes, so the collider stays static.
                        Transform blockerNode = FindDeepChild(root.transform, "DoorBlockerNode");
                        if (blockerNode == null)
                        {
                            var go = new GameObject("DoorBlockerNode");
                            go.transform.SetParent(root.transform, worldPositionStays: false);
                            blockerNode = go.transform;
                            // Position approximates the door centre in wall-local space.
                            // X ~ -0.4 (left-of-centre in 2m wall), Y=1.0 (midpoint of 2m door),
                            // Z=0 (flush with wall face). Tune in Inspector if needed per prefab.
                            blockerNode.localPosition = new Vector3(-0.4f, 1.0f, 0f);
                            blockerNode.localRotation = Quaternion.identity;
                            blockerNode.localScale    = Vector3.one;
                            changed = true;
                        }

                        // CRITICAL: DoorBlockerNode must NEVER be Navigation Static.
                        // If it is, the NavMesh bake treats it as a permanent solid obstacle
                        // and the agent can never path through the doorway — even when open.
                        var currentFlags = GameObjectUtility.GetStaticEditorFlags(blockerNode.gameObject);
#pragma warning disable CS0618 // NavigationStatic is deprecated but still needs explicit cleanup for legacy-authored prefabs.
                        if ((currentFlags & StaticEditorFlags.NavigationStatic) != 0)
                        {
                            GameObjectUtility.SetStaticEditorFlags(blockerNode.gameObject,
                                currentFlags & ~StaticEditorFlags.NavigationStatic);
                            changed = true;
                            Debug.Log($"[DoorSetup] {name}: removed NavigationStatic from DoorBlockerNode");
                        }
                        // Also clear ContributeGI and OccluderStatic — not needed on a runtime toggle.
                        var cleanFlags = GameObjectUtility.GetStaticEditorFlags(blockerNode.gameObject)
                                         & ~StaticEditorFlags.ContributeGI
                                         & ~StaticEditorFlags.OccluderStatic
                                         & ~StaticEditorFlags.OccludeeStatic;
#pragma warning restore CS0618
                        if (cleanFlags != GameObjectUtility.GetStaticEditorFlags(blockerNode.gameObject))
                        {
                            GameObjectUtility.SetStaticEditorFlags(blockerNode.gameObject, cleanFlags);
                            changed = true;
                        }

                        // Replace any stale BoxCollider on the blocker node with a NavMeshObstacle.
                        // NavMeshObstacle (carve mode) dynamically digs a hole when the door is
                        // closed and fills it back when open — agents walk through naturally.
                        foreach (var staleBox in blockerNode.GetComponents<BoxCollider>())
                        {
                            Object.DestroyImmediate(staleBox);
                            changed = true;
                            Debug.Log($"[DoorSetup] {name}: removed BoxCollider from DoorBlockerNode (replaced by NavMeshObstacle)");
                        }

                        var obstacle = blockerNode.GetComponent<UnityEngine.AI.NavMeshObstacle>();
                        if (obstacle == null)
                        {
                            obstacle = blockerNode.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                            changed = true;
                            Debug.Log($"[DoorSetup] {name}: added NavMeshObstacle to DoorBlockerNode");
                        }

                        // Shape matches a standard door opening: 0.9 m wide × 2 m tall × 0.25 m thick.
                        obstacle.shape  = UnityEngine.AI.NavMeshObstacleShape.Box;
                        obstacle.center = Vector3.zero;
                        obstacle.size   = new Vector3(0.9f, 2.0f, 0.25f);
                        obstacle.carving = true;
                        obstacle.carveOnlyStationary = false;
                        // Start disabled — door starts closed but the BoxBlocker on DoorBlocker
                        // will enable/disable this at runtime. Enable here so closed doors block.
                        obstacle.enabled = true;
                        changed = true;

                        // Add DoorBlocker to the door leaf child and wire the obstacle.
                        var db = doorChild.GetComponent<DoorBlocker>();
                        if (db == null)
                        {
                            db = doorChild.gameObject.AddComponent<DoorBlocker>();
                            changed = true;
                            Debug.Log($"[DoorSetup] {name}: added DoorBlocker to '{doorChild.name}'");
                        }

                        // Wire obstacle reference via SerializedObject so it survives prefab save.
                        var so = new SerializedObject(db);
                        var prop = so.FindProperty("_obstacle");
                        if (prop != null && prop.objectReferenceValue != obstacle)
                        {
                            prop.objectReferenceValue = obstacle;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        processed++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = $"Done.\n\nConfigured: {processed}\nAlready correct (skipped): {skipped}\n\nCheck Console for per-prefab details.";
            Debug.Log($"[DoorSetup] {msg}");
            EditorUtility.DisplayDialog("Door Prefab Setup", msg, "OK");
        }

        // ── Fix DoorBlockerNode Navigation Static (scene instances) ──────────

        /// <summary>
        /// Finds every DoorBlockerNode in the currently open scene and strips
        /// NavigationStatic from it. Run this after Setup Door Prefabs, then rebake
        /// the NavMesh so the doorway is navigable.
        /// </summary>
        [MenuItem("Tools/Zombera/Building/Fix DoorBlockerNode Navigation Static (Scene)")]
        private static void FixSceneBlockerNavStatic()
        {
            int fixed_ = 0;

            // Find all GameObjects named DoorBlockerNode in the active scene
            var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (!string.Equals(t.gameObject.name, "DoorBlockerNode", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
#pragma warning disable CS0618 // NavigationStatic is deprecated but still needs explicit cleanup for legacy-authored scene objects.
                var cleaned = flags
                    & ~StaticEditorFlags.NavigationStatic
                    & ~StaticEditorFlags.ContributeGI
                    & ~StaticEditorFlags.OccluderStatic
                    & ~StaticEditorFlags.OccludeeStatic;
#pragma warning restore CS0618

                if (cleaned != flags)
                {
                    Undo.RecordObject(t.gameObject, "Fix DoorBlockerNode NavStatic");
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, cleaned);
                    fixed_++;
                    Debug.Log($"[DoorSetup] Fixed NavigationStatic on DoorBlockerNode (scene path: {GetPath(t)})");
                }
            }

            string msg = fixed_ > 0
                ? $"Fixed {fixed_} DoorBlockerNode(s).\n\nNow rebake your NavMesh so the doorway is walkable."
                : "No DoorBlockerNode objects needed fixing.";
            Debug.Log($"[DoorSetup] {msg}");
            EditorUtility.DisplayDialog("Fix DoorBlockerNode NavStatic", msg, "OK");
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        // ── Strip child colliders ─────────────────────────────────────────────
        [MenuItem("Tools/Zombera/Building/Strip Child Colliders from Door Prefabs")]
        private static void StripChildColliders()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ModularFolder });

            int prefabsChanged = 0;
            int collidersRemoved = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);

                if (!name.Contains("Door", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                bool changed = false;

                try
                {
                    // Remove colliders from every child — leave only the root's collider
                    // (the baked wall MeshCollider already covers the whole piece)
                    for (int i = 0; i < root.transform.childCount; i++)
                    {
                        Transform child = root.transform.GetChild(i);
                        int removed = RemoveCollidersDeep(child);
                        if (removed > 0)
                        {
                            collidersRemoved += removed;
                            changed = true;
                            Debug.Log($"[DoorSetup] {name}: removed {removed} collider(s) from child '{child.name}'");
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabsChanged++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = $"Done.\n\nPrefabs modified: {prefabsChanged}\nColliders removed: {collidersRemoved}";
            Debug.Log($"[DoorSetup] {msg}");
            EditorUtility.DisplayDialog("Strip Child Colliders", msg, "OK");
        }

        /// <summary>Removes all Collider components on this transform and all its descendants.
        /// Returns the number removed.</summary>
        private static int RemoveCollidersDeep(Transform t)
        {
            int count = 0;
            foreach (var col in t.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                Object.DestroyImmediate(col);
                count++;
            }
            return count;
        }

        // ── Fix SM_DoorWall_A Physics Colliders ───────────────────────────────

        /// <summary>
        /// Replaces the solid MeshCollider on SM_DoorWall_A with three BoxColliders
        /// (left panel, right panel, header) that leave a 1.1m-wide doorway gap.
        ///
        /// Why 1.1m: NavMesh erosion eats 2 × AgentRadius from every opening edge.
        /// With AgentRadius=0.5 the minimum clear width is 1.0m — 1.1m adds a small
        /// safety margin so the bake always produces a walkable channel.
        ///
        /// Wall coordinate convention (from building-system baseline):
        ///   X = wall width (2 m, pivot at X=0 centre)
        ///   Y = wall height (2.5 m, pivot at Y=0 floor)
        ///   Z = wall thickness (0.2 m)
        ///
        /// After running this tool:
        ///   1. NavMesh Surface → Use Geometry: Physics Colliders
        ///   2. Bake
        ///
        /// Idempotent — safe to run multiple times.
        /// </summary>
        [MenuItem("Tools/Zombera/Building/Fix SM_DoorWall_A Physics Colliders")]
        private static void FixDoorWallPhysicsColliders()
        {
            const string prefabPath = "Assets/BuildingSystem/Prefab_Modular/SM_DoorWall_A.prefab";

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Error", $"Prefab not found at:\n{prefabPath}", "OK");
                return;
            }

            try
            {
                // 1. Disable the solid MeshCollider so it does not block baking.
                foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                {
                    mc.enabled = false;
                    Debug.Log($"[DoorWallFix] Disabled MeshCollider on '{mc.gameObject.name}'");
                }

                // 2. Remove stale wall panel BoxColliders from previous runs (idempotent).
                foreach (string n in new[] { "WallPanel_Left", "WallPanel_Right", "WallPanel_Header" })
                {
                    Transform old = FindDeepChild(root.transform, n);
                    if (old != null)
                    {
                        Object.DestroyImmediate(old.gameObject);
                        Debug.Log($"[DoorWallFix] Removed stale '{n}'");
                    }
                }

                // 3. Build BoxColliders around the door opening.
                //
                //  Wall:  X [-1 .. +1]  (2 m wide)
                //         Y [ 0 .. 2.5] (2.5 m tall)
                //         Z [-0.1..+0.1](0.2 m thick)
                //
                //  Door opening (1.1 m wide, centred on X=0, 2.1 m tall):
                //         X [-0.55 .. +0.55]
                //         Y [  0   ..  2.1 ]
                //
                //  Panels (left and right ONLY — header intentionally omitted):
                //    Left  : X [-1.0 .. -0.55]  w=0.45  cX=-0.775
                //    Right : X [+0.55.. +1.0]   w=0.45  cX=+0.775
                //
                //  The header strip (Y=2.1-2.5) is NOT added. A box at that height
                //  gives only 2.1m clearance at the threshold. With agent height=2m
                //  the voxeliser rounds this down and marks the doorway non-walkable,
                //  creating an isolated interior patch. Agent height handles vertical
                //  clearance — side panels alone are sufficient for lateral boundaries.

                const float wallThicknessZ = 0.2f;
                const float wallHalfX      = 1.0f;   // half of 2 m
                const float wallTopY       = 2.5f;
                const float doorHalfX      = 0.55f;  // half of 1.1 m opening

                AddWallPanel(root.transform, "WallPanel_Left",
                    localPos: new Vector3(-(wallHalfX + doorHalfX) * 0.5f, wallTopY * 0.5f, 0f),
                    size:     new Vector3(wallHalfX - doorHalfX,            wallTopY,        wallThicknessZ));

                AddWallPanel(root.transform, "WallPanel_Right",
                    localPos: new Vector3(+(wallHalfX + doorHalfX) * 0.5f, wallTopY * 0.5f, 0f),
                    size:     new Vector3(wallHalfX - doorHalfX,            wallTopY,        wallThicknessZ));

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            string msg =
                "Done!\n\n" +
                "• MeshCollider disabled\n" +
                "• WallPanel_Left / WallPanel_Right added (header omitted — avoids clearance failure)\n" +
                "• Door gap = 1.1 m (agent diameter 1.0 m + 0.1 m margin)\n\n" +
                "Next steps:\n" +
                "1. NavMesh Surface → Use Geometry: Physics Colliders\n" +
                "2. Bake\n\n" +
                "If the gap does not line up with the visual door opening,\n" +
                "select WallPanel_Left / WallPanel_Right in the prefab and\n" +
                "drag them in X to match.";

            Debug.Log($"[DoorWallFix] {msg}");
            EditorUtility.DisplayDialog("Fix SM_DoorWall_A Physics Colliders", msg, "OK");
        }

        private static void AddWallPanel(Transform parent, string panelName, Vector3 localPos, Vector3 size)
        {
            var go = new GameObject(panelName);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;

            var box = go.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size   = size;

            Debug.Log($"[DoorWallFix] Added '{panelName}' pos={localPos} size={size}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Depth-first search for a child by name (case-insensitive).</summary>
        private static Transform FindDeepChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
                    return child;
                Transform found = FindDeepChild(child, childName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
