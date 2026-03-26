using System;
using System.Collections.Generic;
using UnityEngine;

/// Manages active gesture sessions and routes completed sessions to the coordinator.
public class GestureSessionManager
{
    public event Action<IGestureSession> SessionEnded;

    private readonly Dictionary<GestureType, IGestureSession> _active
        = new Dictionary<GestureType, IGestureSession>();

    public int ActivePileCount { get; set; } = 1;

    public void OnGestureEvent(GestureEvent e)
    {
        switch (e.Type)
        {
            case GestureType.Cut:
                FireOneShot(GestureType.Cut, e);
                break;

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
                    TryCloseSession(GestureType.RiffleStart, rs);
                }
                else
                    Debug.LogWarning("[GestureSessionManager] RiffleDrop received without active RiffleSession.");
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
                    TryCloseSession(GestureType.OverhandGrab, os);
                }
                break;
            }

            case GestureType.DealStart:
                FireOneShot(GestureType.DealStart, e);
                break;

            case GestureType.CollectPile:
            {
                if (!_active.ContainsKey(GestureType.CollectPile))
                    _active[GestureType.CollectPile] = new CollectSession(ActivePileCount);
                var cs = (CollectSession)_active[GestureType.CollectPile];
                cs.AddEvent(e);
                TryCloseSession(GestureType.CollectPile, cs);
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
            TryCloseSession(GestureType.OverhandGrab, os);
        }
    }

    private void FireOneShot(GestureType type, GestureEvent e)
    {
        var session = new GestureSession(type);
        session.AddEvent(e);
        SessionEnded?.Invoke(session);
    }

    private void TryCloseSession(GestureType key, IGestureSession session)
    {
        if (session.IsComplete || session.IsCancelled)
        {
            _active.Remove(key);
            SessionEnded?.Invoke(session);
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
