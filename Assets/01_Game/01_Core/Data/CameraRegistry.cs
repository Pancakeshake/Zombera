using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    /// Provides efficient access to the Main Camera, avoiding expensive GameObject.FindWithTag calls.
    /// </summary>
    public static class CameraRegistry
    {
        private static Camera _main;
        private static int _lastUpdateFrame = -1;

        public static Camera Main
        {
            get
            {
                if (_lastUpdateFrame == Time.frameCount && _main != null)
                    return _main;

                _main = Camera.main;
                _lastUpdateFrame = Time.frameCount;
                return _main;
            }
        }

        /// <summary>
        /// Explicitly sets the main camera, bypassing the tag search.
        /// </summary>
        public static void RegisterMain(Camera camera)
        {
            _main = camera;
            _lastUpdateFrame = Time.frameCount;
        }
    }
}
