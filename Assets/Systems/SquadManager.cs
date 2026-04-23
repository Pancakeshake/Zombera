using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zombera.Systems
{
    /// <summary>
    /// Tracks all active squad members and coordinates group-level command dispatch.
    /// </summary>
    public sealed class SquadManager : MonoBehaviour
    {
        public static SquadManager Instance { get; private set; }

        [SerializeField] private CommandSystem commandSystem;

        private readonly List<SquadMember> squadMembers = new List<SquadMember>();
        private readonly List<SquadMember> selectedMembers = new List<SquadMember>();
        private readonly List<SquadMember> commandMembersBuffer = new List<SquadMember>();

        public IReadOnlyList<SquadMember> SquadMembers => squadMembers;
        public IReadOnlyList<SquadMember> SelectedMembers => selectedMembers;
        public bool HasSelectedMembers => selectedMembers.Count > 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            RefreshSquadRoster();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        public void RefreshSquadRoster()
        {
            squadMembers.Clear();
            SquadMember[] members = FindObjectsByType<SquadMember>(FindObjectsSortMode.None);

            for (int i = 0; i < members.Length; i++)
            {
                RegisterMember(members[i]);
            }

            PruneSelectedMembers();
        }

        public void RegisterMember(SquadMember member)
        {
            if (member == null || squadMembers.Contains(member))
            {
                return;
            }

            squadMembers.Add(member);

            Core.EventSystem.PublishGlobal(new Core.SquadRosterChangedEvent
            {
                Member = member,
                WasAdded = true
            });

            PruneSelectedMembers();
        }

        public void UnregisterMember(SquadMember member)
        {
            if (member == null)
            {
                return;
            }

            bool removed = squadMembers.Remove(member);

            if (!removed)
            {
                return;
            }

            selectedMembers.Remove(member);

            Core.EventSystem.PublishGlobal(new Core.SquadRosterChangedEvent
            {
                Member = member,
                WasAdded = false
            });

            // Reassign any active commands that were targeting the leaving member.
            if (commandSystem != null && squadMembers.Count > 0)
            {
                commandSystem.ReassignCommandsAwayFrom(member, squadMembers);
            }
        }

        public void IssueOrder(SquadCommandType commandType, Vector3 targetPosition = default)
        {
            if (commandSystem == null)
            {
                return;
            }

            IReadOnlyList<SquadMember> targets = ResolveCommandMembers();
            commandSystem.ExecuteCommand(commandType, targets, targetPosition);
        }

        public void SetSelectedMembers(IReadOnlyList<SquadMember> members)
        {
            selectedMembers.Clear();

            if (members == null)
            {
                return;
            }

            for (int i = 0; i < members.Count; i++)
            {
                SquadMember member = members[i];
                if (!IsMemberSelectable(member) || !squadMembers.Contains(member) || selectedMembers.Contains(member))
                {
                    continue;
                }

                selectedMembers.Add(member);
            }
        }

        public void SelectAllMembers()
        {
            selectedMembers.Clear();

            for (int i = 0; i < squadMembers.Count; i++)
            {
                SquadMember member = squadMembers[i];
                if (!IsMemberSelectable(member))
                {
                    continue;
                }

                selectedMembers.Add(member);
            }
        }

        public void ClearSelectedMembers()
        {
            selectedMembers.Clear();
        }

        public bool IsMemberSelected(SquadMember member)
        {
            return member != null && selectedMembers.Contains(member);
        }

        public SquadMember GetMemberById(string memberId)
        {
            for (int i = 0; i < squadMembers.Count; i++)
            {
                if (squadMembers[i].MemberId == memberId)
                {
                    return squadMembers[i];
                }
            }

            return null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;
            RefreshSquadRoster();
        }

        private IReadOnlyList<SquadMember> ResolveCommandMembers()
        {
            commandMembersBuffer.Clear();

            if (selectedMembers.Count > 0)
            {
                for (int i = 0; i < selectedMembers.Count; i++)
                {
                    SquadMember selected = selectedMembers[i];
                    if (!IsMemberSelectable(selected))
                    {
                        continue;
                    }

                    commandMembersBuffer.Add(selected);
                }
            }

            if (commandMembersBuffer.Count == 0)
            {
                for (int i = 0; i < squadMembers.Count; i++)
                {
                    SquadMember member = squadMembers[i];
                    if (!IsMemberSelectable(member))
                    {
                        continue;
                    }

                    commandMembersBuffer.Add(member);
                }
            }

            return commandMembersBuffer;
        }

        private void PruneSelectedMembers()
        {
            for (int i = selectedMembers.Count - 1; i >= 0; i--)
            {
                SquadMember member = selectedMembers[i];
                if (member != null && squadMembers.Contains(member) && IsMemberSelectable(member))
                {
                    continue;
                }

                selectedMembers.RemoveAt(i);
            }
        }

        private static bool IsMemberSelectable(SquadMember member)
        {
            return member != null && member.IsAvailableForOrders();
        }
    }
}