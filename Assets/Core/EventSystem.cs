using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    /// Lightweight event bus for decoupled game system communication.
    /// Supports immediate dispatch and an optional queued mode for deterministic replay.
    /// </summary>
    public sealed class EventSystem : MonoBehaviour, IGameSystem
    {
        public static EventSystem Instance { get; private set; }

        private readonly Dictionary<Type, Delegate> listenersByType = new Dictionary<Type, Delegate>();
        private readonly Queue<Action> pendingQueue = new Queue<Action>();

        [SerializeField] private bool enableDiagnosticTracing;
        [SerializeField] private bool useQueuedMode;

        public bool IsInitialized { get; private set; }
        public bool UseQueuedMode => useQueuedMode;

        private void Awake()
        {
            GameObject persistentRoot = transform.root.gameObject;

            if (Instance != null && Instance != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(persistentRoot);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Initialize()
        {
            IsInitialized = true;

            if (enableDiagnosticTracing)
            {
                Debug.Log("[EventSystem] Diagnostic tracing enabled.");
            }
        }

        public void Shutdown()
        {
            IsInitialized = false;
            listenersByType.Clear();
            pendingQueue.Clear();
        }

        /// <summary>Flushes all queued events in FIFO order. Call once per frame when useQueuedMode is true.</summary>
        public void FlushQueue()
        {
            while (pendingQueue.Count > 0)
            {
                Action dispatch = pendingQueue.Dequeue();
                dispatch?.Invoke();
            }
        }

        public void Subscribe<TEvent>(Action<TEvent> listener) where TEvent : struct, IGameEvent
        {
            Type eventType = typeof(TEvent);

            if (!listenersByType.TryGetValue(eventType, out Delegate existingDelegate))
            {
                listenersByType[eventType] = listener;
                return;
            }

            listenersByType[eventType] = Delegate.Combine(existingDelegate, listener);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> listener) where TEvent : struct, IGameEvent
        {
            Type eventType = typeof(TEvent);

            if (!listenersByType.TryGetValue(eventType, out Delegate existingDelegate))
            {
                return;
            }

            Delegate updated = Delegate.Remove(existingDelegate, listener);

            if (updated == null)
            {
                listenersByType.Remove(eventType);
            }
            else
            {
                listenersByType[eventType] = updated;
            }
        }

        public void Publish<TEvent>(TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            if (enableDiagnosticTracing)
            {
                Debug.Log($"[EventSystem] Publishing {typeof(TEvent).Name}");
            }

            if (useQueuedMode)
            {
                // Capture value into closure for deferred dispatch.
                TEvent captured = gameEvent;
                pendingQueue.Enqueue(() => DispatchImmediate(captured));
                return;
            }

            DispatchImmediate(gameEvent);
        }

        public static void PublishGlobal<TEvent>(TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            Instance?.Publish(gameEvent);
        }

        private void DispatchImmediate<TEvent>(TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            Type eventType = typeof(TEvent);

            if (!listenersByType.TryGetValue(eventType, out Delegate eventDelegate))
            {
                return;
            }

            Action<TEvent> callback = eventDelegate as Action<TEvent>;
            callback?.Invoke(gameEvent);
        }
    }

    /// <summary>
    /// Marker interface for strongly typed game events.
    /// </summary>
    public interface IGameEvent
    {
    }
}