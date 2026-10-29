/// Base class for anything a player does to the cards. An operation is carried out purely as
/// CardMoves on a TableTransaction, so every operation is tracked, undoable and animatable the same way.
public abstract class GameOperation
{
    public virtual string Name => GetType().Name.Replace("Operation", "");

    /// Performs the operation. Throw ArgumentException or InvalidOperationException if it is physically
    /// impossible; GameStateManager then rolls back any moves already made.
    public abstract void Execute(TableTransaction tx);

    /// Shuffles stage packets in the shuffler's hands, so those must start empty.
    protected static void RequireEmptyHands(CardTable table)
    {
        if (table.Count(ZoneId.LeftHand) > 0 || table.Count(ZoneId.RightHand) > 0)
            throw new System.InvalidOperationException("Put down the cards you are holding before shuffling.");
    }
}
