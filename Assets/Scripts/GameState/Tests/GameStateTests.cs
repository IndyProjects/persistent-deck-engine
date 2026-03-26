using System;
using UnityEngine;

/// Stage 3 test runner. Run with Tools → GameState → Run Tests, or attach to a GameObject and enter Play mode.
public class GameStateTests : MonoBehaviour
{
    void Start() => RunAll();

    public static void RunAll()
    {
        int passed = 0, failed = 0;

        void Assert(string name, bool condition)
        {
            if (condition) { Debug.Log($"[PASS] {name}"); passed++; }
            else           { Debug.LogError($"[FAIL] {name}"); failed++; }
        }


        // --- Initial state ---
        var mgr = new GameStateManager();
        Assert("Initial: Phase == Idle",          mgr.Phase == GamePhase.Idle);
        Assert("Initial: RoundNumber == 0",       mgr.RoundNumber == 0);
        Assert("Initial: Deck valid",             DeckEngine.ValidateDeck(mgr.CurrentDeck));
        Assert("Initial: Hands empty",            mgr.CurrentHands.Length == 0);
        Assert("Initial: CanUndo == false",       !mgr.CanUndo);


        // --- Cut in Idle ---
        var cutOk = mgr.Apply(new CutOperation(26));
        Assert("Cut: Success",                    cutOk.Success);
        Assert("Cut: Phase still Idle",           mgr.Phase == GamePhase.Idle);
        Assert("Cut: Deck still valid",           DeckEngine.ValidateDeck(mgr.CurrentDeck));
        Assert("Cut: Deck actually cut",          mgr.CurrentDeck[0] == 26);
        Assert("Cut: CanUndo after apply",        mgr.CanUndo);


        // --- Riffle in Idle ---
        mgr.Reset();
        int[] drops = new int[52];
        for (int i = 0; i < 52; i++) drops[i] = 1;
        var riffleOk = mgr.Apply(new RiffleOperation(26, drops));
        Assert("Riffle: Success",                 riffleOk.Success);
        Assert("Riffle: Deck valid",              DeckEngine.ValidateDeck(mgr.CurrentDeck));


        // --- Overhand in Idle ---
        mgr.Reset();
        var overhandOk = mgr.Apply(new OverhandOperation(new[] { 13, 13, 13, 13 }));
        Assert("Overhand: Success",               overhandOk.Success);
        Assert("Overhand: Deck valid",            DeckEngine.ValidateDeck(mgr.CurrentDeck));


        // --- Deal: Idle → InPlay ---
        mgr.Reset();
        var dealOk = mgr.Apply(new DealOperation(4, 5));
        Assert("Deal: Success",                   dealOk.Success);
        Assert("Deal: Phase == InPlay",           mgr.Phase == GamePhase.InPlay);
        Assert("Deal: 4 hands",                   mgr.CurrentHands.Length == 4);
        Assert("Deal: Each hand has 5 cards",     mgr.CurrentHands[0].Length == 5);
        Assert("Deal: Remaining deck = 32",       mgr.CurrentDeck.Length == 32);
        Assert("Deal: RoundNumber unchanged",     mgr.RoundNumber == 0);


        // --- Shuffle blocked while InPlay ---
        var cutBlocked = mgr.Apply(new CutOperation(16));
        Assert("Cut blocked InPlay",              !cutBlocked.Success);
        Assert("Phase unchanged after rejection", mgr.Phase == GamePhase.InPlay);

        var riffleBlocked = mgr.Apply(new RiffleOperation(26, drops));
        Assert("Riffle blocked InPlay",           !riffleBlocked.Success);

        var overhandBlocked = mgr.Apply(new OverhandOperation(new[] { 13, 13, 13, 13 }));
        Assert("Overhand blocked InPlay",         !overhandBlocked.Success);


        // --- Double deal blocked ---
        var doubleDeal = mgr.Apply(new DealOperation(2, 3));
        Assert("Double deal blocked",             !doubleDeal.Success);


        // --- Collect: InPlay → Idle ---
        // Assemble all piles (4 hands + remaining deck)
        var allPiles = new int[mgr.CurrentHands.Length + 1][];
        for (int i = 0; i < mgr.CurrentHands.Length; i++) allPiles[i] = mgr.CurrentHands[i];
        allPiles[mgr.CurrentHands.Length] = mgr.CurrentDeck;
        var fullOrder = new int[allPiles.Length];
        for (int i = 0; i < fullOrder.Length; i++) fullOrder[i] = i;

        var collectOk = mgr.Apply(new CollectOperation(allPiles, fullOrder));
        Assert("Collect: Success",                collectOk.Success);
        Assert("Collect: Phase == Idle",          mgr.Phase == GamePhase.Idle);
        Assert("Collect: Hands empty",            mgr.CurrentHands.Length == 0);
        Assert("Collect: Deck valid",             DeckEngine.ValidateDeck(mgr.CurrentDeck));
        Assert("Collect: RoundNumber == 1",       mgr.RoundNumber == 1);


        // --- Collect blocked while Idle ---
        var collectBlocked = mgr.Apply(new CollectOperation(new[] { mgr.CurrentDeck }, new[] { 0 }));
        Assert("Collect blocked Idle",            !collectBlocked.Success);


        // --- Undo ---
        mgr.Reset();
        var deckBefore = (int[])mgr.CurrentDeck.Clone();
        mgr.Apply(new CutOperation(26));
        var undoOk = mgr.Undo();
        Assert("Undo: Success",                   undoOk.Success);
        Assert("Undo: Deck restored",             mgr.CurrentDeck[0] == deckBefore[0]);
        Assert("Undo: CanUndo false after undo",  !mgr.CanUndo);


        // --- Undo from empty ---
        var emptyUndo = mgr.Undo();
        Assert("Undo: Rejected when empty",       !emptyUndo.Success);


        // --- CanApply dry-run ---
        mgr.Reset();
        Assert("CanApply: Cut ok in Idle",        mgr.CanApply(new CutOperation(26)).Success);
        mgr.Apply(new DealOperation(2, 5));
        Assert("CanApply: Cut blocked InPlay",    !mgr.CanApply(new CutOperation(26)).Success);
        Assert("CanApply: Collect ok InPlay",     mgr.CanApply(new CollectOperation(Array.Empty<int[]>(), Array.Empty<int>())).Success);


        // --- Reset clears history and round ---
        mgr.Reset();
        Assert("Reset: Phase == Idle",            mgr.Phase == GamePhase.Idle);
        Assert("Reset: RoundNumber == 0",         mgr.RoundNumber == 0);
        Assert("Reset: CanUndo == false",         !mgr.CanUndo);
        Assert("Reset: Deck valid",               DeckEngine.ValidateDeck(mgr.CurrentDeck));


        // --- Multi-round round trip ---
        mgr.Reset();
        mgr.Apply(new DealOperation(2, 5));
        var r2Piles = new int[mgr.CurrentHands.Length + 1][];
        for (int i = 0; i < mgr.CurrentHands.Length; i++) r2Piles[i] = mgr.CurrentHands[i];
        r2Piles[mgr.CurrentHands.Length] = mgr.CurrentDeck;
        var r2Order = new int[r2Piles.Length];
        for (int i = 0; i < r2Order.Length; i++) r2Order[i] = i;
        mgr.Apply(new CollectOperation(r2Piles, r2Order));
        mgr.Apply(new DealOperation(2, 5));
        var r3Piles = new int[mgr.CurrentHands.Length + 1][];
        for (int i = 0; i < mgr.CurrentHands.Length; i++) r3Piles[i] = mgr.CurrentHands[i];
        r3Piles[mgr.CurrentHands.Length] = mgr.CurrentDeck;
        mgr.Apply(new CollectOperation(r3Piles, r2Order));
        Assert("Multi-round: RoundNumber == 2",   mgr.RoundNumber == 2);
        Assert("Multi-round: Deck valid",         DeckEngine.ValidateDeck(mgr.CurrentDeck));


        Debug.Log($"\n=== GameState Tests: {passed} passed, {failed} failed ===");
    }
}
