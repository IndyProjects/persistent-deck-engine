using System;
using System.Collections.Generic;

public enum ActionKind { Other, Riffle, Overhand, Cut, Deal, Play, Collect, Handling, Undo }

/// The deck's memory: every applied action (undos included, as their own entries) with its exact moves,
/// plus the table as it was before the oldest entry. From that the table before any entry can be rebuilt,
/// a single card's journey traced, and a past shuffle replayed. Keeps the most recent
/// <see cref="MaxEntries"/>; older entries are folded into the origin table. Saved with the table.
public sealed class DeckHistory
{
    public const int MaxEntries = 500;

    public sealed class Entry
    {
        public string                  Name  { get; }
        public int                     Round { get; }
        public ActionKind              Kind  { get; }
        public IReadOnlyList<CardMove> Moves { get; }

        public Entry(string name, int round, ActionKind kind, IReadOnlyList<CardMove> moves)
        {
            Name  = name;
            Round = round;
            Kind  = kind;
            Moves = moves;
        }

        public bool IsShuffle => Kind == ActionKind.Riffle || Kind == ActionKind.Overhand || Kind == ActionKind.Cut;
    }

    /// One change in a card's position caused by one entry.
    public readonly struct Step
    {
        public readonly int    EntryIndex;
        public readonly ZoneId From, To;
        public readonly int    FromIndex, ToIndex;
        public readonly bool   FaceUpBefore, FaceUpAfter;

        public Step(int entry, ZoneId from, int fromIndex, ZoneId to, int toIndex, bool faceUpBefore, bool faceUpAfter)
        {
            EntryIndex   = entry;
            From = from; FromIndex = fromIndex;
            To = to;     ToIndex = toIndex;
            FaceUpBefore = faceUpBefore;
            FaceUpAfter  = faceUpAfter;
        }
    }

    private readonly List<Entry> _entries = new List<Entry>();
    private CardTable _origin;

    public IReadOnlyList<Entry> Entries => _entries;

    public DeckHistory(CardTable origin) => _origin = origin.Clone();

    public static ActionKind KindOf(GameOperation op)
    {
        switch (op)
        {
            case RiffleOperation _:                                 return ActionKind.Riffle;
            case OverhandOperation _:                               return ActionKind.Overhand;
            case CutOperation _: case CutSectionOperation _:        return ActionKind.Cut;
            case DealOperation _: case DealPacketOperation _:       return ActionKind.Deal;
            case MoveCardOperation _: case FlipCardOperation _:
            case MovePacketOperation _: case PlayCardsOperation _:  return ActionKind.Play;
            case DrawCardsOperation _: case CatchLastCardOperation _:
            case StartDiscardOperation _:                           return ActionKind.Deal;
            case CollectOperation _:                                return ActionKind.Collect;
            case PickUpDeckOperation _: case PutDownDeckOperation _: return ActionKind.Handling;
            default:                                                return ActionKind.Other;
        }
    }

    public void Record(Entry entry)
    {
        _entries.Add(entry);
        if (_entries.Count <= MaxEntries) return;
        foreach (var move in _entries[0].Moves) _origin.Apply(move);
        _entries.RemoveAt(0);
    }

    /// The table as it was just before entry <paramref name="index"/> (Entries.Count = after the last).
    public CardTable StateBefore(int index)
    {
        var table = _origin.Clone();
        for (int i = 0; i < index && i < _entries.Count; i++)
            foreach (var move in _entries[i].Moves) table.Apply(move);
        return table;
    }

    /// Every logged action that moved or turned <paramref name="card"/>, oldest first.
    public List<Step> JourneyOf(int card)
    {
        var steps = new List<Step>();
        var table = _origin.Clone();
        for (int i = 0; i < _entries.Count; i++)
        {
            var from = table.LocationOf(card);
            int fromIndex = table.IndexOf(card);
            bool faceBefore = table.IsFaceUp(card);

            foreach (var move in _entries[i].Moves) table.Apply(move);

            var to = table.LocationOf(card);
            int toIndex = table.IndexOf(card);
            bool faceAfter = table.IsFaceUp(card);
            if (to != from || toIndex != fromIndex || faceAfter != faceBefore)
                steps.Add(new Step(i, from, fromIndex, to, toIndex, faceBefore, faceAfter));
        }
        return steps;
    }

