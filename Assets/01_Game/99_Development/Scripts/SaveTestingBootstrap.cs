using UnityEngine;
using Zombera.Core;
using Zombera.Systems;

namespace Zombera.Testing
{
    public class SaveTestingBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var saveManager = SaveManager.ResolveActive();
            var saveSystem = Object.FindFirstObjectByType<SaveSystem>();

            if (saveManager != null && saveSystem != null)
            {
                // Ensure SaveManager has a reference to SaveSystem
                var field = typeof(SaveManager).GetField("saveSystem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(saveManager, saveSystem);
                }
                else
                {
                    Debug.LogError("[SaveTestingBootstrap] Could not find 'saveSystem' field in SaveManager.");
                }

                // Ensure SaveSystem and SaveManager are initialized
                if (!saveSystem.IsInitialized)
                {
                    saveSystem.Initialize();
                }

                if (!saveManager.IsInitialized)
                {
                    saveManager.Initialize();
                }
                
                Debug.Log("[SaveTestingBootstrap] SaveSystem and SaveManager initialized.");
            }
            else
            {
                if (saveManager == null) Debug.LogError("[SaveTestingBootstrap] SaveManager not found in scene.");
                if (saveSystem == null) Debug.LogError("[SaveTestingBootstrap] SaveSystem not found in scene.");
            }
        }
    }
}
