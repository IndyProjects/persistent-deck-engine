using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// Parameter generators, sessions and pointer arbitration, driven by synthetic events (no hardware).
public class InputMappingTests
{
    // ---- Cut ----

    [Test]
    public void Cut_NormalizedPositionMapsToDeck()
    {
        var gen = new CutParamGenerator();
        var mid = gen.Generate(Events(Cut(0.5f)));
        Assert.AreEqual(ParameterStatus.Valid, mid.Status);
        Assert.AreEqual(26, mid.Params.Position);

        var low = gen.Generate(Events(Cut(0f)));
        Assert.AreEqual(ParameterStatus.Clamped, low.Status);
        Assert.AreEqual(1, low.Params.Position);

        var high = gen.Generate(Events(Cut(1f)));
        Assert.AreEqual(ParameterStatus.Valid, high.Status);
        Assert.AreEqual(51, high.Params.Position);

        Assert.AreEqual(ParameterStatus.Rejected, gen.Generate(Events(Cut(float.NaN))).Status);
        Assert.AreEqual(ParameterStatus.Rejected, gen.Generate(new List<GestureEvent>()).Status);
    }

    [Test]
    public void Cut_UsesCurrentDeckSize()
    {
        var gen = new CutParamGenerator { DeckSize = 42 };
        Assert.AreEqual(41, gen.Generate(Events(Cut(1f))).Params.Position);
        Assert.AreEqual(20, gen.Generate(Events(Cut(0.5f))).Params.Position, "0.5 × 41 = 20.5 rounds half-to-even");

        gen.DeckSize = 1;
        Assert.AreEqual(ParameterStatus.Rejected, gen.Generate(Events(Cut(0.5f))).Status);
    }

    // ---- Riffle ----

    [Test]
    public void Riffle_GeneratesSplitAndPattern()
    {
        var gen = new RiffleParamGenerator();
        var events = new List<GestureEvent> { RiffleStart(0.5f) };
        for (int i = 0; i < 52; i++) events.Add(RiffleDrop(i % 2 == 0 ? ShuffleSide.Left : ShuffleSide.Right, 1));
        var r = gen.Generate(events);
        Assert.AreEqual(ParameterStatus.Valid, r.Status);
        Assert.AreEqual(26, r.Params.SplitIndex);
        Assert.AreEqual(52, r.Params.DropPattern.Length);

        var clamp = gen.Generate(Events(RiffleStart(0f), RiffleDrop(ShuffleSide.Left, 1)));
        Assert.AreEqual(ParameterStatus.Clamped, clamp.Status);
        Assert.AreEqual(1, clamp.Params.SplitIndex);

        Assert.AreEqual(ParameterStatus.Rejected, gen.Generate(Events(RiffleStart(0.5f))).Status, "no drops");
        Assert.AreEqual(ParameterStatus.Rejected,
            gen.Generate(Events(RiffleStart(0.5f), RiffleDrop(ShuffleSide.Left, 0))).Status, "zero-card drop");
    }

    [Test]
    public void RiffleSession_CancelsOnWrongStartingSide()
    {
        var s = new RiffleSession();
        s.AddEvent(RiffleStart(0.5f));
        s.AddEvent(RiffleDrop(ShuffleSide.Right, 1));
        Assert.IsTrue(s.IsCancelled);
    }

    [Test]
    public void RiffleSession_CompletesForPartialDeck()
    {
        var s = new RiffleSession(10);
        s.AddEvent(RiffleStart(0.5f));
        Assert.That(s.SplitIndex, Is.InRange(1, 9), "split maps into the 10-card deck");

        // Single-card drops; an exhausted side's turns drop nothing, so allow extra turns
        for (int i = 0; i < 20 && !s.IsComplete; i++)
            s.AddEvent(RiffleDrop(i % 2 == 0 ? ShuffleSide.Left : ShuffleSide.Right, 1));
        Assert.IsTrue(s.IsComplete);
        Assert.AreEqual(0, s.LeftRemaining + s.RightRemaining);
    }

    // ---- Overhand ----

    [Test]
    public void Overhand_SessionCompletesAtDeckSize()
    {
        var gen = new OverhandParamGenerator();
        var full = new OverhandSession();
        full.AddFixedChunk(26);
        full.AddFixedChunk(26);
        Assert.IsTrue(full.IsComplete);
        var r = gen.GenerateFromSession(full);
        Assert.AreEqual(ParameterStatus.Valid, r.Status);
        CollectionAssert.AreEqual(new[] { 26, 26 }, r.Params.ChunkSizes);

        var partial = new OverhandSession();
        partial.AddFixedChunk(10);
        Assert.AreEqual(ParameterStatus.Rejected, gen.GenerateFromSession(partial).Status);

        var cancelled = new OverhandSession();
        cancelled.Cancel();
        Assert.AreEqual(ParameterStatus.Rejected, gen.GenerateFromSession(cancelled).Status);
    }

