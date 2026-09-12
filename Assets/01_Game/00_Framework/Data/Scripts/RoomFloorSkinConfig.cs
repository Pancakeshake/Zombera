using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Zombera.Data
{
    /// <summary>
    ///     Maps room types to weighted floor material lists. References a
    ///     <see cref="BuildingSkinTable"/> for the floor material catalog and
    ///     controls per-room floor materials with weights. Floor materials are
    ///     no longer picked from <see cref="SkinSet"/> — this config is the
    ///     single authority for all floor material assignment.
    ///
    ///     Room type names must match <c>Zombera.Editor.RoomType</c> enum names:
    ///     LivingRoom, Kitchen, Bedroom, Bathroom, Hallway, Dining, Storage,
    ///     Garage, Entry, Utility.
    ///
    ///     Create via Assets → Create → Zombera/Building → Room Floor Skin Config.
    ///     Save the asset under <c>Assets/02_Shared/ScriptableObjects/Buildings/Skins/</c>.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Zombera/Building/Room Floor Skin Config",
        fileName = "RoomFloorSkinConfig")]
    public sealed class RoomFloorSkinConfig : ScriptableObject
    {
        [Tooltip("The BuildingSkinTable this config draws floor materials from. Materials assigned in entries are constrained to this table's floor material pools.")]
        public BuildingSkinTable buildingSkinTable;

        [Tooltip("Reference to RoomSettings — constrains available room types. Entries with room types outside this set are ignored.")]
        public UnityEngine.Object roomSettings;

        [Tooltip("Per-room-type floor material entries. Room type is matched by name (e.g. \"LivingRoom\", \"Kitchen\").")]
        public RoomFloorEntry[] entries = Array.Empty<RoomFloorEntry>();

        [Tooltip("Fallback floor material used when a room type has no explicit entry.")]
        public WeightedMaterial[] fallback = Array.Empty<WeightedMaterial>();

        [Header("Interior Wall Paint")]
        [Tooltip("Fallback interior wall paint material used when a room type has no explicit wall paint entry.")]
        public WeightedMaterial[] wallPaintFallback = Array.Empty<WeightedMaterial>();

        [Header("Ceiling")]
        [Tooltip("Fallback ceiling material used when a room type has no explicit ceiling entry.")]
        public WeightedMaterial[] ceilingFallback = Array.Empty<WeightedMaterial>();

        /// <summary>
        ///     Returns the weighted material list for a given room type name, or the fallback.
        /// </summary>
        public WeightedMaterial[] GetMaterialsForRoom(string roomTypeName)
        {
            if (string.IsNullOrEmpty(roomTypeName))
                return fallback;

            foreach (var entry in entries)
                if (string.Equals(entry.roomTypeName, roomTypeName, StringComparison.OrdinalIgnoreCase)
                    && entry.materials is { Length: > 0 })
                    return entry.materials;

            return fallback;
        }

        /// <summary>
        ///     Returns the weighted material list for a given room type index (legacy).
        ///     Converts the index to the corresponding <c>RoomType</c> name.
        /// </summary>
        public WeightedMaterial[] GetMaterialsForRoom(int roomTypeIndex)
        {
            var name = RoomTypeNameFromIndex(roomTypeIndex);
            return GetMaterialsForRoom(name);
        }

        /// <summary>
        ///     Picks a random floor material for the given room type name using weighted selection.
        ///     Returns null if no materials are configured.
        /// </summary>
        public Material PickFloorMaterial(string roomTypeName)
        {
            var list = GetMaterialsForRoom(roomTypeName);
            return PickWeightedFromList(list);
        }

        /// <summary>
        ///     Picks a random floor material for the given room type index using weighted selection.
        ///     Returns null if no materials are configured.
        /// </summary>
        public Material PickFloorMaterial(int roomTypeIndex)
        {
            var list = GetMaterialsForRoom(roomTypeIndex);
            return PickWeightedFromList(list);
        }

        /// <summary>
        ///     Returns the weighted wall paint material list for a given room type name, or the fallback.
        /// </summary>
        public WeightedMaterial[] GetWallPaintMaterialsForRoom(string roomTypeName)
        {
            if (string.IsNullOrEmpty(roomTypeName))
                return wallPaintFallback;

            foreach (var entry in entries)
                if (string.Equals(entry.roomTypeName, roomTypeName, StringComparison.OrdinalIgnoreCase)
                    && entry.wallPaintMaterials is { Length: > 0 })
                    return entry.wallPaintMaterials;

            return wallPaintFallback;
        }

        /// <summary>
        ///     Picks a random wall paint material for the given room type name using weighted selection.
        ///     Returns null if no materials are configured.
        /// </summary>
        public Material PickWallPaintMaterial(string roomTypeName)
        {
            var list = GetWallPaintMaterialsForRoom(roomTypeName);
            return PickWeightedFromList(list);
        }

        /// <summary>
        ///     Returns the weighted ceiling material list for a given room type name, or the fallback.
        /// </summary>
        public WeightedMaterial[] GetCeilingMaterialsForRoom(string roomTypeName)
        {
            if (string.IsNullOrEmpty(roomTypeName))
                return ceilingFallback;

            foreach (var entry in entries)
                if (string.Equals(entry.roomTypeName, roomTypeName, StringComparison.OrdinalIgnoreCase)
                    && entry.ceilingMaterials is { Length: > 0 })
                    return entry.ceilingMaterials;

            return ceilingFallback;
        }

        /// <summary>
        ///     Picks a random ceiling material for the given room type name using weighted selection.
        ///     Returns null if no materials are configured.
        /// </summary>
        public Material PickCeilingMaterial(string roomTypeName)
        {
            var list = GetCeilingMaterialsForRoom(roomTypeName);
            return PickWeightedFromList(list);
        }

        private static Material PickWeightedFromList(WeightedMaterial[] list)
        {
            if (list == null || list.Length == 0)
                return null;

            var total = TotalWeight(list);
            if (total <= 0f)
                return FirstWithMaterial(list);

            var roll = UnityEngine.Random.value * total;
            var acc = 0f;
            foreach (var wm in list)
            {
                if (wm.material == null) continue;
                acc += Mathf.Max(0f, wm.weight);
                if (roll <= acc)
                    return wm.material;
            }

            return LastWithMaterial(list);
        }

        private static float TotalWeight(WeightedMaterial[] list)
        {
            var total = 0f;
            foreach (var wm in list)
            {
                if (wm.material == null) continue;
                total += Mathf.Max(0f, wm.weight);
            }
            return total;
        }

        private static Material FirstWithMaterial(WeightedMaterial[] list)
        {
            foreach (var wm in list)
            {
                if (wm.material != null)
                    return wm.material;
            }
            return null;
        }

        private static Material LastWithMaterial(WeightedMaterial[] list)
        {
            for (var i = list.Length - 1; i >= 0; i--)
            {
                if (list[i].material != null)
                    return list[i].material;
            }
            return null;
        }

        /// <summary>
        ///     Maps a <c>RoomType</c> integer index to its enum name.
        ///     Must stay in sync with <c>Zombera.Editor.RoomType</c>.
        /// </summary>
        public static string RoomTypeNameFromIndex(int index)
        {
            return index switch
            {
                0 => "LivingRoom",
                1 => "Kitchen",
                2 => "Bedroom",
                3 => "Bathroom",
                4 => "Hallway",
                5 => "Dining",
                6 => "Storage",
                7 => "Garage",
                8 => "Entry",
                9 => "Utility",
                10 => "SalesFloor",
                11 => "Stockroom",
                12 => "CommercialKitchen",
                13 => "ChangeRoom",
                14 => "Office",
                15 => "StaffRoom",
                16 => "Stairwell",
                _ => null
            };
        }

        /// <summary>
        ///     Maps a <c>RoomType</c> name to its integer index.
        ///     Returns -1 for unknown names.
        /// </summary>
        public static int RoomTypeIndexFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            return name switch
            {
                "LivingRoom" => 0,
                "Kitchen" => 1,
                "Bedroom" => 2,
                "Bathroom" => 3,
                "Hallway" => 4,
                "Dining" => 5,
                "Storage" => 6,
                "Garage" => 7,
                "Entry" => 8,
                "Utility" => 9,
                "SalesFloor" => 10,
                "Stockroom" => 11,
                "CommercialKitchen" => 12,
                "ChangeRoom" => 13,
                "Office" => 14,
                "StaffRoom" => 15,
                "Stairwell" => 16,
                _ => -1
            };
        }
    }

    /// <summary>
    ///     Maps a single room type (by name, matching Zombera.Editor.RoomType enum names)
    ///     to weighted material lists for floors, interior wall paint, and ceilings.
    /// </summary>
    [Serializable]
    public struct RoomFloorEntry
    {
        [FormerlySerializedAs("roomType")]
        [Tooltip("Room type name (LivingRoom, Kitchen, Bedroom, Bathroom, Hallway, Dining, Storage, Garage, Entry, Utility).")]
        public string roomTypeName;

        [Tooltip("Weighted floor materials for this room type.")]
        public WeightedMaterial[] materials;

        [Tooltip("Weighted interior wall paint materials for this room type. Applied to IntWall_* renderers inside this room. When empty, falls back to the global interior wall material.")]
        public WeightedMaterial[] wallPaintMaterials;

        [Tooltip("Weighted ceiling materials for this room type. When empty, falls back to wall paint, then to the global interior wall material.")]
        public WeightedMaterial[] ceilingMaterials;
    }
}