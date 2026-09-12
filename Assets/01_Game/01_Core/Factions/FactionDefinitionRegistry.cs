using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Factions
{
    [CreateAssetMenu(menuName = "Zombera/Factions/Faction Definition Registry", fileName = "FactionDefinitionRegistry")]
    public sealed class FactionDefinitionRegistry : ScriptableObject
    {
        private static FactionDefinitionRegistry _instance;

        [SerializeField] private List<FactionDefinition> factions = new();

        public static FactionDefinitionRegistry Instance
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = Resources.Load<FactionDefinitionRegistry>("FactionDefinitionRegistry");
                if (_instance == null)
                {
                    var registries = Resources.FindObjectsOfTypeAll<FactionDefinitionRegistry>();
                    if (registries.Length > 0) _instance = registries[0];
                }

                return _instance;
            }
        }

        public IReadOnlyList<FactionDefinition> Factions => factions;

        public FactionDefinition GetById(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId)) return null;

            for (var i = 0; i < factions.Count; i++)
            {
                var faction = factions[i];
                if (faction != null && string.Equals(faction.FactionId, factionId, System.StringComparison.Ordinal))
                    return faction;
            }

            return null;
        }
    }
}
