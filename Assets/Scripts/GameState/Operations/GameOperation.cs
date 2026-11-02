using System;

/// Base class for anything a player does to the cards. An operation is carried out purely as
/// CardMoves on a TableTransaction, so every operation is tracked, undoable and animatable the same way.
public abstract class GameOperation
{
    public virtual string Name => GetType().Name.Replace("Operation", "");

    /// True for actions that may legitimately move no cards (a call, or drawing from an empty table);
    /// otherwise an operation that moves nothing is rejected as "Nothing to do".
    public virtual bool AllowNoMoves => false;

    /// Performs the operation. Throw ArgumentException or InvalidOperationException if it is physically
    /// impossible; GameStateManager then rolls back any moves already made.
    public abstract void Execute(TableTransaction tx);

    /// The hand opposite <paramref name="hand"/>.
    protected static ZoneId OtherHand(ZoneId hand) => hand == ZoneId.LeftHand ? ZoneId.RightHand : ZoneId.LeftHand;

    /// Shuffles work on the deck lying on the table or on a packet held in one hand, and stage cards
    /// in the hands; the hand(s) used for staging must start empty.
    protected static void RequireShuffleSource(CardTable table, ZoneId source)
    {
        if (source == ZoneId.Deck)
        {
            if (table.Count(ZoneId.LeftHand) > 0 || table.Count(ZoneId.RightHand) > 0)
                throw new InvalidOperationException("Put down the cards you are holding before shuffling.");
        }
        else if (source.Kind == ZoneKind.Held)
        {
            if (table.Count(OtherHand(source)) > 0)
                throw new InvalidOperationException("Your other hand must be empty to shuffle.");
        }
        else
        {
            throw new ArgumentException($"Shuffle the deck or a held packet, not {source}.");
        }
    }
}
