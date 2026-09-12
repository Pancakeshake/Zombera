#region

using UMA.CharacterSystem;
using UnityEngine;

#endregion

namespace Zombera.Core
{
    /// <summary>
    ///     Ensures UMA avatars have an animator controller before mesh generation so UMAData.Validate does not warn.
    /// </summary>
    public static class UmaAnimationControllerUtility
    {
        private const string DefaultsResourcePath = "UmaAnimationDefaults";

        private static RuntimeAnimatorController _cachedDefaultController;

        public static bool EnsureAnimationController(DynamicCharacterAvatar avatar)
        {
            if (avatar == null) return false;

            var controller = ResolveController(avatar);
            if (controller == null) return false;

            var changed = false;

            if (avatar.animationController != controller)
            {
                avatar.animationController = controller;
                changed = true;
            }

            if (avatar.raceAnimationControllers.defaultAnimationController == null)
            {
                avatar.raceAnimationControllers.defaultAnimationController = controller;
                changed = true;
            }

            if (avatar.umaData != null && avatar.umaData.animationController == null)
            {
                avatar.umaData.animationController = controller;
                changed = true;
            }

            return changed;
        }

        private static RuntimeAnimatorController ResolveController(DynamicCharacterAvatar avatar)
        {
            if (avatar.animationController != null)
                return avatar.animationController;

            var animator = avatar.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                return animator.runtimeAnimatorController;

            if (avatar.raceAnimationControllers.defaultAnimationController != null)
                return avatar.raceAnimationControllers.defaultAnimationController;

            if (avatar.activeRace?.racedata != null)
            {
                var raceController = avatar.raceAnimationControllers.GetAnimatorForRace(avatar.activeRace.name);
                if (raceController != null)
                    return raceController;
            }

            return GetDefaultController();
        }

        private static RuntimeAnimatorController GetDefaultController()
        {
            if (_cachedDefaultController != null)
                return _cachedDefaultController;

            var defaults = Resources.Load<UmaAnimationDefaults>(DefaultsResourcePath);
            if (defaults != null && defaults.DefaultHumanoidController != null)
            {
                _cachedDefaultController = defaults.DefaultHumanoidController;
                return _cachedDefaultController;
            }

            return null;
        }
    }
}
