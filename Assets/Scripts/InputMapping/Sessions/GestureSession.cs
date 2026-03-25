using System.Collections.Generic;

/// Base session class. Accumulates GestureEvents for a single gesture sequence.
public class GestureSession : IGestureSession
{
    public GestureType        GestureType { get; }
    public bool               IsComplete  { get; protected set; }
    public bool               IsCancelled { get; private set; }
    public List<GestureEvent> Events      { get; } = new List<GestureEvent>();

    public GestureSession(GestureType type) => GestureType = type;

    public virtual void AddEvent(GestureEvent e)
    {
        if (IsComplete || IsCancelled) return;
        Events.Add(e);
    }

    public void Cancel()
    {
        IsCancelled = true;
        IsComplete  = false;
    }

    protected void Complete() => IsComplete = true;
}
