#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Custom inspector for <see cref="InteriorPropConfig"/>.
    ///     Provides dropdowns for room types (from RoomSettings) and auto-syncs
    ///     entries so only configured room types appear.
    /// </summary>
    [CustomEditor(typeof(InteriorPropConfig))]
    public sealed class InteriorPropConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty _roomSettings;
        private SerializedProperty _roomEntries;
        private SerializedProperty _fallbackProps;
        private SerializedProperty _fallbackSpawnAttempts;

        private Vector2 _entriesScroll;
        private Vector2 _fallbackScroll;

        private void OnEnable()
        {
            _roomSettings = serializedObject.FindProperty("roomSettings");
            _roomEntries = serializedObject.FindProperty("roomEntries");
            _fallbackProps = serializedObject.FindProperty("fallbackProps");
            _fallbackSpawnAttempts = serializedObject.FindProperty("fallbackSpawnAttempts");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var config = (InteriorPropConfig)target;

            // ── References ──────────────────────────────────────────────
            EditorGUILayout.PropertyField(_roomSettings, new GUIContent("Room Settings",
                "Reference to RoomSettings — constrains available room types. " +
                "Entries are auto-synced from this asset's RoomTypes."));

            // ── Collect constraints ─────────────────────────────────────
            var validRoomTypes = CollectValidRoomTypes(config.roomSettings);

            EditorGUILayout.Space(8f);

            // ── Room Entries ────────────────────────────────────────────
            EditorGUILayout.LabelField("Room Entries", EditorStyles.boldLabel);

            if (validRoomTypes.Count == 0)
            {
                if (config.roomSettings != null)
                    EditorGUILayout.HelpBox(
                        "The assigned RoomSettings has no RoomTypes defined. Add RoomType entries to RoomSettings.",
                        MessageType.Warning);
                else
                    EditorGUILayout.HelpBox(
                        "Assign a RoomSettings asset above to auto-sync room entries.",
                        MessageType.Info);
            }
            else
            {
                // Sync entries with available room types (add missing, remove stale).
                SyncRoomEntries(config, validRoomTypes);

                EditorGUI.indentLevel++;
                _entriesScroll = EditorGUILayout.BeginScrollView(_entriesScroll, GUILayout.MaxHeight(400f));

                for (var i = 0; i < _roomEntries.arraySize; i++)
                {
                    DrawRoomEntry(_roomEntries.GetArrayElementAtIndex(i), validRoomTypes);
                    EditorGUILayout.Space(2f);
                }

                EditorGUILayout.EndScrollView();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8f);

            // ── Door Clearance ────────────────────────────────────────
            EditorGUILayout.LabelField("Door Clearance", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            var doorClearanceProp = serializedObject.FindProperty("doorClearanceRadius");
            EditorGUILayout.PropertyField(doorClearanceProp, new GUIContent("Clearance Radius",
                "Hard exclusion radius (m) around each door. No props are placed inside this zone."));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(8f);

            // ── Fallback ────────────────────────────────────────────────
            EditorGUILayout.LabelField("Fallback", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_fallbackSpawnAttempts, new GUIContent("Spawn Attempts",
                "How many placement attempts to make per room when using the fallback list."));
            DrawPropPlacementArray(_fallbackProps);
            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }

        // ── Room-type helpers ────────────────────────────────────────────

        /// <summary>
        ///     Collects distinct room types from a <see cref="RoomSettings"/> asset.
        ///     Returns the types in the order they appear in RoomSettings.
        /// </summary>
        private static List<RoomType> CollectValidRoomTypes(RoomSettings rs)
        {
            var result = new List<RoomType>();
            if (rs == null || rs.RoomTypes == null) return result;
            foreach (var rt in rs.RoomTypes)
                if (!result.Contains(rt.Type))
                    result.Add(rt.Type);
            return result;
        }

        /// <summary>
        ///     Adds entries for room types that are missing from the config,
        ///     and removes entries for room types that no longer exist in RoomSettings.
        /// </summary>
        private static void SyncRoomEntries(
            InteriorPropConfig config,
            List<RoomType> validRoomTypes)
        {
            var entries = config.roomEntries ?? System.Array.Empty<RoomPropEntry>();
            var entryList = new List<RoomPropEntry>(entries);

            // Remove entries whose room type no longer exists in RoomSettings.
            entryList.RemoveAll(e => !validRoomTypes.Contains(e.roomType));

            // Add entries for new room types with default values.
            foreach (var roomType in validRoomTypes)
            {
                if (!entryList.Exists(e => e.roomType == roomType))
                {
                    entryList.Add(new RoomPropEntry
                    {
                        roomType = roomType,
                        spawnAttempts = 5,
                        props = System.Array.Empty<PropPlacement>(),
                    });
                }
            }

            config.roomEntries = entryList.ToArray();
        }

        // ── Drawing ──────────────────────────────────────────────────────

        /// <summary>
        ///     Draws a single room-prop entry with a room-type dropdown and
        ///     a list of prop placements and arrangements.
        /// </summary>
        private static void DrawRoomEntry(
            SerializedProperty entryProp,
            List<RoomType> validRoomTypes)
        {
            var roomTypeProp = entryProp.FindPropertyRelative("roomType");
            var spawnAttemptsProp = entryProp.FindPropertyRelative("spawnAttempts");
            var propsProp = entryProp.FindPropertyRelative("props");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Room type dropdown — show only types from RoomSettings.
            var roomTypeNames = new string[validRoomTypes.Count];
            var currentIndex = 0;
            for (var i = 0; i < validRoomTypes.Count; i++)
            {
                roomTypeNames[i] = validRoomTypes[i].ToString();
                if (validRoomTypes[i] == (RoomType)roomTypeProp.enumValueIndex)
                    currentIndex = i;
            }

            currentIndex = EditorGUILayout.Popup("Room Type", currentIndex, roomTypeNames);
            roomTypeProp.enumValueIndex = (int)validRoomTypes[currentIndex];

            // Spawn attempts.
            EditorGUILayout.PropertyField(spawnAttemptsProp, new GUIContent("Spawn Attempts",
                "How many weighted fill prop placement attempts per room. Set to 0 for hand-crafted prefabs only."));

            // ── Props ──────────────────────────────────────────────────
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(
                $"Props ({(propsProp?.arraySize ?? 0)})", EditorStyles.boldLabel);
            DrawPropPlacementArray(propsProp);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        ///     Draws an array of <see cref="PropPlacement"/> entries with
        ///     prefab field, weight, and placement constraints.
        /// </summary>
        private static void DrawPropPlacementArray(SerializedProperty arrayProp)
        {
            if (arrayProp == null) return;

            EditorGUI.indentLevel++;
            for (var i = 0; i < arrayProp.arraySize; i++)
            {
                var elementProp = arrayProp.GetArrayElementAtIndex(i);
                DrawPropPlacementElement(elementProp, i);
            }
            EditorGUI.indentLevel--;

            // Add / Remove buttons.
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+", GUILayout.Width(24f)))
            {
                arrayProp.arraySize++;
                var newElem = arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1);
                DefaultNewPropPlacement(newElem);
            }

            if (arrayProp.arraySize > 0 && GUILayout.Button("-", GUILayout.Width(24f)))
                arrayProp.arraySize--;
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        ///     Draws a single <see cref="PropPlacement"/> element.
        /// </summary>
        private static void DrawPropPlacementElement(
            SerializedProperty elementProp,
            int index)
        {
            var prefabProp = elementProp.FindPropertyRelative("prefab");
            var weightProp = elementProp.FindPropertyRelative("weight");
            var maxPerRoomProp = elementProp.FindPropertyRelative("maxPerRoom");
            var guaranteedCountProp = elementProp.FindPropertyRelative("guaranteedCount");
            var randomiseYawProp = elementProp.FindPropertyRelative("randomiseYaw");
            var yawSnapDegreesProp = elementProp.FindPropertyRelative("yawSnapDegrees");
            var yOffsetProp = elementProp.FindPropertyRelative("yOffset");
            var wallAlignmentProp = elementProp.FindPropertyRelative("wallAlignment");
            var minWallDistanceProp = elementProp.FindPropertyRelative("minWallDistance");
            var minDoorDistanceProp = elementProp.FindPropertyRelative("minDoorDistance");
            var footprintRadiusProp = elementProp.FindPropertyRelative("footprintRadius");

            var go = (GameObject)prefabProp.objectReferenceValue;
            var label = go != null ? go.name : $"[{index}] (None)";

            var showDetails = EditorGUILayout.Foldout(
                elementProp.isExpanded, label, true);
            elementProp.isExpanded = showDetails;

            if (!showDetails) return;

            EditorGUI.indentLevel++;

            // Core fields.
            EditorGUILayout.PropertyField(prefabProp, new GUIContent("Prefab"));
            EditorGUILayout.PropertyField(weightProp, new GUIContent("Weight"));
            EditorGUILayout.PropertyField(maxPerRoomProp, new GUIContent("Max Per Room"));
            EditorGUILayout.PropertyField(guaranteedCountProp, new GUIContent("Guaranteed",
                "Number guaranteed per room. Placed before weighted fill. 0 = optional only."));

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Placement", EditorStyles.miniBoldLabel);

            EditorGUILayout.PropertyField(randomiseYawProp, new GUIContent("Randomise Yaw"));
            if (randomiseYawProp.boolValue)
                EditorGUILayout.PropertyField(yawSnapDegreesProp, new GUIContent("Yaw Snap °",
                    "0 = free rotation. 90 = cardinal only (0/90/180/270)."));
            EditorGUILayout.PropertyField(yOffsetProp, new GUIContent("Y Offset"));
            EditorGUILayout.PropertyField(wallAlignmentProp, new GUIContent("Wall Alignment"));

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Rules", EditorStyles.miniBoldLabel);

            EditorGUILayout.PropertyField(minWallDistanceProp, new GUIContent("Min Wall Distance"));
            EditorGUILayout.PropertyField(minDoorDistanceProp, new GUIContent("Min Door Distance"));

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Overlap", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(footprintRadiusProp, new GUIContent("Footprint Radius",
                "XZ radius for overlap avoidance. 0 = no overlap check."));

            EditorGUI.indentLevel--;
        }

        /// <summary>
        ///     Sets default values for a newly added <see cref="PropPlacement"/> element.
        /// </summary>
        private static void DefaultNewPropPlacement(SerializedProperty elementProp)
        {
            elementProp.FindPropertyRelative("prefab").objectReferenceValue = null;
            elementProp.FindPropertyRelative("weight").floatValue = 1f;
            elementProp.FindPropertyRelative("maxPerRoom").intValue = 0;
            elementProp.FindPropertyRelative("guaranteedCount").intValue = 0;
            elementProp.FindPropertyRelative("randomiseYaw").boolValue = false;
            elementProp.FindPropertyRelative("yawSnapDegrees").floatValue = 90f;
            elementProp.FindPropertyRelative("yOffset").floatValue = 0f;
            elementProp.FindPropertyRelative("wallAlignment").enumValueIndex = 0;
            elementProp.FindPropertyRelative("minWallDistance").floatValue = 0f;
            elementProp.FindPropertyRelative("minDoorDistance").floatValue = 0f;
            elementProp.FindPropertyRelative("footprintRadius").floatValue = 0f;
        }
    }
}
#endif
