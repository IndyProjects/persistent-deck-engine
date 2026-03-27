using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// Reads keyboard input and emits RawButtonEvents.
/// A prompt state machine handles multi-step sequences (e.g. Deal: D → player count → card count).
public class KeyboardInputReader : IInputReader
{
    public event Action<RawPointerEvent> PointerEvent;
    public event Action<RawAxisEvent>    AxisEvent;
    public event Action<RawButtonEvent>  ButtonEvent;

    private enum PromptState
    {
        Idle,
        CutMode,          // [ / ] adjust cut point; Enter confirms
        RiffleMode,       // S started riffle; digits = drop count; Enter confirms drop
        DealPlayerPrompt, // D pressed; waiting for player-count digit
        DealCardPrompt,   // player count entered; waiting for cards-each digit
        CollectMode       // C pressed; digit keys = pile indices; Enter finishes
    }

    private bool        _enabled;
    private PromptState _state      = PromptState.Idle;
    private int         _cutPoint   = 26;
    private int         _dealPlayers;
    private int         _riffleDropCount;

    public void Enable()  => _enabled = true;
    public void Disable() { _enabled = false; _state = PromptState.Idle; }

    /// Call each frame from InputCoordinator.Update().
    public void Poll()
    {
        if (!_enabled) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        double t = Time.realtimeSinceStartupAsDouble;

        switch (_state)
        {
            case PromptState.Idle:       PollIdle(kb, t);       break;
            case PromptState.CutMode:    PollCutMode(kb, t);    break;
            case PromptState.RiffleMode: PollRiffleMode(kb, t); break;
            case PromptState.DealPlayerPrompt: PollDealPlayer(kb, t); break;
            case PromptState.DealCardPrompt:   PollDealCards(kb, t);  break;
            case PromptState.CollectMode:      PollCollect(kb, t);    break;
        }
    }

    private void PollIdle(Keyboard kb, double t)
    {
        if (kb.cKey.wasPressedThisFrame)   { _state = PromptState.CutMode;    _cutPoint = 26; }
        if (kb.sKey.wasPressedThisFrame)   { _state = PromptState.RiffleMode; EmitButton("RiffleStart", t); }
        if (kb.gKey.wasPressedThisFrame)     EmitButton("OverhandChunk", t);
        if (kb.dKey.wasPressedThisFrame)   { _state = PromptState.DealPlayerPrompt; }
        if (kb.xKey.wasPressedThisFrame)   { _state = PromptState.CollectMode; EmitButton("CollectStart", t); }
        if (kb.escapeKey.wasPressedThisFrame) EmitButton("Cancel", t);
    }

    private void PollCutMode(Keyboard kb, double t)
    {
        if (kb.leftBracketKey.wasPressedThisFrame)
        {
            _cutPoint = Mathf.Max(1, _cutPoint - InputConstants.GamepadCutStep);
            EmitAxis("CutPosition", _cutPoint / 51f, t);
        }
        if (kb.rightBracketKey.wasPressedThisFrame)
        {
            _cutPoint = Mathf.Min(51, _cutPoint + InputConstants.GamepadCutStep);
            EmitAxis("CutPosition", _cutPoint / 51f, t);
        }

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            EmitAxis("CutCommit", _cutPoint / 51f, t);
            _state = PromptState.Idle;
        }
        if (kb.escapeKey.wasPressedThisFrame) _state = PromptState.Idle;
    }

    private void PollRiffleMode(Keyboard kb, double t)
    {
        int? digit = ReadDigit(kb);
        if (digit.HasValue) _riffleDropCount = digit.Value;

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            EmitAxis("RiffleDrop", Mathf.Max(1, _riffleDropCount), t);
            _riffleDropCount = 0;
        }
        if (kb.escapeKey.wasPressedThisFrame)
        {
            EmitButton("Cancel", t);
            _state = PromptState.Idle;
        }
        if (kb.eKey.wasPressedThisFrame) // E = end riffle
            _state = PromptState.Idle;
    }

    private void PollDealPlayer(Keyboard kb, double t)
    {
        int? digit = ReadDigit(kb);
        if (digit.HasValue && digit.Value >= 1)
        {
            _dealPlayers = digit.Value;
            _state = PromptState.DealCardPrompt;
        }
        if (kb.escapeKey.wasPressedThisFrame) _state = PromptState.Idle;
    }

    private void PollDealCards(Keyboard kb, double t)
    {
        int? digit = ReadDigit(kb);
        if (digit.HasValue && digit.Value >= 1)
        {
            EmitAxis("DealPlayers",    _dealPlayers,  t);
            EmitAxis("DealCardsEach",  digit.Value,   t);
            _state = PromptState.Idle;
        }
        if (kb.escapeKey.wasPressedThisFrame) _state = PromptState.Idle;
    }

    private void PollCollect(Keyboard kb, double t)
    {
        int? digit = ReadDigit(kb);
        if (digit.HasValue)
            EmitAxis("CollectPile", digit.Value, t);

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            EmitButton("CollectCommit", t);
            _state = PromptState.Idle;
        }
        if (kb.escapeKey.wasPressedThisFrame) _state = PromptState.Idle;
    }

    private static int? ReadDigit(Keyboard kb)
    {
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 1;
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 2;
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 3;
        if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 4;
        if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) return 5;
        if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) return 6;
        if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) return 7;
        if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) return 8;
        if (kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame) return 9;
        if (kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame) return 0;
        return null;
    }

    private void EmitButton(string action, double t)
        => ButtonEvent?.Invoke(new RawButtonEvent { ActionName = action, IsPressed = true, Source = InputSource.Keyboard, Timestamp = t });

    private void EmitAxis(string action, float value, double t)
        => AxisEvent?.Invoke(new RawAxisEvent { ActionName = action, Value = value, Source = InputSource.Keyboard, Timestamp = t });
}
