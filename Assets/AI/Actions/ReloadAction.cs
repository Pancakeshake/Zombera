using System.Collections;
using UnityEngine;
using Zombera.Characters;
using Zombera.Systems;

namespace Zombera.AI.Actions
{
    /// <summary>
    /// Reusable reload action adapter.
    /// Reload takes <see cref="reloadDurationSeconds"/> to complete and can be
    /// interrupted if the unit starts moving while <see cref="interruptOnMovement"/> is set.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReloadAction : MonoBehaviour
    {
        [SerializeField] private UnitCombat unitCombat;
        [SerializeField] private CombatManager combatManager;
        [SerializeField] private UnitController unitController;

        [Header("Reload Timing")]
        [Tooltip("How long the reload animation/action takes before ammo is restored.")]
        [SerializeField, Min(0f)] private float reloadDurationSeconds = 1.5f;

        [Header("Interrupt Rules")]
        [Tooltip("Cancel reload if the unit starts moving before it completes.")]
        [SerializeField] private bool interruptOnMovement = true;

        /// <summary>Total time (seconds) a reload takes. Exposed for utility scoring and UI.</summary>
        public float ReloadDurationSeconds => reloadDurationSeconds;

        /// <summary>True while a reload sequence is in progress.</summary>
        public bool IsReloading { get; private set; }

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

            if (unitController == null)
            {
                unitController = GetComponent<UnitController>();
            }
        }

        /// <summary>
        /// Begins a timed reload sequence. Returns false if already reloading or blocked by movement.
        /// </summary>
        public bool ExecuteReload()
        {
            if (unitCombat == null || IsReloading)
            {
                return false;
            }

            // Suppress reload while moving if the interrupt rule is active.
            if (interruptOnMovement && unitController != null && unitController.IsMoving)
            {
                return false;
            }

            StartCoroutine(ReloadRoutine());
            return true;
        }

        /// <summary>Cancels an in-progress reload (e.g. forced interrupt from external state).</summary>
        public void CancelReload()
        {
            if (IsReloading)
            {
                StopAllCoroutines();
                IsReloading = false;
            }
        }

        private IEnumerator ReloadRoutine()
        {
            IsReloading = true;
            float elapsed = 0f;

            while (elapsed < reloadDurationSeconds)
            {
                elapsed += Time.deltaTime;

                // Movement interrupt check.
                if (interruptOnMovement && unitController != null && unitController.IsMoving)
                {
                    IsReloading = false;
                    yield break;
                }

                yield return null;
            }

            // Commit the reload through the appropriate channel.
            if (combatManager != null)
            {
                combatManager.RequestReload(unitCombat);
            }
            else
            {
                unitCombat.Reload();
            }

            IsReloading = false;
        }
    }
}
