using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// Reads gamepad input and emits axis/button events.
/// Applies dead-zone and commit threshold from InputConstants.
public class GamepadInputReader : IInputReader
{
    public event Action<RawPointerEvent> PointerEvent;
    public event Action<RawAxisEvent>    AxisEvent;
    public event Action<RawButtonEvent>  ButtonEvent;

    private bool  _enabled;
    private int   _cutPoint        = 26;
    private bool  _leftTriggerHeld;
    private bool  _rightTriggerHeld;

    public void Enable()  => _enabled = true;
    public void Disable() { _enabled = false; }

    public void Poll()
    {
        if (!_enabled) return;
        var gp = Gamepad.current;
        if (gp == null) return;

        double t = Time.realtimeSinceStartupAsDouble;

        if (gp.dpad.left.wasPressedThisFrame)
        {
            _cutPoint = Mathf.Max(1, _cutPoint - InputConstants.GamepadCutStep);
            EmitAxis("CutPosition", _cutPoint / 51f, t);
        }
        if (gp.dpad.right.wasPressedThisFrame)
        {
            _cutPoint = Mathf.Min(51, _cutPoint + InputConstants.GamepadCutStep);
            EmitAxis("CutPosition", _cutPoint / 51f, t);
        }
        if (gp.selectButton.wasPressedThisFrame)
            EmitAxis("CutCommit", _cutPoint / 51f, t);

        float leftTrig = gp.leftTrigger.ReadValue();
        if (!_leftTriggerHeld && leftTrig >= InputConstants.TriggerCommitThreshold)
        {
            _leftTriggerHeld = true;
            EmitAxis("RiffleDropLeft", 1f, t);
        }
        else if (_leftTriggerHeld && leftTrig < InputConstants.TriggerDeadZone)
        {
            _leftTriggerHeld = false;
        }

        float rightTrig = gp.rightTrigger.ReadValue();
        if (!_rightTriggerHeld && rightTrig >= InputConstants.TriggerCommitThreshold)
        {
            _rightTriggerHeld = true;
            EmitAxis("RiffleDropRight", 1f, t);
        }
        else if (_rightTriggerHeld && rightTrig < InputConstants.TriggerDeadZone)
        {
            _rightTriggerHeld = false;
        }

        if (gp.startButton.wasPressedThisFrame)
            EmitAxis("RiffleStart", _cutPoint / 51f, t);

        if (gp.leftShoulder.wasPressedThisFrame)
            EmitButton("OverhandChunk", t);

        if (gp.buttonSouth.wasPressedThisFrame)  EmitAxis("DealPlayers", 1f, t);
        if (gp.buttonEast.wasPressedThisFrame)   EmitAxis("DealPlayers", 2f, t);
        if (gp.buttonWest.wasPressedThisFrame)   EmitAxis("DealPlayers", 3f, t);
        if (gp.buttonNorth.wasPressedThisFrame)  EmitAxis("DealPlayers", 4f, t);

        if (gp.dpad.up.wasPressedThisFrame)   EmitButton("CollectNextPile", t);
        if (gp.dpad.down.wasPressedThisFrame)  EmitButton("CollectCommit", t);
    }

    private void EmitButton(string action, double t)
        => ButtonEvent?.Invoke(new RawButtonEvent { ActionName = action, IsPressed = true, Source = InputSource.Gamepad, Timestamp = t });

    private void EmitAxis(string action, float value, double t)
        => AxisEvent?.Invoke(new RawAxisEvent { ActionName = action, Value = value, Source = InputSource.Gamepad, Timestamp = t });
}
