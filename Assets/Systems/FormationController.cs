using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Systems
{
    /// <summary>
    /// Calculates and applies combat formation slots for squad members.
    /// </summary>
    public sealed class FormationController : MonoBehaviour
    {
        [SerializeField] private FormationType activeFormation = FormationType.Line;

        public FormationType ActiveFormation => activeFormation;

        public void SetFormation(FormationType formationType)
        {
            activeFormation = formationType;

            // TODO: Trigger immediate squad repositioning to new formation.
        }

        public IReadOnlyList<Vector3> CalculateFormationSlots(Vector3 center, Vector3 forward, int unitCount)
        {
            List<Vector3> slots = new List<Vector3>(unitCount);

            // TODO: Calculate pattern offsets for line, wedge, defensive circle.
            _ = center;
            _ = forward;

            return slots;
        }
    }

    public enum FormationType
    {
        Line,
        Wedge,
        DefensiveCircle
    }
}