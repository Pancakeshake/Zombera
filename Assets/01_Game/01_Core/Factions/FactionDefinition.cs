using System;
using UnityEngine;
using Zombera.Characters;

namespace Zombera.Factions
{
    [CreateAssetMenu(menuName = "Zombera/Factions/Faction Definition", fileName = "FactionDefinition")]
    public sealed class FactionDefinition : ScriptableObject
    {
        [SerializeField] private string factionId = string.Empty;
        [SerializeField] private string displayName = "Faction";
        [SerializeField] [TextArea(2, 6)] private string description = string.Empty;
        [SerializeField] private Sprite icon;
        [SerializeField] private Color primaryColor = Color.white;
        [SerializeField] private FactionCategory category = FactionCategory.Survivor;
        [SerializeField] private FactionDiplomacyState defaultAttitudeProfile = FactionDiplomacyState.Neutral;
        [SerializeField] private int defaultStandingValue;
        [SerializeField] private bool isPlayableContact;
        [SerializeField] private bool startsDiscovered;

        public string FactionId => factionId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Color PrimaryColor => primaryColor;
        public FactionCategory Category => category;
        public FactionDiplomacyState DefaultAttitudeProfile => defaultAttitudeProfile;
        public int DefaultStandingValue => defaultStandingValue;
        public bool IsPlayableContact => isPlayableContact;
        public bool StartsDiscovered => startsDiscovered;

        public UnitFaction CoarseFaction => UnitFactionUtility.CoarseFactionFromFactionId(factionId);

        public static FactionDefinition CreateRuntime(
            string id,
            string name,
            string lore,
            FactionCategory factionCategory,
            FactionDiplomacyState defaultAttitude,
            int defaultStanding,
            bool discovered,
            Color color)
        {
            var definition = CreateInstance<FactionDefinition>();
            definition.factionId = id;
            definition.displayName = name;
            definition.description = lore;
            definition.category = factionCategory;
            definition.defaultAttitudeProfile = defaultAttitude;
            definition.defaultStandingValue = defaultStanding;
            definition.startsDiscovered = discovered;
            definition.primaryColor = color;
            definition.isPlayableContact = factionCategory is FactionCategory.Settlement or FactionCategory.Trader;
            return definition;
        }
    }
}
