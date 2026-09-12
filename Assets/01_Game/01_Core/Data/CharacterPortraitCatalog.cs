#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.Core
{
    /// <summary>
    ///     Runtime portrait source for player portrait selection.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Character/Portrait Catalog", fileName = "CharacterPortraitCatalog")]
    public sealed class CharacterPortraitCatalog : ScriptableObject
    {
        public const string DefaultResourcesPath = "Zombera/CharacterPortraitCatalog";

        [SerializeField] private List<PortraitEntry> portraits = new();

        public int Count => portraits != null ? portraits.Count : 0;
        public IReadOnlyList<PortraitEntry> Portraits => portraits;

        public static CharacterPortraitCatalog LoadDefault()
        {
            return Resources.Load<CharacterPortraitCatalog>(DefaultResourcesPath);
        }

        public bool TryGetPortrait(int index, out PortraitEntry entry)
        {
            if (portraits == null || index < 0 || index >= portraits.Count)
            {
                entry = default;
                return false;
            }

            entry = portraits[index];
            return entry.sprite != null;
        }

        public string GetPortraitId(int index)
        {
            return TryGetPortrait(index, out var entry)
                ? NormalizePortraitId(entry.id)
                : string.Empty;
        }

        public Sprite GetPortraitSprite(int index)
        {
            return TryGetPortrait(index, out var entry) ? entry.sprite : null;
        }

        public int IndexOfPortraitId(string portraitId)
        {
            if (portraits == null || string.IsNullOrWhiteSpace(portraitId)) return -1;

            var normalizedId = NormalizePortraitId(portraitId);

            for (var i = 0; i < portraits.Count; i++)
            {
                var entry = portraits[i];
                if (entry.sprite == null) continue;
                if (string.Equals(NormalizePortraitId(entry.id), normalizedId, StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        public int IndexOfSprite(Sprite sprite)
        {
            if (sprite == null || portraits == null) return -1;

            for (var i = 0; i < portraits.Count; i++)
                if (portraits[i].sprite == sprite)
                    return i;

            return -1;
        }

        public bool TryGetSpriteById(string portraitId, out Sprite sprite)
        {
            sprite = null;

            var index = IndexOfPortraitId(portraitId);
            if (index < 0) return false;

            sprite = portraits[index].sprite;
            return sprite != null;
        }

        public static string NormalizePortraitId(string portraitId)
        {
            return string.IsNullOrWhiteSpace(portraitId)
                ? string.Empty
                : portraitId.Trim();
        }

#if UNITY_EDITOR
        public void SetPortraitEntries(IReadOnlyList<PortraitEntry> entries)
        {
            portraits ??= new List<PortraitEntry>();
            portraits.Clear();

            if (entries == null) return;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.sprite == null) continue;

                portraits.Add(new PortraitEntry
                {
                    id = NormalizePortraitId(entry.id),
                    sprite = entry.sprite
                });
            }
        }
#endif

        [Serializable]
        public struct PortraitEntry
        {
            public string id;
            public Sprite sprite;
        }
    }
}