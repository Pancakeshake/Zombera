#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Debugging.DebugLogging;
using Zombera.Debugging.DebugMenu;
using Zombera.Debugging.DebugTools;

#endregion

namespace Zombera.Debugging
{
    /// <summary>
    ///     Global entry point for debug mode orchestration.
    ///     Responsibilities:
    ///     - Toggle debug mode
    ///     - Enable/disable debug tools
    ///     - Manage debug menu visibility
    ///     - Register/unregister debug tool modules
    /// </summary>
    public sealed class DebugManager : MonoBehaviour, IDebugManagerAccessor
    {
        [Header("State")] [SerializeField] private bool debugEnabled = true;

        [SerializeField] private bool autoDiscoverTools = true;

        [Header("Settings")] [SerializeField] private DebugSettings debugSettings;

        [Header("Menu")] [SerializeField] private DebugMenuController debugMenuController;

        private readonly List<IDebugTool> _registeredTools = new();
        public static DebugManager Instance { get; private set; }

        public bool DebugEnabled => debugEnabled;
        public DebugSettings Settings => debugSettings;
        public bool IsDebugMenuVisible => debugMenuController != null && debugMenuController.IsMenuVisible;

        private void Awake()
        {
            var persistentRoot = transform.root.gameObject;

            if (Instance != null && Instance != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }

            Instance = this;
            DebugManagerAccessor.Instance = this;
            DontDestroyOnLoad(persistentRoot);
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (GetComponent<QuickTimeDebugMenu>() == null)
                gameObject.AddComponent<QuickTimeDebugMenu>();

            if (autoDiscoverTools) DiscoverToolsInScene();

            debugMenuController?.Initialize(this);
            ApplyDebugToolStates();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (ReferenceEquals(DebugManagerAccessor.Instance, this)) DebugManagerAccessor.Instance = null;
            Instance = null;
        }

        public event Action<bool> DebugModeChanged;

        public void RegisterDebugTool(IDebugTool tool)
        {
            if (tool == null || _registeredTools.Contains(tool)) return;

            _registeredTools.Add(tool);
            tool.SetToolEnabled(debugEnabled);
        }

        public void UnregisterDebugTool(IDebugTool tool)
        {
            if (tool == null) return;

            _registeredTools.Remove(tool);
        }

        public void ToggleDebug()
        {
            SetDebugEnabled(!debugEnabled);
        }

        public void SetDebugEnabled(bool isDebugEnabled)
        {
            debugEnabled = isDebugEnabled;
            ApplyDebugToolStates();
            DebugModeChanged?.Invoke(debugEnabled);

            if (!debugEnabled) debugMenuController?.SetMenuVisible(false);

            DebugLogger.Log(LogCategory.Debug, $"Debug mode {(debugEnabled ? "enabled" : "disabled")}", this);
        }

        public void ToggleDebugMenu()
        {
            if (!debugEnabled) return;

            if (debugMenuController == null) return;

            debugMenuController.SetMenuVisible(!debugMenuController.IsMenuVisible);
        }

        public void ToggleSlowMotion()
        {
            if (debugSettings == null) return;

            debugSettings.enableSlowMotion = !debugSettings.enableSlowMotion;
            Time.timeScale = debugSettings.enableSlowMotion ? debugSettings.slowMotionScale : 1f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            DebugLogger.Log(LogCategory.Debug, $"Slow motion {(debugSettings.enableSlowMotion ? "ON" : "OFF")}", this);
        }

        public void ToggleGodMode()
        {
            if (debugSettings == null) return;

            debugSettings.enableGodMode = !debugSettings.enableGodMode;
            DebugLogger.Log(LogCategory.Debug, $"God mode {(debugSettings.enableGodMode ? "ON" : "OFF")}", this);
        }

        public void ToggleAIDebugVisuals()
        {
            if (debugSettings == null) return;

            debugSettings.showAIStates = !debugSettings.showAIStates;
            DebugLogger.Log(LogCategory.AI, $"AI debug visuals {(debugSettings.showAIStates ? "ON" : "OFF")}", this);
        }

        private void ApplyDebugToolStates()
        {
            for (var i = _registeredTools.Count - 1; i >= 0; i--)
            {
                var tool = _registeredTools[i];

                if (tool == null)
                {
                    _registeredTools.RemoveAt(i);
                    continue;
                }

                tool.SetToolEnabled(debugEnabled);
            }
        }

        private void DiscoverToolsInScene()
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is not IDebugTool debugTool) continue;

                RegisterDebugTool(debugTool);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;

            if (!autoDiscoverTools) return;

            DiscoverToolsInScene();
        }
    }
}