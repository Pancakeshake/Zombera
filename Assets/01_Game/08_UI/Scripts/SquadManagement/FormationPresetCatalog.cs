#region

using Zombera.Systems;

#endregion

namespace Zombera.UI.SquadManagement
{
    public readonly struct FormationPresetEntry
    {
        public FormationType Type { get; }
        public string Title { get; }
        public string ShortLabel { get; }
        public string Description { get; }

        public FormationPresetEntry(FormationType type, string title, string shortLabel, string description)
        {
            Type = type;
            Title = title;
            ShortLabel = shortLabel;
            Description = description;
        }
    }

    public static class FormationPresetCatalog
    {
        public static readonly FormationPresetEntry[] QuickStripPresets =
        {
            new(FormationType.DefaultMovement, "Default", "DEF", "Grid layout using the same spacing as marching."),
            new(FormationType.Spread, "Spread", "SPR", "Balanced travel line for general marching."),
            new(FormationType.Wedge, "Wedge", "WDG", "Piercing wedge to break contact."),
            new(FormationType.DefensiveCircle, "Circle", "CIR", "Ring formation for all-around defense."),
            new(FormationType.SpearWall, "Spear Wall", "SPW", "Tight layered front for frontal defense.")
        };

        public static readonly FormationPresetEntry[] PanelPresets =
        {
            new(FormationType.SpearWall, "SPEAR WALL", "SPW", "Tight layered front for frontal defense."),
            new(FormationType.Spread, "SPREAD", "SPR", "Balanced travel line for general marching."),
            new(FormationType.DefaultMovement, "DEFAULT MOVEMENT", "DEF", "Grid layout using the same spacing as marching."),
            new(FormationType.Line, "LINE", "LIN", "Classic shoulder-to-shoulder combat line."),
            new(FormationType.Wedge, "WEDGE", "WDG", "Piercing wedge to break contact."),
            new(FormationType.DefensiveCircle, "DEFENSIVE CIRCLE", "CIR", "Ring formation for all-around defense.")
        };

        public static int IndexOfType(FormationPresetEntry[] presets, FormationType type)
        {
            if (presets == null) return -1;

            for (var i = 0; i < presets.Length; i++)
            {
                if (presets[i].Type == type) return i;
            }

            return -1;
        }

        public static bool TryGetEntry(FormationPresetEntry[] presets, FormationType type,
            out FormationPresetEntry entry)
        {
            var index = IndexOfType(presets, type);
            if (index < 0)
            {
                entry = default;
                return false;
            }

            entry = presets[index];
            return true;
        }
    }
}
