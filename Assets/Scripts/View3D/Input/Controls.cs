using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// Every keyboard action in the game. Defaults sit under the left hand around WASD, leaving the right
/// hand on the mouse. Order here is the order shown in the Controls screen.
public enum ControlAction
{
    // At the table
    PickUpPutDown, Cut, Riffle, Overhand, Deal, Collect, Undo, History, Theme, FaceDown, Cancel,
    // While shuffling
    RiffleLeft, RiffleRight, OverhandSlide, OverhandDropRest,
    // Playing a card game (Blackjack, …)
    PlayCards, DrawCard, CallLastCard, CatchLastCard
}

/// Rebindable keyboard controls, saved per player. Actions are grouped by when they're used; keys must be
/// unique wherever two actions can be used at the same moment (Cancel works everywhere), but the same key
/// can serve different moments — Space deals, slides cards during an overhand, and plays cards in a game.
/// The mouse buttons and the number keys (packet size) are fixed.
public static class Controls
{
    public enum Group { Table, Shuffling, Game }

    private const string PrefsPrefix = "pde.key.";

    private static readonly Key[] Defaults =
    {
        Key.E, Key.C, Key.R, Key.F, Key.Space, Key.X, Key.Z, Key.Tab, Key.T, Key.LeftShift, Key.Escape,
        Key.A, Key.D, Key.Space, Key.S,
        Key.Space, Key.Q, Key.G, Key.V
    };

    private static readonly string[] Labels =
    {
        "Pick up / put down the deck", "Cut", "Riffle", "Overhand", "Deal a packet", "Collect the cards",
        "Undo", "Deck history", "Change theme", "Place a card face down (hold)", "Cancel / controls",
        "Riffle: drop from left hand", "Riffle: drop from right hand", "Overhand: slide cards (hold)",
        "Overhand: drop the rest",
        "Play the selected cards", "Pick up a card", "Call LAST CARD", "Catch an uncalled last card"
    };

    private static Key[] _keys;

    /// Raised after any binding changes, so prompts can refresh.
    public static event Action Changed;

    public static int Count => Defaults.Length;

    public static Group GroupOf(ControlAction action)
        => action >= ControlAction.PlayCards ? Group.Game
         : action >= ControlAction.RiffleLeft ? Group.Shuffling
         : Group.Table;

    /// Table actions only used while dealing (so free for game actions to share).
    private static bool DealingOnly(ControlAction action)
        => action == ControlAction.Deal || action == ControlAction.Cut || action == ControlAction.Riffle ||
           action == ControlAction.Overhand || action == ControlAction.PickUpPutDown;

    public static string Label(ControlAction action) => Labels[(int)action];

    public static Key KeyOf(ControlAction action) => Keys[(int)action];

    public static Key DefaultKeyOf(ControlAction action) => Defaults[(int)action];

    /// Short display name for prompts, e.g. "E", "Space", "Tab" (follows the player's keyboard layout).
    public static string Name(ControlAction action) => KeyName(KeyOf(action));

    public static string KeyName(Key key)
    {
        var kb = Keyboard.current;
        string name = kb != null ? kb[key].displayName : null;
        return string.IsNullOrEmpty(name) ? key.ToString() : name;
    }

    public static bool Pressed(ControlAction action)
    {
        var kb = Keyboard.current;
        return kb != null && kb[KeyOf(action)].wasPressedThisFrame;
    }

    public static bool Held(ControlAction action)
    {
        var kb = Keyboard.current;
        return kb != null && kb[KeyOf(action)].isPressed;
    }

    /// Binds <paramref name="key"/> to <paramref name="action"/>. If another action that could be used at
    /// the same time already has that key, the two swap keys, so nothing is ever left unbound.
    /// Returns the action that was swapped, or null.
    public static ControlAction? Bind(ControlAction action, Key key)
    {
        if (key == Key.None || IsReserved(key)) return null;
        var keys = Keys;
        Key previous = keys[(int)action];
        ControlAction? swapped = null;

        for (int i = 0; i < keys.Length; i++)
        {
            var other = (ControlAction)i;
            if (other == action || keys[i] != key || !Clash(action, other)) continue;
            keys[i] = previous;
            Save(other);
            swapped = other;
        }

        keys[(int)action] = key;
        Save(action);
        Changed?.Invoke();
        return swapped;
    }

    public static void ResetToDefaults()
    {
        _keys = (Key[])Defaults.Clone();
        for (int i = 0; i < _keys.Length; i++) PlayerPrefs.DeleteKey(PrefsPrefix + (ControlAction)i);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// Forgets the cached bindings so they're re-read from saved preferences.
    public static void Reload()
    {
        _keys = null;
        Changed?.Invoke();
    }

    /// Keys that can't be bound: the number row picks the packet size.
    public static bool IsReserved(Key key) => key >= Key.Digit1 && key <= Key.Digit0;

    /// Two actions clash if they can be used at the same moment.
    public static bool Clash(ControlAction a, ControlAction b)
    {
        if (a == ControlAction.Cancel || b == ControlAction.Cancel) return true;
        var ga = GroupOf(a);
        var gb = GroupOf(b);
        if (ga == gb) return true;
        // Game actions are used at the table alongside its everyday keys (undo, history, …)
        if (ga == Group.Game && gb == Group.Table) return !DealingOnly(b);
        if (gb == Group.Game && ga == Group.Table) return !DealingOnly(a);
        return false;
    }

    private static Key[] Keys
    {
        get
        {
            if (_keys != null) return _keys;
            _keys = (Key[])Defaults.Clone();
            for (int i = 0; i < _keys.Length; i++)
            {
                int saved = PlayerPrefs.GetInt(PrefsPrefix + (ControlAction)i, -1);
                if (saved > 0 && Enum.IsDefined(typeof(Key), saved) && !IsReserved((Key)saved)) _keys[i] = (Key)saved;
            }
            return _keys;
        }
    }

    private static void Save(ControlAction action)
    {
        PlayerPrefs.SetInt(PrefsPrefix + action, (int)Keys[(int)action]);
        PlayerPrefs.Save();
    }

    // Start clean each play session when domain reload is disabled
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _keys = null;
        Changed = null;
    }
}
