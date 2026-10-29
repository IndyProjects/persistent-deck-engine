using System;
using System.Collections.Generic;
using UnityEngine;

/// Manages active gesture sessions and routes completed sessions to the coordinator.
public class GestureSessionManager
{
    public event Action<IGestureSession> SessionEnded;

    private readonly Dictionary<GestureType, IGestureSession> _active
        = new Dictionary<GestureType, IGestureSession>();

    /// Live deck size, read when a shuffle session starts. Set by InputCoordinator.
    public Func<int> DeckSize  { get; set; } = () => DeckEngine.DeckSize;
    /// Live number of collectable piles, read when a collect session starts. Set by InputCoordinator.
    public Func<int> PileCount { get; set; } = () => 1;

    public bool IsActive(GestureType key) => _active.ContainsKey(key);

    public void OnGestureEvent(GestureEvent e)
    {
        switch (e.Type)
        {
            case GestureType.Cut:
                FireOneShot(GestureType.Cut, e);
                break;

            case GestureType.RiffleStart:
            {
                // Register the new session before cancelling the old one so listeners of
                // SessionEnded see a riffle still in progress.
                _active.TryGetValue(GestureType.RiffleStart, out var previous);
                var session = new RiffleSession(DeckSize());
                session.AddEvent(e);
                _active[GestureType.RiffleStart] = session;
                if (previous != null)
                {
                    previous.Cancel();
                    SessionEnded?.Invoke(previous);
                }
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
                    _active[GestureType.OverhandGrab] = new OverhandSession(DeckSize());
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
                    _active[GestureType.CollectPile] = new CollectSession(PileCount());
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
            s = new OverhandSession(DeckSize());
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
}
