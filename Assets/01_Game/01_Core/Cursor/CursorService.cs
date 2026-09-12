#region

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace Zombera.Systems
{
    /// <summary>
    ///     Priority bands for cursor lock/visibility requests. Higher values win.
    /// </summary>
    public enum CursorContextPriority
    {
        Gameplay = 0,
        BuildMode = 10,
        MenuModal = 50,
        SystemOverride = 100
    }

    /// <summary>
    ///     Resolved lock/visibility state applied to the OS cursor.
    /// </summary>
    public readonly struct CursorPresentation : IEquatable<CursorPresentation>
    {
        public static CursorPresentation FreePointer => new(true, CursorLockMode.None);

        public static CursorPresentation CapturedPointer => new(false, CursorLockMode.Locked);

        public readonly bool Visible;
        public readonly CursorLockMode LockMode;

        public CursorPresentation(bool visible, CursorLockMode lockMode)
        {
            Visible = visible;
            LockMode = lockMode;
        }

        public bool Equals(CursorPresentation other)
        {
            return Visible == other.Visible && LockMode == other.LockMode;
        }

        public override bool Equals(object obj)
        {
            return obj is CursorPresentation other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Visible.GetHashCode() * 397) ^ (int)LockMode;
            }
        }
    }

    /// <summary>
    ///     Handle returned by <see cref="CursorService.Request"/>. Dispose to release the request.
    /// </summary>
    public readonly struct CursorStateHandle : IDisposable
    {
        private readonly int _id;

        internal CursorStateHandle(int id)
        {
            _id = id;
        }

        public bool IsValid => _id > 0;

        public void Dispose()
        {
            CursorService.Release(_id);
        }
    }

    /// <summary>
    ///     Single authority for OS cursor lock mode and visibility.
    ///     Icon/hotspot selection is orchestrated by <see cref="CursorManager"/>.
    /// </summary>
    public static partial class CursorService
    {
        private struct ActiveRequest
        {
            public int Id;
            public CursorContextPriority Priority;
            public CursorPresentation Presentation;
        }

        private static readonly List<ActiveRequest> ActiveRequests = new();
        private static int _nextRequestId;
        private static CursorPresentation _appliedPresentation = CursorPresentation.FreePointer;
        private static bool _hasAppliedPresentation;

        public static CursorPresentation CurrentPresentation => _hasAppliedPresentation
            ? _appliedPresentation
            : CursorPresentation.FreePointer;

        public static int ActiveRequestCount => ActiveRequests.Count;

        public static CursorStateHandle Request(CursorContextPriority priority, CursorPresentation presentation)
        {
            var id = ++_nextRequestId;
            ActiveRequests.Add(new ActiveRequest
            {
                Id = id,
                Priority = priority,
                Presentation = presentation
            });
            Reconcile();
            return new CursorStateHandle(id);
        }

        public static CursorStateHandle RequestMenuModal()
        {
            return Request(CursorContextPriority.MenuModal, CursorPresentation.FreePointer);
        }

        public static CursorStateHandle RequestGameplay()
        {
            return Request(CursorContextPriority.Gameplay, CursorPresentation.FreePointer);
        }

        public static CursorStateHandle RequestCapturedPointer(
            CursorContextPriority priority = CursorContextPriority.SystemOverride)
        {
            return Request(priority, CursorPresentation.CapturedPointer);
        }

        public static void Release(int requestId)
        {
            if (requestId <= 0) return;

            for (var i = ActiveRequests.Count - 1; i >= 0; i--)
            {
                if (ActiveRequests[i].Id != requestId) continue;

                ActiveRequests.RemoveAt(i);
                Reconcile();
                return;
            }
        }

        /// <summary>
        ///     Applies the default free pointer when no requests are active.
        /// </summary>
        public static void EnsureDefaultState()
        {
            Reconcile();
        }

        /// <summary>
        ///     Applies a hardware cursor icon. Only cursor icon owners should call this.
        /// </summary>
        public static void ApplyCursorIcon(Texture2D texture, Vector2 hotspot, CursorMode mode = CursorMode.Auto)
        {
            Cursor.SetCursor(texture, hotspot, mode);
        }

        private static void Reconcile()
        {
            ApplyPresentationIfChanged(ResolvePresentation());
        }

        private static CursorPresentation ResolvePresentation()
        {
            if (ActiveRequests.Count == 0) return CursorPresentation.FreePointer;

            var bestIndex = 0;
            for (var i = 1; i < ActiveRequests.Count; i++)
            {
                var candidate = ActiveRequests[i];
                var best = ActiveRequests[bestIndex];

                if (candidate.Priority > best.Priority
                    || (candidate.Priority == best.Priority && candidate.Id > best.Id))
                    bestIndex = i;
            }

            return ActiveRequests[bestIndex].Presentation;
        }

        private static void ApplyPresentationIfChanged(CursorPresentation presentation)
        {
            if (_hasAppliedPresentation && _appliedPresentation.Equals(presentation)) return;

            var previous = _hasAppliedPresentation ? _appliedPresentation : CursorPresentation.FreePointer;
            _appliedPresentation = presentation;
            _hasAppliedPresentation = true;

            if (Cursor.visible != presentation.Visible) Cursor.visible = presentation.Visible;
            if (Cursor.lockState != presentation.LockMode) Cursor.lockState = presentation.LockMode;

            LogPresentationTransition(previous, presentation);
        }
    }
}
