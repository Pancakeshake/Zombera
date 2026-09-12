#region

using UnityEngine;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Runtime access to the active <see cref="MovementGroundingProfile"/>.
    /// </summary>
    public static class MovementGroundingSettings
    {
        private const string DefaultResourcePath = "DefaultMovementGroundingProfile";

        private static MovementGroundingProfile _activeProfile;

        public static MovementGroundingProfile Active
        {
            get
            {
                if (_activeProfile != null) return _activeProfile;

                _activeProfile = Resources.Load<MovementGroundingProfile>(DefaultResourcePath);
                if (_activeProfile == null)
                    _activeProfile = ScriptableObject.CreateInstance<MovementGroundingProfile>();

                _activeProfile.RebuildCache();
                return _activeProfile;
            }
        }

        public static void SetActiveProfile(MovementGroundingProfile profile)
        {
            _activeProfile = profile;
            _activeProfile?.RebuildCache();
        }
    }
}
