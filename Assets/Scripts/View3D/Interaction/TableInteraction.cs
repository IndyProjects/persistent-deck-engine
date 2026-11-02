using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// Base for a hands-on interaction with the cards (cut, riffle, overhand): takes the camera to a
/// close-up, owns the mouse until it finishes, and cancels itself if anything else changes the table.
/// Subclasses implement the mode itself; the base handles setup, prompts and bookkeeping.
public abstract class TableInteraction : MonoBehaviour, IStateManagerConsumer
{
    public GameStateManager StateManager { protected get; set; }

    /// Raised when the interaction starts (true) and ends (false), so other input can stand down.
    public event Action<bool> ActiveChanged;

    public bool IsActive { get; private set; }

    /// Why the last attempt to start was refused (or the last commit failed); null if it wasn't.
    public string LastMessage => Message;

    protected GameStateManager Sm;
    protected TableCardView    View;
    protected SeatedCameraRig  Rig;
    protected Camera           Cam;
    protected string           Message;

    private bool _applying;

    protected virtual void Start()
    {
        Sm = StateManager;
        if (Sm != null) Sm.StateChanged += OnStateChanged;
    }

    protected virtual void OnDestroy()
    {
        if (Sm != null) Sm.StateChanged -= OnStateChanged;
    }

    /// Starts with the scene's card view and seated camera. Returns false (changing nothing) if the
    /// interaction isn't possible right now; <see cref="LastMessage"/> then says why.
    public bool TryBegin()
    {
        if (IsActive || Sm == null) return false;
        var view = View != null ? View : FindAnyObjectByType<TableCardView>();
        var cam  = Camera.main;
        var rig  = cam != null ? cam.GetComponent<SeatedCameraRig>() : null;
        if (view == null || rig == null) return false;
        return Begin(Sm, view, rig);
    }

    /// Starts with explicit collaborators (also used by previews and tests).
    public bool Begin(GameStateManager sm, TableCardView view, SeatedCameraRig rig)
    {
        if (IsActive) return false;
        Sm = sm;
        View = view;
        Rig = rig;
        Cam = rig.GetComponent<Camera>();
        Message = null;
        if (view.IsReplaying) view.EndReplay();
        if (!OnBegin()) return false;

        IsActive = true;
        ActiveChanged?.Invoke(true);
        return true;
    }

    /// Abandons the interaction, leaving the game unchanged.
    public void Cancel()
    {
        if (!IsActive) return;
        OnCancel();
        Finish();
    }

    /// Prepares the mode; return false (with <see cref="Message"/> set) to refuse.
    protected abstract bool OnBegin();

    /// Undoes any presentation the mode set up (spreads, previews).
    protected abstract void OnCancel();

    /// One frame of the mode while active (after the shared Esc check).
    protected abstract void Tick(Mouse mouse, Keyboard kb);

    /// The prompt shown at the bottom of the screen while active.
    protected abstract string Prompt { get; }

    /// Right-click cancels too, unless the mode uses the right button itself.
    protected virtual bool RightClickCancels => true;

    /// Applies the mode's result as one game action and ends the mode.
    protected OperationResult Commit(GameOperation op)
    {
        _applying = true;
        OperationResult result;
        try { result = Sm.Apply(op); }
        finally { _applying = false; }
        if (!result.Success) Message = result.Reason;
        Finish();
        return result;
    }

    protected void Finish()
    {
        if (!IsActive) return;
        IsActive = false;
        Rig.ReleaseFocus();
        ActiveChanged?.Invoke(false);
    }

    /// The packet being handled: the deck in the dealer's right hand if held, else the table deck.
    protected static ZoneId HeldOrTable(CardTable table)
        => table.Count(ZoneId.RightHand) > 0 ? ZoneId.RightHand : ZoneId.Deck;

    protected static bool PointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    /// Close-up looking down at the dealer's two hands and the deck spot where riffles land.
    protected void FocusOnHands()
    {
        var hands = (TableLayout.LeftHandAnchor + TableLayout.RightHandAnchor) * 0.5f;
        var target = Vector3.Lerp(hands, TableLayout.DeckAnchor, 0.35f);
        Rig.Focus(hands + new Vector3(-0.05f, 0.32f, -0.36f), target);
    }

    /// Close, front-on view across the riffle spot: low enough to see the two halves zip together, with
    /// the packets still in hand above and behind them.
    protected void FocusOnRiffle()
    {
        var spot = RifflePresentation.Spot;
        Rig.Focus(spot + new Vector3(0f, 0.15f, -0.3f), spot + new Vector3(0f, 0.045f, 0.02f));
    }

    /// Side-on close-up of a spread stack, from above the top card so only backs are seen.
    protected void FocusOnSpread(Vector3 anchor, Quaternion frame, int count, float spacing, float distance)
    {
        float height = spacing * count;
        var mid = anchor + Vector3.up * (height * 0.5f);
        var eye = anchor + Vector3.up * (height + 0.1f) + frame * Vector3.right * distance + frame * Vector3.back * (distance * 0.3f);
        Rig.Focus(eye, mid);
    }

    protected virtual void Update()
    {
        if (!IsActive) return;
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (Controls.Pressed(ControlAction.Cancel) ||
            (RightClickCancels && mouse != null && mouse.rightButton.wasPressedThisFrame))
        {
            Cancel();
            return;
        }
        Tick(mouse, kb);
    }

    private void OnStateChanged(GameStateSnapshot _)
    {
        // Something else changed the table (undo, reset, a button): this mode's picture is stale
        if (IsActive && !_applying) Cancel();
    }

    private void OnGUI()
    {
        if (IsActive) PromptBox.Draw(Message != null ? $"{Prompt}\n{Message}" : Prompt);
        else if (Message != null && PromptBox.DrawDismissable(Message)) Message = null;
    }
}

/// Temporary on-screen prompt (IMGUI) until interactions get a proper HUD.
public static class PromptBox
{
    private static GUIStyle _style;

    private static GUIStyle Style => _style ??= new GUIStyle(GUI.skin.box)
    {
        fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true
    };

    public static void Draw(string text)
    {
        float width = Mathf.Min(760f, Screen.width - 40f);
        var size = Style.CalcHeight(new GUIContent(text), width);
        GUI.Box(new Rect((Screen.width - width) * 0.5f, Screen.height - size - 24f, width, size + 8f), text, Style);
    }

    /// Draws the message; returns true once the player clicks anywhere (to dismiss it).
    public static bool DrawDismissable(string text)
    {
        Draw(text);
        return Event.current.type == EventType.MouseDown;
    }
}