    /// Index of the most recent shuffle (riffle, overhand or cut), or −1.
    public int LastShuffleIndex()
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
            if (_entries[i].IsShuffle) return i;
        return -1;
    }

    /// How many of each kind of action happened since the deck was last collected.
    public int CountSinceCollect(ActionKind kind)
    {
        int count = 0;
        for (int i = _entries.Count - 1; i >= 0 && _entries[i].Kind != ActionKind.Collect; i--)
            if (_entries[i].Kind == kind) count++;
        return count;
    }

    // ---- Persistence ----

    public TableSaveData.HistorySaveData ToSave()
    {
        var origin = TableSaveData.From(_origin, 0);
        var data = new TableSaveData.HistorySaveData
        {
            originZones  = origin.zones,
            originFaceUp = origin.faceUpCards,
            entries      = new TableSaveData.EntryData[_entries.Count]
        };
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            var ints = new int[e.Moves.Count * TableSaveData.MoveInts];
            for (int m = 0; m < e.Moves.Count; m++)
            {
                var mv = e.Moves[m];
                int o = m * TableSaveData.MoveInts;
                ints[o]     = (int)mv.From.Kind; ints[o + 1] = mv.From.Index; ints[o + 2] = mv.FromIndex; ints[o + 3] = mv.Count;
                ints[o + 4] = (int)mv.To.Kind;   ints[o + 5] = mv.To.Index;   ints[o + 6] = mv.ToIndex;   ints[o + 7] = mv.Flip ? 1 : 0;
            }
            data.entries[i] = new TableSaveData.EntryData { name = e.Name, round = e.Round, kind = (int)e.Kind, moves = ints };
        }
        return data;
    }

    /// Rebuilds a history and checks it leads exactly to <paramref name="expectedEnd"/>.
    /// Throws ArgumentException if the data is malformed or inconsistent with the saved table.
    public static DeckHistory FromSave(TableSaveData.HistorySaveData data, CardTable expectedEnd)
    {
        if (data?.originZones == null || data.entries == null)
            throw new ArgumentException("History data is incomplete.");

        var origin = new TableSaveData { zones = data.originZones, faceUpCards = data.originFaceUp }.ToTable();
        var history = new DeckHistory(origin);
        var replay = origin.Clone();

        foreach (var e in data.entries)
        {
            if (e?.moves == null || e.moves.Length % TableSaveData.MoveInts != 0)
                throw new ArgumentException("History entry is malformed.");
            var moves = new List<CardMove>(e.moves.Length / TableSaveData.MoveInts);
            for (int o = 0; o < e.moves.Length; o += TableSaveData.MoveInts)
            {
                var mv = new CardMove(Zone(e.moves[o], e.moves[o + 1]), e.moves[o + 2], e.moves[o + 3],
                                      Zone(e.moves[o + 4], e.moves[o + 5]), e.moves[o + 6], e.moves[o + 7] != 0);
                moves.Add(replay.Apply(mv)); // throws if the move doesn't fit
            }
            var kind = Enum.IsDefined(typeof(ActionKind), e.kind) ? (ActionKind)e.kind : ActionKind.Other;
            history._entries.Add(new Entry(e.name ?? "?", e.round, kind, moves));
        }

        if (!replay.SameAs(expectedEnd))
            throw new ArgumentException("History doesn't lead to the saved table.");
        return history;
    }

    private static ZoneId Zone(int kind, int index)
    {
        if (!Enum.IsDefined(typeof(ZoneKind), kind)) throw new ArgumentException($"Unknown zone kind {kind}.");
        return new ZoneId((ZoneKind)kind, index);
    }
}
