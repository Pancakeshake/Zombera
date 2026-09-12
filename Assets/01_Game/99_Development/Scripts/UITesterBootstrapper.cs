using UnityEngine;
using Zombera.Core;
using Zombera.Systems;
using System.Collections.Generic;

namespace Zombera.Testing
{
    public class UITesterBootstrapper : MonoBehaviour
    {
        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Playing);
            }

            // Force select the first unit to populate UI
            if (SquadManager.Instance != null)
            {
                SquadManager.Instance.RefreshSquadRoster();
                var members = SquadManager.Instance.SquadMembers;
                if (members.Count > 0)
                {
                    SquadManager.Instance.SetSelectedMembers(new List<SquadMember> { members[0] });
                }
            }
        }
    }
}
