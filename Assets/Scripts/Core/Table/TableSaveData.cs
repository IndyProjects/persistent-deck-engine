using System;
using System.Collections.Generic;

/// Serializable snapshot of a CardTable (plain fields, so JsonUtility can write it).
/// Loading validates the 52-card invariant, so a corrupted or hand-edited save is rejected, not half-loaded.
[Serializable]
public class TableSaveData
{
    public const int CurrentVersion = 1;

    public int        version = CurrentVersion;
    public int        roundNumber;
    public ZoneData[] zones;
    public int[]      faceUpCards;

    [Serializable]
    public class ZoneData
    {
        public ZoneKind kind;
        public int      index;
        public int[]    cards;
    }

    public static TableSaveData From(CardTable table, int roundNumber)
    {
        var zoneIds = table.NonEmptyZones();
        var data = new TableSaveData
        {
            roundNumber = roundNumber,
            zones       = new ZoneData[zoneIds.Count]
        };
        for (int i = 0; i < zoneIds.Count; i++)
        {
            var cards = new int[table.Count(zoneIds[i])];
            for (int c = 0; c < cards.Length; c++) cards[c] = table.Cards(zoneIds[i])[c];
            data.zones[i] = new ZoneData { kind = zoneIds[i].Kind, index = zoneIds[i].Index, cards = cards };
        }

        var faceUp = new List<int>();
        for (int card = 0; card < DeckEngine.DeckSize; card++)
            if (table.IsFaceUp(card)) faceUp.Add(card);
        data.faceUpCards = faceUp.ToArray();
        return data;
    }

    /// Rebuilds the table. Throws ArgumentException if the data is invalid or from an unknown version.
    public CardTable ToTable()
    {
        if (version != CurrentVersion)
            throw new ArgumentException($"Unsupported save version {version} (expected {CurrentVersion}).");
        if (zones == null)
            throw new ArgumentException("Save data has no zones.");

        var entries = new List<KeyValuePair<ZoneId, int[]>>(zones.Length);
        foreach (var z in zones)
        {
            if (z == null) throw new ArgumentException("Save data contains a null zone.");
            if (!Enum.IsDefined(typeof(ZoneKind), z.kind)) throw new ArgumentException($"Unknown zone kind {(int)z.kind}.");
            entries.Add(new KeyValuePair<ZoneId, int[]>(new ZoneId(z.kind, z.index), z.cards));
        }
        return CardTable.FromZones(entries, faceUpCards);
    }
}
