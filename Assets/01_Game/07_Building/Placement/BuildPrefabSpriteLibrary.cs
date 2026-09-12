#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.BuildingSystem
{
    [Serializable]
    public sealed class BuildPrefabSpriteEntry
    {
        public string partReference;
        public Sprite sprite;

        [Tooltip("Source prefab asset path used when this entry was generated.")]
        public string prefabAssetPath;
    }

    [CreateAssetMenu(menuName = "Zombera/Building/Build Prefab Sprite Library", fileName = "BuildPrefabSpriteLibrary")]
    public sealed class BuildPrefabSpriteLibrary : ScriptableObject
    {
        [SerializeField] private List<BuildPrefabSpriteEntry> entries = new();

        private readonly Dictionary<string, Sprite> _lookup = new(StringComparer.OrdinalIgnoreCase);

        public bool TryGetSprite(string partReference, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(partReference)) return false;

            EnsureLookup();
            return _lookup.TryGetValue(partReference.Trim(), out sprite) && sprite != null;
        }

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        private void EnsureLookup()
        {
            if (_lookup.Count > 0 || entries == null || entries.Count == 0) return;
            RebuildLookup();
        }

        private void RebuildLookup()
        {
            _lookup.Clear();

            if (entries == null || entries.Count == 0) return;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.partReference) || entry.sprite == null)
                    continue;

                var key = entry.partReference.Trim();
                if (!_lookup.ContainsKey(key))
                    _lookup.Add(key, entry.sprite);
            }
        }

#if UNITY_EDITOR
        public void ReplaceEntries(List<BuildPrefabSpriteEntry> replacementEntries)
        {
            if (replacementEntries == null)
            {
                entries.Clear();
                RebuildLookup();
                return;
            }

            entries = replacementEntries;
            RebuildLookup();
        }
#endif
    }
}
