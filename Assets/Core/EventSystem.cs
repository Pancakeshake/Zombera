using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Core
{
    /// <summary>
    /// Lightweight event bus for decoupled game system communication.
    /// </summary>
    public sealed class EventSystem : MonoBehaviour, IGameSystem
    {
        public static EventSystem Instance { get; private set; }

        private readonly Dictionary<Type, Delegate> listenersByType = new Dictionary<Type, Delegate>();

        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
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

            // TODO: Register built-in event diagnostics and tracing.
        }

        public void Shutdown()
        {
            IsInitialized = false;
            listenersByType.Clear();

            // TODO: Persist event analytics if needed.
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
            Type eventType = typeof(TEvent);

            if (!listenersByType.TryGetValue(eventType, out Delegate eventDelegate))
            {
                return;
            }

            Action<TEvent> callback = eventDelegate as Action<TEvent>;
            callback?.Invoke(gameEvent);

            // TODO: Add queued event mode for deterministic replay/network sync.
        }

        public static void PublishGlobal<TEvent>(TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            Instance?.Publish(gameEvent);
        }
    }

    /// <summary>
    /// Marker interface for strongly typed game events.
    /// </summary>
    public interface IGameEvent
    {
    }
}