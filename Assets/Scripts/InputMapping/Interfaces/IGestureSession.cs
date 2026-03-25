using System.Collections.Generic;

public interface IGestureSession
{
    GestureType        GestureType  { get; }
    bool               IsComplete   { get; }
    bool               IsCancelled  { get; }
    List<GestureEvent> Events       { get; }

    void AddEvent(GestureEvent e);
    void Cancel();
}
