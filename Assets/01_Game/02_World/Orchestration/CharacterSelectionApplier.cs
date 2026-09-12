using System.Linq;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.Core
{
    /// <summary>
    ///     Applies the main-menu character creation selection (name, stats, carry capacity)
    ///     to the spawned player unit at world-session start. Extracted from GameManager.
    /// </summary>
    internal static class CharacterSelectionApplier
    {
        public static void ApplyToActivePlayer()
        {
            if (!CharacterSelectionState.HasSelection) return;

            if (UnitManager.Instance?.GetUnitsByRole(UnitRole.Player).FirstOrDefault() is not { } player) return;

            if (!string.IsNullOrWhiteSpace(CharacterSelectionState.SelectedCharacterName))
                player.name = CharacterSelectionState.SelectedCharacterName;

            ApplySelectedCharacterBuild(player);
        }

        private static void ApplySelectedCharacterBuild(Unit player)
        {
            if (player == null) return;

            if (player.Controller != null) player.Controller.SetMoveSpeed(CharacterSelectionState.SelectedMoveSpeed);

            if (player.Inventory != null)
                player.Inventory.SetWeightLimit(CharacterSelectionState.SelectedCarryCapacity);

            if (player.Stats != null)
            {
                player.Stats.ResetAllSkillsToLevelOne();
                player.Stats.SetStamina(CharacterSelectionState.SelectedStamina);
                player.Stats.SetStrengthBaseHealth(CharacterSelectionState.SelectedMaxHealth, true);
            }
            else if (player.Health != null)
            {
                player.Health.SetMaxHealth(CharacterSelectionState.SelectedMaxHealth, true);
            }
        }
    }
}
