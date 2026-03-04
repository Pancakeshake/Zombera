using UnityEngine;

namespace Zombera.Systems
{
    /// <summary>
    /// Handles exploration follow behavior for squad members.
    /// </summary>
    public sealed class FollowController : MonoBehaviour
    {
        [SerializeField] private FollowStyle followStyle = FollowStyle.Loose;
        [SerializeField] private float followDistance = 2.5f;

        public FollowStyle CurrentFollowStyle => followStyle;
        public float FollowDistance => followDistance;

        public void SetFollowStyle(FollowStyle style)
        {
            followStyle = style;

            // TODO: Recompute slot offsets/spacing for active style.
        }

        public void TickFollow(Vector3 leaderPosition, Vector3 leaderForward)
        {
            // TODO: Evaluate style and move toward desired offset position.
            // TODO: Integrate obstacle avoidance and sprint catch-up behavior.
            _ = leaderPosition;
            _ = leaderForward;
        }
    }

    public enum FollowStyle
    {
        Free,
        Loose,
        OrderedMarch
    }
}