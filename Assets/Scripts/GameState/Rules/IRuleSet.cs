/// Decides which operations are allowed, on top of physical possibility (which the operations
/// themselves enforce). Swap rule sets to change from free play to a specific game.
public interface IRuleSet
{
    string Name { get; }
    OperationResult Check(GameOperation op, CardTable table);

    /// One-line status for the HUD (phase, whose turn, …).
    string Describe(CardTable table);
}

/// A rule set that remembers things the table alone can't tell it (e.g. whose turn it is to be dealt to).
/// GameStateManager saves this state with every history entry so undo restores it exactly, and writes it
/// into save files.
public interface IStatefulRuleSet : IRuleSet
{
    /// Called after an operation has been applied successfully.
    void OnApplied(GameOperation op, CardTable table);

    /// Serialises the rule state (any format; round-trips through LoadState).
    string SaveState();

    /// Restores state from SaveState output; null means the initial state.
    void LoadState(string state);
}

/// A rule set whose round starts with the standard dealing round (pick up, shuffle, deal packets).
/// Controls use <see cref="Dealing"/> for packet size, whose turn it is to receive, and so on.
public interface IDealingRuleSet : IRuleSet
{
    DealerRules Dealing { get; }
}

/// Anything physically possible is allowed, like handling a real deck.
public sealed class FreePlayRules : IRuleSet
{
    public string Name => "Free play";
    public OperationResult Check(GameOperation op, CardTable table) => OperationResult.Ok();
    public string Describe(CardTable table) => "Free play";
}

/// The original two-phase flow: shuffle and deal only while all 52 cards are in the deck
/// (Idle), then play and collect until everything is back (InPlay).
public sealed class ClassicRules : IRuleSet
{
    public string Name => "Classic";

    public string Describe(CardTable table) => GameStateManager.PhaseOf(table).ToString();

    public OperationResult Check(GameOperation op, CardTable table)
    {
        bool idle = GameStateManager.PhaseOf(table) == GamePhase.Idle;
        switch (op)
        {
            case CutOperation _:
            case CutSectionOperation _:
            case RiffleOperation _:
            case OverhandOperation _:
                return idle ? OperationResult.Ok() : OperationResult.Reject("Cannot shuffle while hands are dealt.");

            case DealOperation _:
                return idle ? OperationResult.Ok() : OperationResult.Reject("Cards already dealt. Collect first.");

            case CollectOperation _:
                return idle ? OperationResult.Reject("No hands to collect.") : OperationResult.Ok();

            case MoveCardOperation _:
            case FlipCardOperation _:
            case MovePacketOperation _:
                return idle ? OperationResult.Reject("Deal before playing cards.") : OperationResult.Ok();

            default:
                return OperationResult.Reject($"Unknown operation type: {op.GetType().Name}");
        }
    }
}