    [Test]
    public void Overhand_UsesCurrentDeckSize()
    {
        var s = new OverhandSession(42);
        s.AddFixedChunk(40);
        s.AddFixedChunk(4); // clamped to the 2 remaining
        Assert.IsTrue(s.IsComplete);
        CollectionAssert.AreEqual(new[] { 40, 2 }, new OverhandParamGenerator().GenerateFromSession(s).Params.ChunkSizes);

        Assert.IsTrue(new OverhandSession(0).IsCancelled, "nothing to shuffle");
    }

    // ---- Deal ----

    [Test]
    public void Deal_ValidatesAndClamps()
    {
        var gen = new DealParamGenerator();
        var r = gen.Generate(Events(DealStart(4, 5)));
        Assert.AreEqual(ParameterStatus.Valid, r.Status);
        Assert.AreEqual(4, r.Params.Players);
        Assert.AreEqual(5, r.Params.CardsEach);

        var clamp = gen.Generate(Events(DealStart(4, 20)));
        Assert.AreEqual(ParameterStatus.Clamped, clamp.Status);
        Assert.AreEqual(13, clamp.Params.CardsEach);

        Assert.AreEqual(ParameterStatus.Rejected, gen.Generate(Events(DealStart(0, 5))).Status);
        Assert.AreEqual(ParameterStatus.Rejected, gen.Generate(Events(DealStart(4, 0))).Status);

        gen.DeckSize = 3;
        Assert.AreEqual(ParameterStatus.Rejected, gen.Generate(Events(DealStart(4, 1))).Status, "fewer cards than players");
    }

    // ---- Collect ----

    [Test]
    public void Collect_SessionTracksOrder()
    {
        var piles = new int[4][];
        for (int p = 0; p < 4; p++) piles[p] = new int[13];

        var cs = new CollectSession(4);
        foreach (int i in new[] { 3, 2, 1, 0 }) cs.AddEvent(CollectPile(i));
        Assert.IsTrue(cs.IsComplete);
        var r = new CollectParamGenerator().GenerateFromSession(cs, piles);
        Assert.AreEqual(ParameterStatus.Valid, r.Status);
        CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, r.Params.Order);

        var dup = new CollectSession(4);
        dup.AddEvent(CollectPile(0));
        dup.AddEvent(CollectPile(0));
        Assert.IsTrue(dup.IsCancelled);

