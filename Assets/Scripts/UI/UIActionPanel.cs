using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// HUD buttons: a mouse-only alternative to the table controls. Cut, Riffle and Overhand start the
/// hands-on interactions when available; otherwise they make an automatic shuffle with natural-looking
/// parameters (the engine stays deterministic because the chosen parameters are what gets recorded).
/// Each button is enabled only when the active rules and the physical state allow its action.
public class UIActionPanel : MonoBehaviour
{
    private GameStateManager _sm;
    private readonly System.Random _rng = new System.Random();
    private HudHandlers _handlers = new HudHandlers();

    private Button _pickUpBtn, _cutBtn, _riffleBtn, _overhandBtn;
    private Button _packetBtn, _dealBtn, _putDownBtn, _collectBtn;
    private Button _undoBtn, _resetBtn, _menuBtn, _historyBtn;
    private Text   _packetLabel;

    private void Awake()
    {
        var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing              = 6f;
        layout.childAlignment       = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = true;
        layout.padding = new RectOffset(8, 8, 6, 6);

        _menuBtn     = Btn("Menu");
        _pickUpBtn   = Btn("Pick up");
        _cutBtn      = Btn("Cut");
        _riffleBtn   = Btn("Riffle");
        _overhandBtn = Btn("Overhand");
        _packetBtn   = Btn("Packet: 1");
        _packetLabel = _packetBtn.GetComponentInChildren<Text>();
        _dealBtn     = Btn("Deal");
        _putDownBtn  = Btn("Put down");
        _collectBtn  = Btn("Collect");
        _undoBtn     = Btn("Undo");
        _resetBtn    = Btn("Reset");
        _historyBtn  = Btn("History");
    }

    public void Initialise(GameStateManager sm, HudHandlers handlers = null)
    {
        _sm = sm;
        _handlers = handlers ?? new HudHandlers();
        _menuBtn.gameObject.SetActive(_handlers.Menu != null);
        _historyBtn.gameObject.SetActive(_handlers.History != null);
        _menuBtn.onClick.AddListener(()     => _handlers.Menu?.Invoke());
        _historyBtn.onClick.AddListener(()  => _handlers.History?.Invoke());
        _pickUpBtn.onClick.AddListener(()   => Apply(new PickUpDeckOperation()));
        _cutBtn.onClick.AddListener(()      => Interactive(_handlers.Cut, CutOp));
        _riffleBtn.onClick.AddListener(()   => Interactive(_handlers.Riffle, RiffleOp));
        _overhandBtn.onClick.AddListener(() => Interactive(_handlers.Overhand, OverhandOp));
        _packetBtn.onClick.AddListener(CyclePacketSize);
        _dealBtn.onClick.AddListener(()     => Apply(DealOp()));
        _putDownBtn.onClick.AddListener(()  => Apply(new PutDownDeckOperation()));
        _collectBtn.onClick.AddListener(()  => Apply(CollectOperation.All()));
        _undoBtn.onClick.AddListener(()     => _sm.Undo());
        _resetBtn.onClick.AddListener(()    => _sm.Reset());
    }

    public void RefreshButtons(GameStateSnapshot s)
    {
        if (_sm == null) return;
        _pickUpBtn.interactable   = Can(new PickUpDeckOperation());
        _cutBtn.interactable      = Can(CutOp());
        _riffleBtn.interactable   = Can(RiffleOp());
        _overhandBtn.interactable = Can(OverhandOp());
        var dealing = Dealing;
        _packetBtn.interactable   = dealing != null;
        if (dealing != null) _packetLabel.text = $"Packet: {dealing.PacketSize}";
        _dealBtn.interactable     = DealOp() != null && Can(DealOp());
        _putDownBtn.interactable  = Can(new PutDownDeckOperation());
        _collectBtn.interactable  = Can(CollectOperation.All());
        _undoBtn.interactable     = _sm.CanUndo;
        _resetBtn.interactable    = true;
    }

    // ---- Operations ----

    /// Shuffles act on the deck in the dealer's hand if they're holding it, otherwise on the table.
    private ZoneId Source => _sm.Table.Count(ZoneId.RightHand) > 0 ? ZoneId.RightHand : ZoneId.Deck;
    private int    Size   => _sm.Table.Count(Source);

    private GameOperation CutOp()
    {
        int n = Size;
        int position = Mathf.Clamp(n / 2 + _rng.Next(-n / 8, n / 8 + 1), 1, Mathf.Max(1, n - 1));
        return new CutOperation(position, Source);
    }

    private GameOperation RiffleOp()
    {
        int n = Size;
        int split = Mathf.Clamp(n / 2 + _rng.Next(-3, 4), 1, Mathf.Max(1, n - 1));
        var drops = new int[Mathf.Max(1, n)];
        for (int i = 0; i < drops.Length; i++) drops[i] = _rng.Next(1, 4);
        return new RiffleOperation(split, drops, Source);
    }

    private GameOperation OverhandOp()
    {
        int n = Size;
        var chunks = new List<int>();
        for (int left = n; left > 0;)
        {
            int chunk = Mathf.Min(left, _rng.Next(3, 10));
            chunks.Add(chunk);
            left -= chunk;
        }
        if (chunks.Count == 0) chunks.Add(0); // empty packet: rejected as impossible
        return new OverhandOperation(chunks.ToArray(), Source);
    }

    /// Starts the hands-on version if there is one and it can start; otherwise acts automatically.
    private void Interactive(System.Func<bool> start, System.Func<GameOperation> automatic)
    {
        if (start != null && start()) return;
        Apply(automatic());
    }

    /// The dealing round of whatever game is running (Free Play, Johoti, Blackjack…), if it has one.
    private DealerRules Dealing => (_sm.Rules as IDealingRuleSet)?.Dealing;

    private GameOperation DealOp()
    {
        var dealer = Dealing;
        return dealer?.NextPacket(_sm.Table);
    }

    private void CyclePacketSize()
    {
        var dealer = Dealing;
        if (dealer == null) return;
        dealer.PacketSize = dealer.PacketSize % dealer.CardsPerPlayer + 1;
        RefreshButtons(default);
    }

    private void Apply(GameOperation op)
    {
        if (op != null) _sm?.Apply(op);
    }

    private bool Can(GameOperation op) => _sm.CanApply(op).Success;

    // ---- Construction ----

    private Button Btn(string label)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = 64f;
        le.preferredHeight = 30f;

        go.GetComponent<Image>().color = new Color(0.28f, 0.28f, 0.40f);

        var txtGO = new GameObject("T", typeof(RectTransform), typeof(Text));
        txtGO.transform.SetParent(go.transform, false);
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        var txt = txtGO.GetComponent<Text>();
        txt.font      = HudStyle.DefaultFont;
        txt.fontSize  = 12;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = Color.white;
        txt.text      = label;

        var btn = go.GetComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = new Color(0.28f, 0.28f, 0.40f);
        cb.highlightedColor = new Color(0.38f, 0.38f, 0.55f);
        cb.pressedColor     = new Color(0.20f, 0.20f, 0.30f);
        cb.disabledColor    = new Color(0.20f, 0.20f, 0.20f, 0.5f);
        btn.colors = cb;

        return btn;
    }
}
