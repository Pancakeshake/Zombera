#region

using UnityEngine;

#endregion

namespace Zombera.Core
{
    [CreateAssetMenu(fileName = "UmaAnimationDefaults", menuName = "Zombera/UMA/Animation Defaults")]
    public sealed class UmaAnimationDefaults : ScriptableObject
    {
        [Tooltip("Default humanoid controller. Use Assets/Animations/Players/Player_Default.controller.")]
        [SerializeField] private RuntimeAnimatorController defaultHumanoidController;

        public RuntimeAnimatorController DefaultHumanoidController => defaultHumanoidController;
    }
}
