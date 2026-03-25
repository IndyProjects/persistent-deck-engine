using System;
using System.Collections.Generic;
using UnityEngine;

/// Manages active gesture sessions and routes completed sessions to the coordinator.
public class GestureSessionManager
{
    /// Raised when a session completes (or is cancelled).
    public event Action<IGestureSession> SessionEnded;

    private readonly Dictionary<GestureType, IGestureSession> _active
        = new Dictionary<GestureType, IGestureSession>();

    // How many piles are currently in play set by the coordinator before collect gestures start.
    public int ActivePileCount { get; set; } = 1;

    public void OnGestureEvent(GestureEvent e)
    {
        switch (e.Type)
        {
            case GestureType.Cut:
            {
                // session open and immediately close.
                var session = new GestureSession(GestureType.Cut);
                session.AddEvent(e);
                SessionEnded?.Invoke(session);
                break;
            }

            case GestureType.RiffleStart:
            {
                CancelExisting(GestureType.RiffleStart);
                var session = new RiffleSession();
                session.AddEvent(e);
                _active[GestureType.RiffleStart] = session;
                break;
            }

            case GestureType.RiffleDrop:
            {
                if (_active.TryGetValue(GestureType.RiffleStart, out var s) && s is RiffleSession rs)
                {
                    rs.AddEvent(e);
                    if (rs.IsComplete || rs.IsCancelled)
                    {
                        _active.Remove(GestureType.RiffleStart);
                        SessionEnded?.Invoke(rs);
                    }
                }
                else
                {
                    Debug.LogWarning("[GestureSessionManager] RiffleDrop received without active RiffleSession.");
                }
                break;
            }

            case GestureType.OverhandGrab:
            {
                if (!_active.ContainsKey(GestureType.OverhandGrab))
                    _active[GestureType.OverhandGrab] = new OverhandSession();
                _active[GestureType.OverhandGrab].AddEvent(e);
                break;
            }

            case GestureType.OverhandRelease:
            {
                if (_active.TryGetValue(GestureType.OverhandGrab, out var s) && s is OverhandSession os)
                {
                    os.AddEvent(e);
                    if (os.IsComplete || os.IsCancelled)
                    {
                        _active.Remove(GestureType.OverhandGrab);
                        SessionEnded?.Invoke(os);
                    }
                }
                break;
            }

            case GestureType.DealStart:
            {
                var session = new GestureSession(GestureType.DealStart);
                session.AddEvent(e);
                SessionEnded?.Invoke(session);
                break;
            }

            case GestureType.DealStroke:
            {
                // Decorative — logged but does not trigger a session end.
                break;
            }

            case GestureType.CollectPile:
            {
                if (!_active.ContainsKey(GestureType.CollectPile))
                    _active[GestureType.CollectPile] = new CollectSession(ActivePileCount);
                _active[GestureType.CollectPile].AddEvent(e);
                var cs = _active[GestureType.CollectPile];
                if (cs.IsComplete || cs.IsCancelled)
                {
                    _active.Remove(GestureType.CollectPile);
                    SessionEnded?.Invoke(cs);
                }
                break;
            }
        }
    }

    /// Force-add a fixed overhand chunk.
    public void AddOverhandChunk(int size)
    {
        if (!_active.TryGetValue(GestureType.OverhandGrab, out var s))
        {
            s = new OverhandSession();
            _active[GestureType.OverhandGrab] = s;
        }
        if (s is OverhandSession os)
        {
            os.AddFixedChunk(size);
            if (os.IsComplete || os.IsCancelled)
            {
                _active.Remove(GestureType.OverhandGrab);
                SessionEnded?.Invoke(os);
            }
        }
    }

    private void CancelExisting(GestureType type)
    {
        if (_active.TryGetValue(type, out var existing))
        {
            existing.Cancel();
            _active.Remove(type);
            SessionEnded?.Invoke(existing);
        }
    }
}
