using System;
using System.Collections.Generic;

/// Deterministic, input-driven physical simulation of a 52-card deck.
/// No UI, no randomness, no game rules. Pure logic only.
///
/// Card integer encoding:
///   0–12  = Hearts
///   13–25 = Diamonds
///   26–38 = Clubs
///   39–51 = Spades
public static class DeckEngine
{
    public const int DeckSize = 52;

    ///Returns a fresh ordered deck array 0-51].
    public static int[] CreateOrderedDeck()
    {
        int[] deck = new int[DeckSize];
        for (int i = 0; i < DeckSize; i++)
            deck[i] = i;
        return deck;
    }


    /// Returns true if deck has exactly 52 cards, all unique values 0–51.
    public static bool ValidateDeck(int[] deck)
    {
        if (deck == null || deck.Length != DeckSize)
            return false;

        bool[] seen = new bool[DeckSize];
        foreach (int card in deck)
        {
            if (card < 0 || card >= DeckSize)
                return false;
            if (seen[card])
                return false;
            seen[card] = true;
        }
        return true;
    }

    /// Splits the deck at <paramref name="position"/> and swaps the two halves.
    public static int[] CutDeck(int[] deck, int position)
    {
        if (!ValidateDeck(deck))
            throw new ArgumentException("Invalid deck passed to CutDeck.");
        if (position < 1 || position > DeckSize - 1)
            throw new ArgumentOutOfRangeException(nameof(position), "Cut position must be between 1 and 51.");

        int[] result = new int[DeckSize];
        int bottomLength = DeckSize - position;

        // Bottom half comes first
        Array.Copy(deck, position, result, 0, bottomLength);
        // Top half follows
        Array.Copy(deck, 0, result, bottomLength, position);

        return result;
    }

    public static int[] RiffleShuffle(int[] deck, int splitIndex, int[] dropPattern)
    {
        if (!ValidateDeck(deck))
            throw new ArgumentException("Invalid deck passed to RiffleShuffle.");
        if (splitIndex < 1 || splitIndex > DeckSize - 1)
            throw new ArgumentOutOfRangeException(nameof(splitIndex), "splitIndex must be between 1 and 51.");
        if (dropPattern == null || dropPattern.Length == 0)
            throw new ArgumentException("dropPattern must not be empty.", nameof(dropPattern));

        // Split into two halves
        int leftLength = splitIndex;
        int rightLength = DeckSize - splitIndex;
        int leftPos = 0;
        int rightPos = 0;

        List<int> result = new List<int>(DeckSize);

        bool takingFromLeft = true;
        int patternIndex = 0;

        while (leftPos < leftLength || rightPos < rightLength)
        {
            int take = patternIndex < dropPattern.Length ? dropPattern[patternIndex] : 1;
            patternIndex++;

            if (takingFromLeft)
            {
                int available = leftLength - leftPos;
                int actual = Math.Min(take, available);
                for (int i = 0; i < actual; i++)
                    result.Add(deck[leftPos++]);
            }
            else
            {
                int available = rightLength - rightPos;
                int actual = Math.Min(take, available);
                for (int i = 0; i < actual; i++)
                    result.Add(deck[splitIndex + rightPos++]);
            }

            takingFromLeft = !takingFromLeft;
        }

        return result.ToArray();
    }

    /// Input-driven overhand shuffle.
    /// Removes chunks from the top of the deck in sequence, then reassembles
    /// them in reverse order of removal. Sum of <paramref name="chunkSizes"/> must equal 52.
    public static int[] OverhandShuffle(int[] deck, int[] chunkSizes)
    {
        if (!ValidateDeck(deck))
            throw new ArgumentException("Invalid deck passed to OverhandShuffle.");
        if (chunkSizes == null || chunkSizes.Length == 0)
            throw new ArgumentException("chunkSizes must not be empty.", nameof(chunkSizes));

        int total = 0;
        foreach (int size in chunkSizes)
        {
            if (size <= 0)
                throw new ArgumentException("Each chunk size must be greater than 0.");
            total += size;
        }
        if (total != DeckSize)
            throw new ArgumentException($"Sum of chunkSizes must equal {DeckSize}. Got {total}.");

        // Extract chunks from the top of the deck in order
        int[][] chunks = new int[chunkSizes.Length][];
        int deckPos = 0;
        for (int i = 0; i < chunkSizes.Length; i++)
        {
            chunks[i] = new int[chunkSizes[i]];
            Array.Copy(deck, deckPos, chunks[i], 0, chunkSizes[i]);
            deckPos += chunkSizes[i];
        }

        // Reassemble in reverse order
        int[] result = new int[DeckSize];
        int resultPos = 0;
        for (int i = chunks.Length - 1; i >= 0; i--)
        {
            Array.Copy(chunks[i], 0, result, resultPos, chunks[i].Length);
            resultPos += chunks[i].Length;
        }

        return result;
    }

    /// Deals cards round-robin from the top of the deck (index 0).
    public static (int[][] hands, int[] remainingDeck) DealCards(int[] deck, int players, int cardsEach)
    {
        if (!ValidateDeck(deck))
            throw new ArgumentException("Invalid deck passed to DealCards.");
        if (players <= 0)
            throw new ArgumentOutOfRangeException(nameof(players), "players must be greater than 0.");
        if (cardsEach <= 0)
            throw new ArgumentOutOfRangeException(nameof(cardsEach), "cardsEach must be greater than 0.");

        int totalDealt = players * cardsEach;
        if (totalDealt > DeckSize)
            throw new ArgumentException($"Cannot deal {totalDealt} cards from a {DeckSize}-card deck.");

        int[][] hands = new int[players][];
        for (int i = 0; i < players; i++)
            hands[i] = new int[cardsEach];

        // Round-robin: card 0 → player 0, card 1 → player 1, etc.
        for (int card = 0; card < totalDealt; card++)
            hands[card % players][card / players] = deck[card];

        int remaining = DeckSize - totalDealt;
        int[] remainingDeck = new int[remaining];
        Array.Copy(deck, totalDealt, remainingDeck, 0, remaining);

        return (hands, remainingDeck);
    }

    public static int[] CollectCards(int[][] piles, int[] order)
    {
        if (piles == null) throw new ArgumentNullException(nameof(piles));
        if (order == null) throw new ArgumentNullException(nameof(order));
        if (order.Length != piles.Length)
            throw new ArgumentException("order length must match number of piles.");

        List<int> result = new List<int>(DeckSize);
        foreach (int pileIndex in order)
        {
            if (pileIndex < 0 || pileIndex >= piles.Length)
                throw new ArgumentOutOfRangeException(nameof(order), $"Pile index {pileIndex} is out of range.");
            if (piles[pileIndex] != null)
                result.AddRange(piles[pileIndex]);
        }

        int[] deck = result.ToArray();
        if (!ValidateDeck(deck))
            throw new InvalidOperationException("CollectCards produced an invalid deck. Check that all 52 cards appear exactly once across all piles.");

        return deck;
    }

    ///Returns the suit of a card (0=Hearts, 1=Diamonds, 2=Clubs, 3=Spades)
    public static int GetSuit(int card) => card / 13;

    ///Returns the rank of a card (0=2, 1=3, ..., 11=King, 12=Ace)
    public static int GetRank(int card) => card % 13;
}
