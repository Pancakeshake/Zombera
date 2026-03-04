using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zombera.Debugging.DebugMenu;
using Zombera.Debugging.DebugLogging;

namespace Zombera.Debugging
{
    /// <summary>
    /// Global entry point for debug mode orchestration.
    /// Responsibilities:
    /// - Toggle debug mode
    /// - Enable/disable debug tools
    /// - Manage debug menu visibility
    /// - Register/unregister debug tool modules
    /// </summary>
    public sealed class DebugManager : MonoBehaviour
    {
        public static DebugManager Instance { get; private set; }

        [Header("State")]
        [SerializeField] private bool debugEnabled = true;
        [SerializeField] private bool autoDiscoverTools = true;

        [Header("Settings")]
        [SerializeField] private DebugSettings debugSettings;

        [Header("Menu")]
        [SerializeField] private DebugMenuController debugMenuController;

        private readonly List<IDebugTool> registeredTools = new List<IDebugTool>();

        public bool DebugEnabled => debugEnabled;
        public DebugSettings Settings => debugSettings;

        public event Action<bool> DebugModeChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (autoDiscoverTools)
            {
                DiscoverToolsInScene();
            }

            debugMenuController?.Initialize(this);
            ApplyDebugToolStates();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        public void RegisterDebugTool(IDebugTool tool)
        {
            if (tool == null || registeredTools.Contains(tool))
            {
                return;
            }

            registeredTools.Add(tool);
            tool.SetToolEnabled(debugEnabled);
        }

        public void UnregisterDebugTool(IDebugTool tool)
        {
            if (tool == null)
            {
                return;
            }

            registeredTools.Remove(tool);
        }

        public void ToggleDebug()
        {
            SetDebugEnabled(!debugEnabled);
        }

        public void SetDebugEnabled(bool enabled)
        {
            debugEnabled = enabled;
            ApplyDebugToolStates();
            DebugModeChanged?.Invoke(debugEnabled);

            if (!debugEnabled)
            {
                debugMenuController?.SetMenuVisible(false);
            }

            DebugLogger.Log(LogCategory.Debug, $"Debug mode {(debugEnabled ? "enabled" : "disabled")}", this);
        }

        public void ToggleDebugMenu()
        {
            if (!debugEnabled)
            {
                return;
            }

            if (debugMenuController == null)
            {
                return;
            }

            debugMenuController.SetMenuVisible(!debugMenuController.IsMenuVisible);
        }

        public void ToggleSlowMotion()
        {
            if (debugSettings == null)
            {
                return;
            }

            debugSettings.enableSlowMotion = !debugSettings.enableSlowMotion;
            Time.timeScale = debugSettings.enableSlowMotion ? debugSettings.slowMotionScale : 1f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            DebugLogger.Log(LogCategory.Debug, $"Slow motion {(debugSettings.enableSlowMotion ? "ON" : "OFF")}", this);
        }

        public void ToggleGodMode()
        {
            if (debugSettings == null)
            {
                return;
            }

            debugSettings.enableGodMode = !debugSettings.enableGodMode;
            DebugLogger.Log(LogCategory.Debug, $"God mode {(debugSettings.enableGodMode ? "ON" : "OFF")}", this);
        }

        public void ToggleAIDebugVisuals()
        {
            if (debugSettings == null)
            {
                return;
            }

            debugSettings.showAIStates = !debugSettings.showAIStates;
            DebugLogger.Log(LogCategory.AI, $"AI debug visuals {(debugSettings.showAIStates ? "ON" : "OFF")}", this);
        }

        private void ApplyDebugToolStates()
        {
            for (int i = registeredTools.Count - 1; i >= 0; i--)
            {
                IDebugTool tool = registeredTools[i];

                if (tool == null)
                {
                    registeredTools.RemoveAt(i);
                    continue;
                }

                tool.SetToolEnabled(debugEnabled);
            }
        }

        private void DiscoverToolsInScene()
        {
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IDebugTool debugTool)
                {
                    RegisterDebugTool(debugTool);
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;

            if (!autoDiscoverTools)
            {
                return;
            }

            DiscoverToolsInScene();
        }
    }

    /// <summary>
    /// Shared interface for modular debug tools.
    /// </summary>
    public interface IDebugTool
    {
        string ToolName { get; }
        bool IsToolEnabled { get; }
        void SetToolEnabled(bool enabled);
    }
}