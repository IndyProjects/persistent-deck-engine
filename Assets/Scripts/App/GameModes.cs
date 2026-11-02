using System;
using System.Collections.Generic;
using UnityEngine;

/// One playable game: what the menu shows and which rules the table runs.
public sealed class GameMode
{
    public string Id          { get; }
    public string DisplayName { get; }
    public string Description { get; }
    /// False shows the mode greyed out as "Coming soon".
    public bool   Available   { get; }
    /// Shown in the HUD while playing (e.g. what isn't implemented yet); null for none.
    public string Note        { get; }

    private readonly Func<IRuleSet> _createRules;

    public GameMode(string id, string displayName, string description, Func<IRuleSet> createRules,
                    bool available = true, string note = null)
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        _createRules = createRules;
        Available = available;
        Note = note;
    }

    public IRuleSet CreateRules() => _createRules();
}

/// The games on offer and which one the player picked. The table (and its persistent deck) is shared
/// by every game: switching games keeps the same physical deck, in whatever order it was left.
public static class GameModes
{
    public const string PrefsKey = "pde.mode";

    public static readonly GameMode Johoti = new GameMode(
        "johoti", "Johoti",
        "Four players, all 52 cards dealt — 13 each.",
        () => new DealerRules(players: 4, cardsPerPlayer: 13),
        note: "Play rules coming soon — dealing only for now.");

    public static readonly GameMode FreePlay = new GameMode(
        "freeplay", "Free Play",
        "Shuffle, deal in any rhythm and play cards freely. Four players, five cards each.",
        () => new DealerRules(players: 4, cardsPerPlayer: 5));

    public static readonly GameMode Blackjack = new GameMode(
        "blackjack", "Blackjack",
        "Be first to shed your cards. 2s, 8s, black jacks and Aces bite. You vs three opponents.",
        () => new BlackjackRules(players: 4));

    public static IReadOnlyList<GameMode> All { get; } = new[] { Johoti, FreePlay, Blackjack };

    /// The game the table will run: the player's last choice if it's still available, else Free Play.
    public static GameMode Selected
    {
        get
        {
            var mode = Find(PlayerPrefs.GetString(PrefsKey, null));
            return mode != null && mode.Available ? mode : FreePlay;
        }
    }

    public static GameMode Find(string id)
    {
        foreach (var mode in All)
            if (mode.Id == id) return mode;
        return null;
    }

    /// Remembers the choice; refuses modes that aren't available yet.
    public static bool Select(GameMode mode)
    {
        if (mode == null || !mode.Available) return false;
        PlayerPrefs.SetString(PrefsKey, mode.Id);
        PlayerPrefs.Save();
        return true;
    }
}
