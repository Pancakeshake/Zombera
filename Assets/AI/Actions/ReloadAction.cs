using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.AI.Actions
{
    /// <summary>
    /// Reusable reload action adapter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReloadAction : MonoBehaviour
    {
        [SerializeField] private UnitCombat unitCombat;
        [SerializeField] private CombatManager combatManager;

        public void Initialize(UnitCombat combat)
        {
            if (combat != null)
            {
                unitCombat = combat;
            }

            if (unitCombat == null)
            {
                unitCombat = GetComponent<UnitCombat>();
            }
        }

        public bool ExecuteReload()
        {
            if (unitCombat == null)
            {
                return false;
            }

            if (combatManager != null)
            {
                combatManager.RequestReload(unitCombat);
                return true;
            }

            unitCombat.Reload();
            return true;
        }

        // TODO: Add interrupt rules (movement, stunned, suppression, etc.).
        // TODO: Surface reload duration for utility scoring and UI hints.
    }
}