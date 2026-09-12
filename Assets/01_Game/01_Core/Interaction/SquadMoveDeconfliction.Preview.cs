#region

using System.Collections.Generic;
using UnityEngine;
using Zombera.Characters;

#endregion

namespace Zombera.Systems
{
    public static partial class SquadMoveDeconfliction
    {
        public readonly struct GroupMoveAssignment
        {
            public SquadMember Member { get; }
            public Vector3 Destination { get; }

            public GroupMoveAssignment(SquadMember member, Vector3 destination)
            {
                Member = member;
                Destination = destination;
            }
        }

        private static readonly List<GroupMoveAssignment> _assignmentScratch = new(64);

        /// <summary>
        ///     Resolves deconflicted per-member destinations without issuing move commands.
        ///     Uses the same slot pipeline as <see cref="IssueGroupMove" />.
        /// </summary>
        public static int TryResolveGroupMoveAssignments(
            IReadOnlyList<SquadMember> members,
            Vector3 destination,
            Vector3 groupForward,
            IReadOnlyList<Vector3> rawSlots,
            in Settings settings,
            List<GroupMoveAssignment> assignmentsOut)
        {
            assignmentsOut?.Clear();
            if (assignmentsOut == null || members == null || members.Count == 0) return 0;

            _orderableScratch.Clear();
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (!IsOrderableMember(member)) continue;
                _orderableScratch.Add(member);
            }

            if (_orderableScratch.Count == 0) return 0;

            MovementDestinationResolver.TryValidateSlotPosition(destination, destination, out destination);

            if (_orderableScratch.Count == 1)
            {
                var solo = _orderableScratch[0];
                var soloDestination = destination;
                MovementDestinationResolver.TryValidateSlotPosition(soloDestination, destination, out soloDestination);
                assignmentsOut.Add(new GroupMoveAssignment(solo, soloDestination));
                return 1;
            }

            _slotScratch.Clear();
            if (rawSlots != null && rawSlots.Count > 0)
                _slotScratch.AddRange(rawSlots);
            else
            {
                var generated = CalculateRadialFallbackSlots(
                    destination,
                    groupForward,
                    _orderableScratch.Count,
                    ComputeSlotSpacing(_orderableScratch.Count, settings));
                _slotScratch.AddRange(generated);
            }

            while (_slotScratch.Count < _orderableScratch.Count)
                _slotScratch.Add(destination);

            var minSeparation = ComputeMinSeparation(_orderableScratch, settings);
            DeconflictSlots(_slotScratch, minSeparation, settings.MaxDeconflictionIterations,
                settings.DeconflictionStepMeters);
            EnsureSlotsGrounded(_slotScratch, destination);

            PushOutCollidingSlots(
                _slotScratch,
                destination,
                minSeparation,
                settings.MaxDeconflictionIterations,
                settings.DeconflictionStepMeters);

            DeconflictSlots(_slotScratch, minSeparation, settings.MaxDeconflictionIterations,
                settings.DeconflictionStepMeters);
            EnsureSlotsGrounded(_slotScratch, destination);

            _orderableScratch.Sort(CompareMembersForAssignment);

            _availableSlotIndices.Clear();
            for (var i = 0; i < _slotScratch.Count; i++)
                _availableSlotIndices.Add(i);

            for (var m = 0; m < _orderableScratch.Count; m++)
            {
                var member = _orderableScratch[m];
                if (member?.UnitController == null) continue;

                var memberPos = member.UnitController.transform.position;
                var bestListIndex = -1;
                var bestDist = float.MaxValue;
                var bestSlotIndex = int.MaxValue;

                for (var s = 0; s < _availableSlotIndices.Count; s++)
                {
                    var slotIndex = _availableSlotIndices[s];
                    var slotPos = _slotScratch[slotIndex];
                    var dist = HorizontalSqrDistance(memberPos, slotPos);
                    if (dist < bestDist || (Mathf.Approximately(dist, bestDist) && slotIndex < bestSlotIndex))
                    {
                        bestDist = dist;
                        bestSlotIndex = slotIndex;
                        bestListIndex = s;
                    }
                }

                if (bestListIndex < 0) continue;

                assignmentsOut.Add(new GroupMoveAssignment(member, _slotScratch[bestSlotIndex]));
                _availableSlotIndices.RemoveAt(bestListIndex);
            }

            return assignmentsOut.Count;
        }

        /// <summary>
        ///     Issues move commands for pre-resolved assignments (e.g. ghost preview commit).
        /// </summary>
        public static int IssueGroupMoveAssignments(
            IReadOnlyList<GroupMoveAssignment> assignments,
            in Settings settings)
        {
            if (assignments == null || assignments.Count == 0)
            {
                LastDiagnostics = default;
                return 0;
            }

            var commandId = _nextCommandId++;
            var issued = 0;

            for (var i = 0; i < assignments.Count; i++)
            {
                var assignment = assignments[i];
                var member = assignment.Member;
                if (member?.UnitController == null) continue;

                var profile = assignments.Count == 1
                    ? MoveArrivalProfile.Precise
                    : MoveArrivalProfile.GroupCommand;
                member.UnitController.MoveTo(assignment.Destination, profile);
                member.SetCommandState(SquadCommandType.Move);
                issued++;
            }

            LastDiagnostics = new Diagnostics
            {
                CommandId = commandId,
                MemberCount = issued,
                GeneratedSlotCount = issued,
                ResolvedSlotCount = issued,
                IssuedCount = issued,
                ConflictPushCount = 0,
                MinPairwiseSeparation = issued > 1
                    ? ComputeMinPairwiseSeparationFromAssignments(assignments)
                    : float.PositiveInfinity
            };

            if (settings.LogDiagnostics)
                LogDiagnostics(LastDiagnostics);

            return issued;
        }

        private static float ComputeMinPairwiseSeparationFromAssignments(IReadOnlyList<GroupMoveAssignment> assignments)
        {
            if (assignments == null || assignments.Count < 2) return float.PositiveInfinity;

            var minSeparation = float.PositiveInfinity;
            for (var i = 0; i < assignments.Count; i++)
            {
                for (var j = i + 1; j < assignments.Count; j++)
                {
                    var dist = HorizontalSqrDistance(assignments[i].Destination, assignments[j].Destination);
                    if (dist < minSeparation) minSeparation = dist;
                }
            }

            return minSeparation == float.PositiveInfinity ? minSeparation : Mathf.Sqrt(minSeparation);
        }
    }
}
