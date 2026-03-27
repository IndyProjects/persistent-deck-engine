using System;
using System.Collections.Generic;
using UnityEngine;

/// Stage 2 test runner — tests parameter generators with synthetic GestureEvent sequences.
///  No input hardware required. Run with Tools → InputMapping → Run Tests, or attach to a GameObject and enter Play mode.
public class InputMappingTests : MonoBehaviour
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


        var cutGen = new CutParamGenerator();

        var cutResult = cutGen.Generate(Events(Cut(0.5f)));
        Assert("Cut: Valid — mid-deck", cutResult.Status == ParameterStatus.Valid && cutResult.Params.Position == 26);

        var cutLow = cutGen.Generate(Events(Cut(0f)));
        Assert("Cut: Clamped — norm=0 clamps to position=1", cutLow.Status == ParameterStatus.Clamped && cutLow.Params.Position == 1);

        var cutHigh = cutGen.Generate(Events(Cut(1f)));
        Assert("Cut: Valid — norm=1 → position=51", cutHigh.Status == ParameterStatus.Valid && cutHigh.Params.Position == 51);

        var cutNaN = cutGen.Generate(Events(Cut(float.NaN)));
        Assert("Cut: Rejected — NaN payload", cutNaN.Status == ParameterStatus.Rejected);

        var cutEmpty = cutGen.Generate(new List<GestureEvent>());
        Assert("Cut: Rejected — empty events", cutEmpty.Status == ParameterStatus.Rejected);


        var riffleGen = new RiffleParamGenerator();

        var events26 = new List<GestureEvent>();
        events26.Add(RiffleStart(0.5f));
        for (int i = 0; i < 52; i++) events26.Add(RiffleDrop(i % 2 == 0 ? ShuffleSide.Left : ShuffleSide.Right, 1));
        var riffleResult = riffleGen.Generate(events26);
        Assert("Riffle: Valid — 52 single-card drops", riffleResult.Status == ParameterStatus.Valid);
        Assert("Riffle: SplitIndex == 26", riffleResult.Params.SplitIndex == 26);
        Assert("Riffle: DropPattern length == 52", riffleResult.Params.DropPattern.Length == 52);

        var riffleClamp = riffleGen.Generate(Events(RiffleStart(0f), RiffleDrop(ShuffleSide.Left, 1)));
        Assert("Riffle: Clamped — norm=0 splitIndex clamps to 1", riffleClamp.Status == ParameterStatus.Clamped && riffleClamp.Params.SplitIndex == 1);

        var riffleNoDrops = riffleGen.Generate(Events(RiffleStart(0.5f)));
        Assert("Riffle: Rejected — no drops", riffleNoDrops.Status == ParameterStatus.Rejected);

        var riffleBadDrop = riffleGen.Generate(Events(RiffleStart(0.5f), RiffleDrop(ShuffleSide.Left, 0)));
        Assert("Riffle: Rejected — CardCount=0", riffleBadDrop.Status == ParameterStatus.Rejected);


        var overhandGen = new OverhandParamGenerator();

        var os52 = new OverhandSession();
        os52.AddFixedChunk(26);
        os52.AddFixedChunk(26);
        Assert("Overhand: session auto-completes at 52 cards", os52.IsComplete);
        var ohResult = overhandGen.GenerateFromSession(os52);
        Assert("Overhand: Valid — two chunks of 26", ohResult.Status == ParameterStatus.Valid);
        Assert("Overhand: ChunkSizes sum == 52", ohResult.Params.ChunkSizes[0] + ohResult.Params.ChunkSizes[1] == 52);

        var osBad = new OverhandSession();
        osBad.AddFixedChunk(10); // not complete
        var ohBad = overhandGen.GenerateFromSession(osBad);
        Assert("Overhand: Rejected — incomplete (sum != 52)", ohBad.Status == ParameterStatus.Rejected);

        var osCancel = new OverhandSession();
        osCancel.Cancel();
        Assert("Overhand: Rejected — cancelled session", overhandGen.GenerateFromSession(osCancel).Status == ParameterStatus.Rejected);

        var dealGen = new DealParamGenerator();

        var dealResult = dealGen.Generate(Events(DealStart(4, 5)));
        Assert("Deal: Valid — 4 players × 5 cards", dealResult.Status == ParameterStatus.Valid);
        Assert("Deal: Players == 4", dealResult.Params.Players == 4);
        Assert("Deal: CardsEach == 5", dealResult.Params.CardsEach == 5);

        var dealClamp = dealGen.Generate(Events(DealStart(4, 20))); // 4×20=80 > 52
        Assert("Deal: Clamped — product > 52", dealClamp.Status == ParameterStatus.Clamped);
        Assert("Deal: CardsEach reduced", dealClamp.Params.CardsEach == 13);

        Assert("Deal: Rejected — 0 players", dealGen.Generate(Events(DealStart(0, 5))).Status == ParameterStatus.Rejected);
        Assert("Deal: Rejected — 0 cardsEach", dealGen.Generate(Events(DealStart(4, 0))).Status == ParameterStatus.Rejected);


        var collectGen = new CollectParamGenerator();
        var testPiles = new int[][] {
            new int[]{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 },
            new int[]{ 13,14,15,16,17,18,19,20,21,22,23,24,25 },
            new int[]{ 26,27,28,29,30,31,32,33,34,35,36,37,38 },
            new int[]{ 39,40,41,42,43,44,45,46,47,48,49,50,51 }
        };

        var cs = new CollectSession(4);
        cs.AddEvent(CollectPile(3));
        cs.AddEvent(CollectPile(2));
        cs.AddEvent(CollectPile(1));
        cs.AddEvent(CollectPile(0));
        Assert("Collect: session completes at 4 piles", cs.IsComplete);
        var collectResult = collectGen.GenerateFromSession(cs, testPiles);
        Assert("Collect: Valid", collectResult.Status == ParameterStatus.Valid);
        Assert("Collect: Order=[3,2,1,0]", collectResult.Params.Order[0] == 3 && collectResult.Params.Order[3] == 0);

        var csDup = new CollectSession(4);
        csDup.AddEvent(CollectPile(0));
        csDup.AddEvent(CollectPile(0)); // duplicate
        Assert("Collect: session cancelled on duplicate", csDup.IsCancelled);

        var csRange = new CollectSession(4);
        csRange.AddEvent(CollectPile(99)); // out of range
        Assert("Collect: session cancelled on out-of-range index", csRange.IsCancelled);

        var sess = new GestureSession(GestureType.Cut);
        sess.Cancel();
        sess.AddEvent(Cut(0.5f)); // should be ignored after cancel
        Assert("Session: events ignored after cancel", sess.Events.Count == 0);


        var riffSess = new RiffleSession();
        riffSess.AddEvent(RiffleStart(0.5f));
        riffSess.AddEvent(RiffleDrop(ShuffleSide.Right, 1)); // wrong side first
        Assert("RiffleSession: cancelled on wrong starting side", riffSess.IsCancelled);

        var deck = DeckEngine.CreateOrderedDeck();
        var cr   = cutGen.Generate(Events(Cut(0.5f)));
        Assert("Integration: Cut result valid", cr.Status != ParameterStatus.Rejected);
        var cutDeck = DeckEngine.CutDeck(deck, cr.Params.Position);
        Assert("Integration: Cut produces valid deck", DeckEngine.ValidateDeck(cutDeck));
        Assert("Integration: Cut bottom half now at front", cutDeck[0] == cr.Params.Position);

        Debug.Log($"\n=== InputMapping Tests: {passed} passed, {failed} failed ===");
    }


    private static List<GestureEvent> Events(params GestureEvent[] evts)
        => new List<GestureEvent>(evts);

    private static GestureEvent Cut(float norm) => new GestureEvent
    {
        Type = GestureType.Cut, Source = InputSource.Keyboard,
        Phase = GesturePhase.Ended, Timestamp = 0,
        Payload = new CutPayload { NormalizedPosition = norm }
    };

    private static GestureEvent RiffleStart(float norm) => new GestureEvent
    {
        Type = GestureType.RiffleStart, Source = InputSource.Keyboard,
        Phase = GesturePhase.Began, Timestamp = 0,
        Payload = new RiffleStartPayload { NormalizedSplitPosition = norm }
    };

    private static GestureEvent RiffleDrop(ShuffleSide side, int count) => new GestureEvent
    {
        Type = GestureType.RiffleDrop, Source = InputSource.Keyboard,
        Phase = GesturePhase.Ended, Timestamp = 0,
        Payload = new RiffleDropPayload { Side = side, CardCount = count }
    };

    private static GestureEvent DealStart(int players, int cardsEach) => new GestureEvent
    {
        Type = GestureType.DealStart, Source = InputSource.Keyboard,
        Phase = GesturePhase.Ended, Timestamp = 0,
        Payload = new DealStartPayload { PlayerCount = players, CardsEachCount = cardsEach }
    };

    private static GestureEvent CollectPile(int index) => new GestureEvent
    {
        Type = GestureType.CollectPile, Source = InputSource.Keyboard,
        Phase = GesturePhase.Ended, Timestamp = 0,
        Payload = new CollectPilePayload { PileIndex = index }
    };
}
