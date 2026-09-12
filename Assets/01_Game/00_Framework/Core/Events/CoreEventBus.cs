#region

using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Debugging.DebugLogging;

#endregion

namespace Zombera.Core
{
    /// <summary>
    ///     Lightweight event bus for decoupled game system communication.
    ///     Supports immediate dispatch and an optional queued mode for deterministic replay.
    /// </summary>
    public sealed class CoreEventBus : MonoBehaviour, IGameSystem
    {
        [SerializeField] private bool enableDiagnosticTracing;
        [SerializeField] private bool useQueuedMode;
        [SerializeField] [Range(32, 2048)] private int diagnosticHistoryLimit = 512;

        private readonly Dictionary<Type, Delegate> _listenersByType = new();
        private readonly Queue<IQueuedDispatch> _pendingQueue = new();
        private static readonly List<EventTrafficSample> DiagnosticHistory = new(512);
        private static CoreEventBus _instance;
        private static bool _warnedAboutMissingInstance;

        /// <summary>
        ///     Enables traffic samples even when local per-component diagnostic tracing is disabled.
        ///     Intended for editor tooling.
        /// </summary>
        public static bool GlobalDiagnosticTrafficEnabled { get; set; }

        /// <summary>
        ///     Raised whenever event traffic occurs (subscribe/unsubscribe/publish/dispatch).
        /// </summary>
        public static event Action<EventTrafficSample> EventTrafficObserved;

        public static bool HasInstance => _instance != null;

        public static CoreEventBus Instance
        {
            get => _instance;
            private set => _instance = value;
        }

        public bool UseQueuedMode => useQueuedMode;

        private void Awake()
        {
            var persistentRoot = transform.root.gameObject;

            if (_instance != null && _instance != this)
            {
                DebugLogger.LogWarning(
                    LogCategory.Events,
                    $"[CoreEventBus] Duplicate instance detected on '{name}'. Keeping '{_instance.name}' and destroying duplicate component.",
                    this);
                enabled = false;
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(persistentRoot);
        }

        private void Update()
        {
            if (UseQueuedMode && _pendingQueue.Count > 0) FlushQueue();
        }

        private void OnDestroy()
        {
            if (_instance == this) Instance = null;
        }

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            IsInitialized = true;

            if (enableDiagnosticTracing)
                DebugLogger.LogTrace(LogCategory.Events, "[CoreEventBus] Diagnostic tracing enabled.", this);
        }

        public void Shutdown()
        {
            IsInitialized = false;
            _listenersByType.Clear();

            while (_pendingQueue.Count > 0)
                _pendingQueue.Dequeue()?.Recycle();
        }

        public static EventTrafficSample[] SnapshotDiagnosticHistory()
        {
            lock (DiagnosticHistory)
            {
                return DiagnosticHistory.ToArray();
            }
        }

        public static void ClearDiagnosticHistory()
        {
            lock (DiagnosticHistory)
            {
                DiagnosticHistory.Clear();
            }
        }

        /// <summary>Flushes all queued events in FIFO order. Call once per frame when useQueuedMode is true.</summary>
        public void FlushQueue()
        {
            while (_pendingQueue.Count > 0)
            {
                var dispatch = _pendingQueue.Dequeue();
                if (dispatch == null) continue;

                try
                {
                    dispatch.Dispatch(this);
                }
                catch (Exception exception)
                {
                    LogDispatchException("QueuedEvent", exception);
                    dispatch.Recycle();
                }
            }
        }

        public void Subscribe<TEvent>(Action<TEvent> listener) where TEvent : struct, IGameEvent
        {
            var eventType = typeof(TEvent);

            if (!_listenersByType.TryGetValue(eventType, out var existingDelegate))
            {
                _listenersByType[eventType] = listener;
                RecordTraffic("Subscribe", eventType, 1);
                return;
            }

            _listenersByType[eventType] = Delegate.Combine(existingDelegate, listener);
            RecordTraffic("Subscribe", eventType, GetListenerCount(eventType));
        }

        public void Unsubscribe<TEvent>(Action<TEvent> listener) where TEvent : struct, IGameEvent
        {
            var eventType = typeof(TEvent);

            if (!_listenersByType.TryGetValue(eventType, out var existingDelegate)) return;

            var updated = Delegate.Remove(existingDelegate, listener);

            if (updated == null)
                _listenersByType.Remove(eventType);
            else
                _listenersByType[eventType] = updated;

            RecordTraffic("Unsubscribe", eventType, GetListenerCount(eventType));
        }

