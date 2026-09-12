#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Custom inspector for <see cref="Zombera.Data.RoomFloorSkinConfig"/>.
    ///     Provides dropdowns for room type names (from RoomSettings) and filtered
    ///     material pickers (from BuildingSkinTable floor material pools).
    /// </summary>
    [CustomEditor(typeof(Zombera.Data.RoomFloorSkinConfig))]
    public sealed class RoomFloorSkinConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty _buildingSkinTable;
        private SerializedProperty _roomSettings;
        private SerializedProperty _entries;
        private SerializedProperty _fallback;
        private SerializedProperty _wallPaintFallback;
        private SerializedProperty _ceilingFallback;

        private Vector2 _entriesScroll;
        private Vector2 _fallbackScroll;

        private void OnEnable()
        {
            _buildingSkinTable = serializedObject.FindProperty("buildingSkinTable");
            _roomSettings = serializedObject.FindProperty("roomSettings");
            _entries = serializedObject.FindProperty("entries");
            _fallback = serializedObject.FindProperty("fallback");
            _wallPaintFallback = serializedObject.FindProperty("wallPaintFallback");
            _ceilingFallback = serializedObject.FindProperty("ceilingFallback");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var config = (Zombera.Data.RoomFloorSkinConfig)target;

            // ── References ──────────────────────────────────────────────
            EditorGUILayout.PropertyField(_buildingSkinTable, new GUIContent("Building Skin Table",
                "The BuildingSkinTable this config draws floor materials from. " +
                "Materials assigned in entries are constrained to this table's floor material pools."));
            EditorGUILayout.PropertyField(_roomSettings, new GUIContent("Room Settings",
                "Reference to RoomSettings — constrains available room types. " +
                "Entries with room types outside this set are ignored."));

            if (config.buildingSkinTable == null && config.roomSettings == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Building Skin Table and/or a Room Settings asset to enable filtered dropdowns for entries.",
                    MessageType.Info);
            }

            // ── Collect constraints ─────────────────────────────────────
            var validTypeNames = CollectValidRoomTypeNames(config.roomSettings as RoomSettings);
            var floorMaterialPool = CollectFloorMaterialsFromTable(config.buildingSkinTable);
            var wallPaintMaterialPool = CollectWallPaintMaterialsFromTable(config.buildingSkinTable);
            var ceilingMaterialPool = CollectCeilingMaterialsFromTable(config.buildingSkinTable);

            EditorGUILayout.Space(8f);

            // ── Entries ─────────────────────────────────────────────────
            EditorGUILayout.LabelField("Room Entries", EditorStyles.boldLabel);

            if (validTypeNames.Count == 0)
            {
                if (config.roomSettings != null)
                    EditorGUILayout.HelpBox(
                        "The assigned RoomSettings has no RoomTypes defined. Add RoomType entries to RoomSettings.",
                        MessageType.Warning);
                else
                    EditorGUILayout.HelpBox(
                        "Assign a RoomSettings asset above to see available room types.",
                        MessageType.Info);
            }
            else
            {
                // Sync entries with available room types (add missing, remove stale).
                SyncRoomFloorEntries(config, validTypeNames);

                EditorGUI.indentLevel++;
                _entriesScroll = EditorGUILayout.BeginScrollView(_entriesScroll, GUILayout.MaxHeight(400f));

                for (var i = 0; i < _entries.arraySize; i++)
                {
                    DrawRoomFloorEntry(_entries.GetArrayElementAtIndex(i), validTypeNames,
                        floorMaterialPool, wallPaintMaterialPool, ceilingMaterialPool);
                    EditorGUILayout.Space(2f);
                }

                EditorGUILayout.EndScrollView();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8f);

            // ── Fallback ────────────────────────────────────────────────
            EditorGUILayout.LabelField("Fallback", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Floor", EditorStyles.miniBoldLabel);
            DrawWeightedMaterialArray(_fallback, floorMaterialPool);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4f);

            // ── Wall Paint Fallback ────────────────────────────────────
            EditorGUILayout.LabelField("Wall Paint Fallback", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawWeightedMaterialArray(_wallPaintFallback, wallPaintMaterialPool);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4f);

            // ── Ceiling Fallback ───────────────────────────────────────
            EditorGUILayout.LabelField("Ceiling Fallback", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawWeightedMaterialArray(_ceilingFallback, ceilingMaterialPool);
            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }

        // ── Room-type helpers ────────────────────────────────────────────

        /// <summary>
        ///     Collects room type names from a <see cref="RoomSettings"/> asset.
        /// </summary>
        private static List<string> CollectValidRoomTypeNames(RoomSettings rs)
        {
            var result = new List<string>();
            if (rs == null || rs.RoomTypes == null) return result;
            foreach (var rt in rs.RoomTypes)
                result.Add(rt.Type.ToString());
            return result;
        }

        /// <summary>
        ///     Adds entries for room types that are missing from the config,
        ///     and removes entries for room types that no longer exist in RoomSettings.
        /// </summary>
        private static void SyncRoomFloorEntries(
            Zombera.Data.RoomFloorSkinConfig config,
            List<string> validTypeNames)
        {
            var entries = config.entries ?? System.Array.Empty<Zombera.Data.RoomFloorEntry>();
            var entryList = new List<Zombera.Data.RoomFloorEntry>(entries);

            // Remove entries whose type name no longer exists.
            entryList.RemoveAll(e => !validTypeNames.Contains(e.roomTypeName));

            // Add entries for new room types.
            foreach (var typeName in validTypeNames)
            {
                if (!entryList.Exists(e =>
                        string.Equals(e.roomTypeName, typeName, System.StringComparison.OrdinalIgnoreCase)))
                {
                    entryList.Add(new Zombera.Data.RoomFloorEntry { roomTypeName = typeName });
                }
            }

            config.entries = entryList.ToArray();
        }

        // ── Floor-material helpers ───────────────────────────────────────

        /// <summary>
        ///     Collects all distinct floor materials from a <see cref="Zombera.Data.BuildingSkinTable"/> —
        ///     across all zones, conditions, and the fallback skin set.
        ///     Returns null when no table is assigned (no filter applied).
        /// </summary>
        private static HashSet<Material> CollectFloorMaterialsFromTable(
            Zombera.Data.BuildingSkinTable table) =>
            CollectMaterialsFromTable(table, ss => ss.floorMaterials);

        /// <summary>
        ///     Collects all distinct interior wall paint materials from a
        ///     <see cref="Zombera.Data.BuildingSkinTable"/> across all zones and conditions.
        ///     Returns null when no table is assigned.
        /// </summary>
        private static HashSet<Material> CollectWallPaintMaterialsFromTable(
            Zombera.Data.BuildingSkinTable table) =>
            CollectMaterialsFromTable(table, ss => ss.wallInteriorMaterials);

        /// <summary>
        ///     Collects all distinct ceiling/roof materials from a
        ///     <see cref="Zombera.Data.BuildingSkinTable"/> across all zones and conditions.
        ///     Returns null when no table is assigned.
        /// </summary>
        private static HashSet<Material> CollectCeilingMaterialsFromTable(
            Zombera.Data.BuildingSkinTable table) =>
            CollectMaterialsFromTable(table, ss => ss.roofMaterials);

        /// <summary>
        ///     Shared traversal across the fallback skin set and every zone /
        ///     condition override in a <see cref="Zombera.Data.BuildingSkinTable"/>.
        /// </summary>
        private static HashSet<Material> CollectMaterialsFromTable(
            Zombera.Data.BuildingSkinTable table,
            System.Func<Zombera.Data.SkinSet, System.Collections.Generic.List<Zombera.Data.WeightedMaterial>> pickMaterials)
        {
            if (table == null) return null;

            var pool = new HashSet<Material>();
            CollectFromSkinSet(table.fallback, pickMaterials, pool);

            if (table.zones == null)
                return pool.Count > 0 ? pool : null;

            foreach (var zone in table.zones)
                CollectFromZone(zone, pickMaterials, pool);

            return pool.Count > 0 ? pool : null;
        }

        private static void CollectFromZone(
            Zombera.Data.ZoneSkinSet zone,
            System.Func<Zombera.Data.SkinSet, System.Collections.Generic.List<Zombera.Data.WeightedMaterial>> pickMaterials,
            HashSet<Material> pool)
        {
            if (zone.defaultSkins != null)
                foreach (var ss in zone.defaultSkins)
                    CollectFromSkinSet(ss, pickMaterials, pool);

            if (zone.conditionOverrides != null)
                foreach (var cond in zone.conditionOverrides)
                    CollectFromConditionSkinSet(cond, pickMaterials, pool);
        }

        private static void CollectFromConditionSkinSet(
            Zombera.Data.ConditionSkinSet cond,
            System.Func<Zombera.Data.SkinSet, System.Collections.Generic.List<Zombera.Data.WeightedMaterial>> pickMaterials,
            HashSet<Material> pool)
        {
            if (cond?.skins == null) return;
            foreach (var ss in cond.skins)
                CollectFromSkinSet(ss, pickMaterials, pool);
        }

        private static void CollectFromSkinSet(
            Zombera.Data.SkinSet ss,
            System.Func<Zombera.Data.SkinSet, System.Collections.Generic.List<Zombera.Data.WeightedMaterial>> pickMaterials,
            HashSet<Material> pool)
        {
            if (ss == null) return;
            var materials = pickMaterials(ss);
            if (materials == null) return;
            foreach (var wm in materials)
                if (wm.material != null)
                    pool.Add(wm.material);
        }

        // ── Drawing ──────────────────────────────────────────────────────

        /// <summary>
        ///     Draws a single room-floor entry with a room-type dropdown and
        ///     filtered material lists for floor, wall paint, and ceiling.
        /// </summary>
        private static void DrawRoomFloorEntry(
            SerializedProperty entryProp,
            List<string> validTypeNames,
            HashSet<Material> floorMaterialPool,
            HashSet<Material> wallPaintMaterialPool,
            HashSet<Material> ceilingMaterialPool)
        {
            var roomTypeNameProp = entryProp.FindPropertyRelative("roomTypeName");
            var materialsProp = entryProp.FindPropertyRelative("materials");
            var wallPaintProp = entryProp.FindPropertyRelative("wallPaintMaterials");
            var ceilingProp = entryProp.FindPropertyRelative("ceilingMaterials");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Room type dropdown.
            var selectedIndex = validTypeNames.FindIndex(
                n => string.Equals(n, roomTypeNameProp.stringValue, System.StringComparison.OrdinalIgnoreCase));
            if (selectedIndex < 0) selectedIndex = 0;

            selectedIndex = EditorGUILayout.Popup(
                "Room Type", selectedIndex, validTypeNames.ToArray());
            roomTypeNameProp.stringValue = validTypeNames[selectedIndex];

            // Floor materials.
            EditorGUILayout.LabelField("Floor", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"  {(materialsProp.arraySize)} material(s)", EditorStyles.miniLabel);
            DrawWeightedMaterialArray(materialsProp, floorMaterialPool);

            EditorGUILayout.Space(4f);

            // Wall paint materials.
            EditorGUILayout.LabelField("Wall Paint", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"  {(wallPaintProp.arraySize)} material(s)", EditorStyles.miniLabel);
            DrawWeightedMaterialArray(wallPaintProp, wallPaintMaterialPool);

            EditorGUILayout.Space(4f);

            // Ceiling materials.
            EditorGUILayout.LabelField("Ceiling", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"  {(ceilingProp.arraySize)} material(s)", EditorStyles.miniLabel);
            DrawWeightedMaterialArray(ceilingProp, ceilingMaterialPool);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        ///     Draws a <see cref="SerializedProperty"/> array of
        ///     <see cref="Zombera.Data.WeightedMaterial"/> entries with
        ///     optional material pool filtering.
        /// </summary>
        private static void DrawWeightedMaterialArray(
            SerializedProperty arrayProp,
            HashSet<Material> materialPool)
        {
            if (arrayProp == null) return;

            for (var i = 0; i < arrayProp.arraySize; i++)
            {
                var elementProp = arrayProp.GetArrayElementAtIndex(i);
                DrawWeightedMaterialElement(elementProp, materialPool);
            }

            // Add / Remove buttons.
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+", GUILayout.Width(24f)))
            {
                arrayProp.arraySize++;
                // Default new element (weight = 1).
                var newElem = arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1);
                var matProp = newElem.FindPropertyRelative("material");
                var weightProp = newElem.FindPropertyRelative("weight");
                matProp.objectReferenceValue = null;
                weightProp.floatValue = 1f;
            }

            if (arrayProp.arraySize > 0 && GUILayout.Button("-", GUILayout.Width(24f)))
                arrayProp.arraySize--;
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        ///     Draws a single weighted material element — a filtered material
        ///     dropdown (or object field) plus a weight float field.
        /// </summary>
        private static void DrawWeightedMaterialElement(
            SerializedProperty elementProp,
            HashSet<Material> materialPool)
        {
            var matProp = elementProp.FindPropertyRelative("material");
            var weightProp = elementProp.FindPropertyRelative("weight");

            EditorGUILayout.BeginHorizontal();

            if (materialPool != null)
            {
                // Constrain to materials from the BuildingSkinTable floor pool.
                matProp.objectReferenceValue = DrawFilteredMaterialField(
                    (Material)matProp.objectReferenceValue, materialPool);
            }
            else
            {
                matProp.objectReferenceValue = EditorGUILayout.ObjectField(
                    matProp.objectReferenceValue, typeof(Material), false,
                    GUILayout.Width(200f));
            }

            weightProp.floatValue = EditorGUILayout.FloatField(
                weightProp.floatValue, GUILayout.Width(60f));

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        ///     Draws a material object field filtered to only accept materials
        ///     present in <paramref name="pool"/>. Shows a dropdown of available
        ///     materials or the standard object picker with a help message.
        /// </summary>
        private static Material DrawFilteredMaterialField(
            Material current,
            HashSet<Material> pool)
        {
            if (pool == null || pool.Count == 0)
                return (Material)EditorGUILayout.ObjectField(
                    current, typeof(Material), false, GUILayout.Width(200f));

            // Build sorted list for stable display.
            var sorted = new List<Material>(pool);
            sorted.Sort((a, b) => string.CompareOrdinal(
                a != null ? a.name : "",
                b != null ? b.name : ""));

            var names = BuildMaterialNames(sorted);
            var newIndex = EditorGUILayout.Popup(IndexOfMaterial(sorted, current), names, GUILayout.Width(200f));
            return newIndex > 0 ? sorted[newIndex - 1] : null;
        }

        private static string[] BuildMaterialNames(List<Material> sorted)
        {
            var names = new string[sorted.Count + 1];
            names[0] = "(None)";
            for (var j = 0; j < sorted.Count; j++)
                names[j + 1] = sorted[j] != null ? sorted[j].name : "(Missing)";
            return names;
        }

        private static int IndexOfMaterial(List<Material> sorted, Material current)
        {
            if (current == null) return 0;
            for (var j = 0; j < sorted.Count; j++)
            {
                if (sorted[j] == current)
                    return j + 1;
            }
            return 0;
        }
    }
}
#endif