        var range = new CollectSession(4);
        range.AddEvent(CollectPile(99));
        Assert.IsTrue(range.IsCancelled);
    }

    [Test]
    public void SessionManager_ReadsLivePileCount()
    {
        int piles = 3;
        var sm = new GestureSessionManager { PileCount = () => piles };
        var ended = new List<IGestureSession>();
        sm.SessionEnded += ended.Add;

        foreach (int i in new[] { 2, 0, 1 }) sm.OnGestureEvent(CollectPile(i));
        Assert.AreEqual(1, ended.Count);
        Assert.IsTrue(ended[0].IsComplete);
    }

    [Test]
    public void Session_IgnoresEventsAfterCancel()
    {
        var s = new GestureSession(GestureType.Cut);
        s.Cancel();
        s.AddEvent(Cut(0.5f));
        Assert.AreEqual(0, s.Events.Count);
    }

    [Test]
    public void SessionManager_ReplacingRiffle_KeepsOneActive()
    {
        var sm = new GestureSessionManager();
        bool activeDuringCancel = false;
        sm.SessionEnded += s => activeDuringCancel = s.IsCancelled && sm.IsActive(GestureType.RiffleStart);
        sm.OnGestureEvent(RiffleStart(0.5f));
        sm.OnGestureEvent(RiffleStart(0.3f));
        Assert.IsTrue(activeDuringCancel);
    }

    // ---- Pointer arbitration: one physical gesture → exactly one recognized gesture ----

    private GestureRecognizerDispatcher _dispatcher;
    private RiffleRecognizer            _riffle;
    private FakePointerReader           _pointer;
    private List<GestureType>           _seen;

    [SetUp]
    public void BuildDispatcher()
    {
        _dispatcher = new GestureRecognizerDispatcher();
        _riffle     = new RiffleRecognizer();
        _dispatcher.Register(new CutRecognizer());
        _dispatcher.Register(_riffle);
        _dispatcher.Register(new OverhandRecognizer());
        _pointer = new FakePointerReader();
        _dispatcher.Subscribe(_pointer);
        _seen = new List<GestureType>();
        _dispatcher.GestureRecognized += g => _seen.Add(g.Type);
    }

    [Test]
    public void Arbitration_HorizontalDrag_IsCutOnly()
    {
        _pointer.Drag(new Vector2(100, 300), new Vector2(400, 320), 0, 0.2);
        CollectionAssert.AreEqual(new[] { GestureType.Cut }, _seen);
    }

    [Test]
    public void Arbitration_VerticalDrag_IsOverhandOnly()
    {
        _pointer.Drag(new Vector2(300, 100), new Vector2(310, 400), 0, 0.2);
        CollectionAssert.AreEqual(new[] { GestureType.OverhandGrab, GestureType.OverhandRelease }, _seen);
    }

    [Test]
    public void Arbitration_ShortTapWithoutRiffle_DoesNothing()
    {
        _pointer.Tap(new Vector2(300, 300), 0, 0.1);
        Assert.IsEmpty(_seen);
    }

    [Test]
    public void Arbitration_RiffleCapturesPointerUntilEnded()
    {
        _pointer.Tap(new Vector2(300, 300), 0, InputConstants.LongPressSeconds);
        CollectionAssert.AreEqual(new[] { GestureType.RiffleStart }, _seen);
        Assert.IsTrue(_riffle.CapturesPointer);

        _seen.Clear();
        _pointer.Drag(new Vector2(100, 300), new Vector2(400, 320), 1, 1.2);
        Assert.IsEmpty(_seen, "drag during riffle is ignored");

        _pointer.Tap(new Vector2(300, 300), 2, 2.1);
        CollectionAssert.AreEqual(new[] { GestureType.RiffleDrop }, _seen);

        _riffle.EndRiffle();
        _seen.Clear();
        _pointer.Drag(new Vector2(100, 300), new Vector2(400, 320), 3, 3.2);
        CollectionAssert.AreEqual(new[] { GestureType.Cut }, _seen);
    }

    // ---- Helpers ----

    /// Emits synthetic pointer sequences in the same shape MouseInputReader produces.
    private class FakePointerReader : IInputReader
    {
        public event Action<RawPointerEvent> PointerEvent;
        public event Action<RawAxisEvent>    AxisEvent   { add { } remove { } }
        public event Action<RawButtonEvent>  ButtonEvent { add { } remove { } }

        public void Enable()  { }
        public void Disable() { }
        public void Poll()    { }

        public void Drag(Vector2 from, Vector2 to, double t0, double t1)
        {
            Emit(from, GesturePhase.Began,   t0);
            Emit(to,   GesturePhase.Updated, (t0 + t1) * 0.5);
            Emit(to,   GesturePhase.Ended,   t1);
        }

        public void Tap(Vector2 at, double t0, double t1)
        {
            Emit(at, GesturePhase.Began, t0);
            Emit(at, GesturePhase.Ended, t1);
        }

        private void Emit(Vector2 pos, GesturePhase phase, double t)
            => PointerEvent?.Invoke(new RawPointerEvent
            {
                Position = pos, Phase = phase, Timestamp = t,
                Pressure = 1f, Source = InputSource.Mouse
            });
    }

    private static List<GestureEvent> Events(params GestureEvent[] evts) => new List<GestureEvent>(evts);

    private static GestureEvent Ev(GestureType type, GesturePhase phase, IGesturePayload payload) => new GestureEvent
        { Type = type, Source = InputSource.Keyboard, Phase = phase, Timestamp = 0, Payload = payload };

    private static GestureEvent Cut(float norm)
        => Ev(GestureType.Cut, GesturePhase.Ended, new CutPayload { NormalizedPosition = norm });
    private static GestureEvent RiffleStart(float norm)
        => Ev(GestureType.RiffleStart, GesturePhase.Began, new RiffleStartPayload { NormalizedSplitPosition = norm });
    private static GestureEvent RiffleDrop(ShuffleSide side, int count)
        => Ev(GestureType.RiffleDrop, GesturePhase.Ended, new RiffleDropPayload { Side = side, CardCount = count });
    private static GestureEvent DealStart(int players, int cardsEach)
        => Ev(GestureType.DealStart, GesturePhase.Ended, new DealStartPayload { PlayerCount = players, CardsEachCount = cardsEach });
    private static GestureEvent CollectPile(int index)
        => Ev(GestureType.CollectPile, GesturePhase.Ended, new CollectPilePayload { PileIndex = index });
}
