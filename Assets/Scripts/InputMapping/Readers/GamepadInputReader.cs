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
    private int   _cutPoint         = 26;
    private bool  _leftTriggerHeld  = false;
    private bool  _rightTriggerHeld = false;

    public void Enable()  => _enabled = true;
    public void Disable() { _enabled = false; }

    /// Call each frame from InputCoordinator.Update().
    public void Poll()
    {
        if (!_enabled) return;
        var gp = Gamepad.current;
        if (gp == null) return;

        double t = Time.realtimeSinceStartupAsDouble;

        // D-pad left/right → adjust cut point; South button → confirm cut
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
        if (gp.buttonSouth.wasPressedThisFrame)
            EmitAxis("CutCommit", _cutPoint / 51f, t);

        // Left trigger → riffle drop from left half (squeeze past threshold)
        float leftTrig = gp.leftTrigger.ReadValue();
        if (!_leftTriggerHeld && leftTrig >= InputConstants.TriggerCommitThreshold)
        {
            _leftTriggerHeld = true;
            EmitAxis("RiffleDropLeft", 1f, t); // 1 card from left
        }
        else if (_leftTriggerHeld && leftTrig < InputConstants.TriggerDeadZone)
        {
            _leftTriggerHeld = false;
        }

        // Right trigger → riffle drop from right half
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

        // Start button → riffle start (split at current cut point)
        if (gp.startButton.wasPressedThisFrame)
            EmitAxis("RiffleStart", _cutPoint / 51f, t);

        // Left bumper → overhand chunk
        if (gp.leftShoulder.wasPressedThisFrame)
            EmitButton("OverhandChunk", t);

        // Face buttons → deal player count (A=1, B=2, X=3, Y=4)
        if (gp.buttonSouth.wasPressedThisFrame) EmitAxis("DealPlayers", 1f, t);
        if (gp.buttonEast.wasPressedThisFrame)  EmitAxis("DealPlayers", 2f, t);
        if (gp.buttonWest.wasPressedThisFrame)  EmitAxis("DealPlayers", 3f, t);
        if (gp.buttonNorth.wasPressedThisFrame) EmitAxis("DealPlayers", 4f, t);

        // D-pad up → collect next pile; D-pad down → commit collection
        if (gp.dpad.up.wasPressedThisFrame)   EmitButton("CollectNextPile", t);
        if (gp.dpad.down.wasPressedThisFrame)  EmitButton("CollectCommit", t);
    }

    private void EmitButton(string action, double t)
        => ButtonEvent?.Invoke(new RawButtonEvent { ActionName = action, IsPressed = true, Source = InputSource.Gamepad, Timestamp = t });

    private void EmitAxis(string action, float value, double t)
        => AxisEvent?.Invoke(new RawAxisEvent { ActionName = action, Value = value, Source = InputSource.Gamepad, Timestamp = t });
}