        public void Publish<TEvent>(TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            var eventType = typeof(TEvent);
            RecordTraffic("Publish", eventType, GetListenerCount(eventType));

            if (UseQueuedMode)
            {
                // Queue a pooled, typed dispatch entry to avoid per-event closure allocations.
                _pendingQueue.Enqueue(QueuedDispatch<TEvent>.Rent(gameEvent));
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
            var eventType = typeof(TEvent);

            if (!_listenersByType.TryGetValue(eventType, out var eventDelegate))
            {
                RecordTraffic("DispatchSkipped", eventType, 0);
                return;
            }

            if (eventDelegate is not Action<TEvent> callback) return;

            RecordTraffic("Dispatch", eventType, callback.GetInvocationList().Length);

            var listeners = callback.GetInvocationList();
            for (var i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] is not Action<TEvent> listener) continue;

                try
                {
                    listener.Invoke(gameEvent);
                }
                catch (Exception exception)
                {
                    LogDispatchException(eventType.Name, exception);
                }
            }
        }

        private void LogDispatchException(string eventName, Exception exception)
        {
            DebugLogger.LogWarning(
                LogCategory.Events,
                $"[CoreEventBus] Listener dispatch failed for '{eventName}': {exception.GetType().Name}: {exception.Message}",
                this);
        }

        private int GetListenerCount(Type eventType)
        {
            if (eventType == null) return 0;

            return _listenersByType.TryGetValue(eventType, out var eventDelegate)
                ? eventDelegate.GetInvocationList().Length
                : 0;
        }

        private void RecordTraffic(string operation, Type eventType, int listenerCount)
        {
            var shouldTrace = enableDiagnosticTracing || GlobalDiagnosticTrafficEnabled;
            if (!shouldTrace) return;

            var sample = new EventTrafficSample(
                operation,
                eventType != null ? eventType.Name : "UnknownEvent",
                listenerCount,
                _pendingQueue.Count,
                UseQueuedMode,
                Time.frameCount,
                Time.realtimeSinceStartup);

            if (enableDiagnosticTracing)
                DebugLogger.LogTrace(
                    LogCategory.Events,
                    "[CoreEventBus] " + operation + " " + sample.EventTypeName +
                    " listeners=" + listenerCount + " queued=" + sample.QueueDepth,
                    this);

            lock (DiagnosticHistory)
            {
                DiagnosticHistory.Add(sample);
                var historyLimit = Mathf.Max(32, diagnosticHistoryLimit);
                if (DiagnosticHistory.Count > historyLimit)
                    DiagnosticHistory.RemoveRange(0, DiagnosticHistory.Count - historyLimit);
            }

            EventTrafficObserved?.Invoke(sample);
        }

        private interface IQueuedDispatch
        {
            void Dispatch(CoreEventBus bus);
            void Recycle();
        }

        private sealed class QueuedDispatch<TEvent> : IQueuedDispatch where TEvent : struct, IGameEvent
        {
            private static readonly Stack<QueuedDispatch<TEvent>> Pool = new();

            private TEvent _eventPayload;

            public static IQueuedDispatch Rent(TEvent gameEvent)
            {
                var queuedDispatch = Pool.Count > 0
                    ? Pool.Pop()
                    : new QueuedDispatch<TEvent>();

                queuedDispatch._eventPayload = gameEvent;
                return queuedDispatch;
            }

            public void Dispatch(CoreEventBus bus)
            {
                bus.DispatchImmediate(_eventPayload);
                Recycle();
            }

            public void Recycle()
            {
                _eventPayload = default;
                Pool.Push(this);
            }
        }
    }

    public readonly struct EventTrafficSample
    {
        public EventTrafficSample(
            string operation,
            string eventTypeName,
            int listenerCount,
            int queueDepth,
            bool queuedMode,
            int frame,
            float realtimeSinceStartup)
        {
            Operation = operation;
            EventTypeName = eventTypeName;
            ListenerCount = listenerCount;
            QueueDepth = queueDepth;
            QueuedMode = queuedMode;
            Frame = frame;
            RealtimeSinceStartup = realtimeSinceStartup;
        }

        public string Operation { get; }
        public string EventTypeName { get; }
        public int ListenerCount { get; }
        public int QueueDepth { get; }
        public bool QueuedMode { get; }
        public int Frame { get; }
        public float RealtimeSinceStartup { get; }
    }

    /// <summary>
    ///     Marker interface for strongly typed game events.
    /// </summary>
    public interface IGameEvent
    {
    }
}