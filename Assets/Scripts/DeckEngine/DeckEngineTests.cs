using System;
using UnityEngine;

/// <summary>
/// Lightweight in-editor test runner for Stage 1 DeckEngine.
/// Attach to any GameObject and hit Play, or call RunAll() from the Unity console.
/// Not a substitute for a proper test framework — purely for rapid validation.
/// </summary>
public class DeckEngineTests : MonoBehaviour
{
    void Start() => RunAll();

    public static void RunAll()
    {
        int passed = 0;
        int failed = 0;

        void Assert(string name, bool condition)
        {
            if (condition)
            {
                Debug.Log($"[PASS] {name}");
                passed++;
            }
            else
            {
                Debug.LogError($"[FAIL] {name}");
                failed++;
            }
        }

        // --- CreateOrderedDeck ---
        int[] ordered = DeckEngine.CreateOrderedDeck();
        Assert("CreateOrderedDeck: length 52", ordered.Length == 52);
        Assert("CreateOrderedDeck: starts at 0", ordered[0] == 0);
        Assert("CreateOrderedDeck: ends at 51", ordered[51] == 51);
        Assert("CreateOrderedDeck: sequential", ordered[26] == 26);

        // --- ValidateDeck ---
        Assert("ValidateDeck: valid deck", DeckEngine.ValidateDeck(ordered));
        Assert("ValidateDeck: null fails", !DeckEngine.ValidateDeck(null));
        Assert("ValidateDeck: wrong length fails", !DeckEngine.ValidateDeck(new int[51]));
        int[] duped = (int[])ordered.Clone(); duped[1] = 0;
        Assert("ValidateDeck: duplicate fails", !DeckEngine.ValidateDeck(duped));

        // --- CutDeck ---
        int[] cut = DeckEngine.CutDeck(ordered, 26);
        Assert("CutDeck: length preserved", cut.Length == 52);
        Assert("CutDeck: bottom half now at front", cut[0] == 26);
        Assert("CutDeck: top half now at back", cut[51] == 25);
        Assert("CutDeck: original unmutated", ordered[0] == 0);

        int[] smallCut = DeckEngine.CutDeck(new int[]{1,2,3,4,5,6,7,8,9,10,
            11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,
            27,28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,
            43,44,45,46,47,48,49,50,51,0}, 2);
        Assert("CutDeck: swap correctness", smallCut[0] == 3);

        // --- RiffleShuffle ---
        int[] riffle = DeckEngine.RiffleShuffle(ordered, 26, new int[]{1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,
                                                                         1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1});
        Assert("RiffleShuffle: valid deck output", DeckEngine.ValidateDeck(riffle));
        Assert("RiffleShuffle: first card from left half", riffle[0] == 0);
        Assert("RiffleShuffle: second card from right half", riffle[1] == 26);
        Assert("RiffleShuffle: original unmutated", ordered[0] == 0);

        // Drop pattern larger than remaining cards — engine should clamp gracefully
        int[] riffleClamp = DeckEngine.RiffleShuffle(ordered, 26, new int[]{52});
        Assert("RiffleShuffle: clamps to available cards", DeckEngine.ValidateDeck(riffleClamp));

        // --- OverhandShuffle ---
        int[] overhand = DeckEngine.OverhandShuffle(ordered, new int[]{5,5,5,5,5,5,5,5,5,5,2});
        Assert("OverhandShuffle: valid deck output", DeckEngine.ValidateDeck(overhand));
        Assert("OverhandShuffle: last chunk is now at front", overhand[0] == 50);
        Assert("OverhandShuffle: original unmutated", ordered[0] == 0);

        bool threwOnBadSum = false;
        try { DeckEngine.OverhandShuffle(ordered, new int[]{10, 10}); }
        catch (ArgumentException) { threwOnBadSum = true; }
        Assert("OverhandShuffle: throws on bad chunk sum", threwOnBadSum);

        // --- DealCards ---
        var (hands, remaining) = DeckEngine.DealCards(ordered, 4, 5);
        Assert("DealCards: correct number of hands", hands.Length == 4);
        Assert("DealCards: correct cards per hand", hands[0].Length == 5);
        Assert("DealCards: remaining deck size", remaining.Length == 32);
        Assert("DealCards: round-robin — player 0 card 0", hands[0][0] == 0);
        Assert("DealCards: round-robin — player 1 card 0", hands[1][0] == 1);
        Assert("DealCards: round-robin — player 0 card 1", hands[0][1] == 4);
        Assert("DealCards: remaining starts after dealt cards", remaining[0] == 20);
        Assert("DealCards: original unmutated", ordered[0] == 0);

        // --- CollectCards ---
        int[][] piles = { new int[]{0,1,2}, new int[]{3,4,5}, new int[]{6,7,8} };
        // Build a full 52-card pile set for validation
        int[][] fullPiles = new int[4][];
        fullPiles[0] = new int[13]; fullPiles[1] = new int[13];
        fullPiles[2] = new int[13]; fullPiles[3] = new int[13];
        for (int i = 0; i < 13; i++) { fullPiles[0][i] = i; fullPiles[1][i] = i+13; fullPiles[2][i] = i+26; fullPiles[3][i] = i+39; }
        int[] collected = DeckEngine.CollectCards(fullPiles, new int[]{3,2,1,0});
        Assert("CollectCards: valid deck output", DeckEngine.ValidateDeck(collected));
        Assert("CollectCards: order respected — first card from pile 3", collected[0] == 39);
        Assert("CollectCards: order respected — last card from pile 0", collected[51] == 12);

        // --- Helper functions ---
        Assert("GetSuit: Hearts", DeckEngine.GetSuit(0) == 0);
        Assert("GetSuit: Diamonds", DeckEngine.GetSuit(13) == 1);
        Assert("GetSuit: Clubs", DeckEngine.GetSuit(26) == 2);
        Assert("GetSuit: Spades", DeckEngine.GetSuit(39) == 3);
        Assert("GetRank: 2 of Hearts", DeckEngine.GetRank(0) == 0);
        Assert("GetRank: Ace of Hearts", DeckEngine.GetRank(12) == 12);
        Assert("GetRank: 2 of Spades", DeckEngine.GetRank(39) == 0);

        Debug.Log($"\n=== DeckEngine Tests: {passed} passed, {failed} failed ===");
    }
}
