using System;
using System.Collections.Generic;

/// Routes raw events from all IInputReaders to the registered IGestureRecognizers.
public class GestureRecognizerDispatcher
{
    public event Action<GestureEvent> GestureRecognized;

    private readonly List<IGestureRecognizer> _recognizers = new List<IGestureRecognizer>();

    public void Register(IGestureRecognizer recognizer)
    {
        _recognizers.Add(recognizer);
        recognizer.GestureRecognized += e => GestureRecognized?.Invoke(e);
    }

    public void Subscribe(IInputReader reader)
    {
        reader.PointerEvent += OnPointer;
        reader.AxisEvent    += OnAxis;
        reader.ButtonEvent  += OnButton;
    }

    private void OnPointer(RawPointerEvent e)
    {
        foreach (var r in _recognizers) r.OnPointerEvent(e);
    }

    private void OnAxis(RawAxisEvent e)
    {
        foreach (var r in _recognizers) r.OnAxisEvent(e);
    }

    private void OnButton(RawButtonEvent e)
    {
        foreach (var r in _recognizers) r.OnButtonEvent(e);
    }
}
