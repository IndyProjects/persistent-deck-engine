/// Decides which operations are allowed, on top of physical possibility (which the operations
/// themselves enforce). Swap rule sets to change from free play to a specific game.
public interface IRuleSet
{
    string Name { get; }
    OperationResult Check(GameOperation op, CardTable table);
}

/// Anything physically possible is allowed, like handling a real deck.
public sealed class FreePlayRules : IRuleSet
{
    public string Name => "Free play";
    public OperationResult Check(GameOperation op, CardTable table) => OperationResult.Ok();
}

/// The original two-phase flow: shuffle and deal only while all 52 cards are in the deck
/// (Idle), then play and collect until everything is back (InPlay).
public sealed class ClassicRules : IRuleSet
{
    public string Name => "Classic";

    public OperationResult Check(GameOperation op, CardTable table)
    {
        bool idle = GameStateManager.PhaseOf(table) == GamePhase.Idle;
        switch (op)
        {
            case CutOperation _:
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
