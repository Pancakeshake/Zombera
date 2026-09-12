#region



using System;

using System.Collections.Generic;

using UnityEngine;

using UnityEngine.AI;

using Zombera.Characters;



#endregion



namespace Zombera.Systems

{

    /// <summary>

    ///     Slot generation, separation, and stable member assignment for group move commands.

    /// </summary>

    public static partial class SquadMoveDeconfliction

    {

        public const float DefaultGroupSpacingBaseline = 1.4f;

        public const float DefaultAdaptiveSpacingGain = 0.08f;

        public const float DefaultMinSeparationFloor = 1.2f;

        public const float DefaultMinSeparationMultiplier = 2.4f;



        public struct Settings

        {

            public float GroupSpacingBaseline;

            public float AdaptiveSpacingGain;

            public float MinSeparationFloor;

            public float MinSeparationMultiplier;

            public int MaxDeconflictionIterations;

            public float DeconflictionStepMeters;

            public bool AdaptiveSpacingEnabled;

            public bool SampleSlotsOnNavMesh;

            public bool LogDiagnostics;

        }



        public struct Diagnostics

        {

            public int CommandId;

            public int MemberCount;

            public int GeneratedSlotCount;

            public int ResolvedSlotCount;

            public int IssuedCount;

            public int ConflictPushCount;

            public float MinPairwiseSeparation;

        }



        public static Settings DefaultSettings => new()

        {

            GroupSpacingBaseline = DefaultGroupSpacingBaseline,

            AdaptiveSpacingGain = DefaultAdaptiveSpacingGain,

            MinSeparationFloor = DefaultMinSeparationFloor,

            MinSeparationMultiplier = DefaultMinSeparationMultiplier,

            MaxDeconflictionIterations = 8,

            DeconflictionStepMeters = 0.5f,

            AdaptiveSpacingEnabled = true,

            SampleSlotsOnNavMesh = true,

            LogDiagnostics = false

        };



        public static Diagnostics LastDiagnostics { get; private set; }



        private static int _nextCommandId = 1;



        public static float ComputeSlotSpacing(int unitCount, in Settings settings)

        {

            var spacing = Mathf.Max(0.5f, settings.GroupSpacingBaseline);

            if (!settings.AdaptiveSpacingEnabled || unitCount <= 4) return spacing;



            spacing += (unitCount - 4) * Mathf.Max(0f, settings.AdaptiveSpacingGain);

            return spacing;

        }



        public static float ComputeMinSeparation(IReadOnlyList<SquadMember> members, in Settings settings)

        {

            var averageRadius = 0.35f;

            var counted = 0;



            if (members != null)

            {

                for (var i = 0; i < members.Count; i++)

                {

                    var member = members[i];

                    if (!IsOrderableMember(member)) continue;



                    var agent = member.UnitController != null

                        ? member.UnitController.GetComponent<NavMeshAgent>()

                        : null;

                    if (agent == null) continue;



                    averageRadius += agent.radius;

                    counted++;

                }

            }



            if (counted > 0) averageRadius /= counted;



            return Mathf.Max(

                settings.MinSeparationFloor,

                averageRadius * Mathf.Max(0.5f, settings.MinSeparationMultiplier));

        }



        public static int IssueGroupMove(

            IReadOnlyList<SquadMember> members,

            Vector3 destination,

            Vector3 groupForward,

            IReadOnlyList<Vector3> rawSlots,

            in Settings settings)

        {

            if (members == null || members.Count == 0)

            {

                LastDiagnostics = default;

                return 0;

            }



            var resolved = TryResolveGroupMoveAssignments(
                members,
                destination,
                groupForward,
                rawSlots,
                settings,
                _assignmentScratch);

            if (resolved == 0)
            {
                LastDiagnostics = default;
                return 0;
            }

            return IssueGroupMoveAssignments(_assignmentScratch, settings);
        }



        private static void LogDiagnostics(in Diagnostics diagnostics)

        {

            Debug.Log(

                $"[SquadMoveDeconfliction] cmd={diagnostics.CommandId} members={diagnostics.MemberCount} " +

                $"slots={diagnostics.GeneratedSlotCount}->{diagnostics.ResolvedSlotCount} issued={diagnostics.IssuedCount} " +

                $"conflictPushes={diagnostics.ConflictPushCount} minSep={diagnostics.MinPairwiseSeparation:F2}m");

        }



        private static readonly List<SquadMember> _orderableScratch = new(64);

        private static readonly List<Vector3> _slotScratch = new(64);

        private static readonly List<int> _availableSlotIndices = new(64);



        private static int CompareMembersForAssignment(SquadMember a, SquadMember b)

        {

            return string.Compare(

                ResolveMemberSortKey(a),

                ResolveMemberSortKey(b),

                StringComparison.Ordinal);

        }



        private static string ResolveMemberSortKey(SquadMember member)

        {

            if (member == null) return string.Empty;



            if (member.Unit != null && !string.IsNullOrWhiteSpace(member.Unit.UnitId))

                return member.Unit.UnitId;



            if (!string.IsNullOrWhiteSpace(member.MemberId))

                return member.MemberId;



            return member.GetInstanceID().ToString();

        }



        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)

        {

            a.y = 0f;

            b.y = 0f;

            return (b - a).sqrMagnitude;

        }



        private static bool IsOrderableMember(SquadMember member)

        {

            return member != null && member.IsAvailableForOrders();

        }

    }

}


