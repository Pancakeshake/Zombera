using System;
using UnityEngine;
using Zombera.Core;

namespace Zombera.Systems
{
    public sealed partial class SaveManager
    {
        public Exception LastDeferredRestoreError { get; private set; }

        public bool TryGetDeferredProceduralWorld(out ProceduralWorldSaveData data)
        {
            data = _deferredRestoreData?.proceduralWorld;
            return _hasDeferredRestore && data != null && data.hasData;
        }

        private void InvokeProviderSave(ISaveProvider provider, GameSaveData saveData)
        {
            try
            {
                provider.OnSave(saveData);
            }
            catch (Exception ex)
            {
                HandleProviderFailure(provider, "save", ex);
            }
        }

        private void InvokeProviderLoad(ISaveProvider provider, GameSaveData saveData)
        {
            try
            {
                provider.OnLoad(saveData);
            }
            catch (Exception ex)
            {
                HandleProviderFailure(provider, "load", ex);
            }
        }

        private static void HandleProviderFailure(ISaveProvider provider, string operation, Exception ex)
        {
            if (provider is ICriticalSaveProvider)
                throw new CriticalSaveProviderException(provider.GetType(), operation, ex);

            Debug.LogError(
                $"[SaveManager] Provider {provider.GetType().Name} failed during {operation}: {ex.Message}");
        }
    }
}
