using UnityEngine;
using UnityEngine.UI;

public class UIActionPanel : MonoBehaviour
{
    private GameStateManager _sm;

    private Button _dealBtn;
    private Button _cutBtn;
    private Button _shuffleBtn;
    private Button _collectBtn;
    private Button _undoBtn;
    private Button _resetBtn;

    private void Awake()
    {
        var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing              = 6f;
        layout.childAlignment       = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = true;
        layout.padding = new RectOffset(8, 8, 6, 6);

        _dealBtn    = Btn("Deal 2p/5c");
        _cutBtn     = Btn("Cut");
        _shuffleBtn = Btn("Shuffle");
        _collectBtn = Btn("Collect");
        _undoBtn    = Btn("Undo");
        _resetBtn   = Btn("Reset");
    }

    public void Initialise(GameStateManager sm)
    {
        _sm = sm;
        _dealBtn.onClick.AddListener(OnDeal);
        _cutBtn.onClick.AddListener(OnCut);
        _shuffleBtn.onClick.AddListener(OnShuffle);
        _collectBtn.onClick.AddListener(OnCollect);
        _undoBtn.onClick.AddListener(() => _sm.Undo());
        _resetBtn.onClick.AddListener(() => _sm.Reset());
    }

    public void RefreshButtons(GameStateSnapshot s)
    {
        bool idle   = s.Phase == GamePhase.Idle;
        bool inPlay = s.Phase == GamePhase.InPlay;
        _dealBtn.interactable    = idle;
        _cutBtn.interactable     = idle;
        _shuffleBtn.interactable = idle;
        _collectBtn.interactable = inPlay;
        _undoBtn.interactable    = _sm != null && _sm.CanUndo;
        _resetBtn.interactable   = true;
    }

    private void OnDeal()    => _sm?.Apply(new DealOperation(2, 5));
    private void OnCut()     => _sm?.Apply(new CutOperation(26));
    private void OnShuffle() => _sm?.Apply(new OverhandOperation(new[] { 26, 26 }));

    private void OnCollect()
    {
        if (_sm == null) return;
        int[][] hands = _sm.CurrentHands;
        int[]   deck  = _sm.CurrentDeck;
        bool    hasDeck = deck != null && deck.Length > 0;

        var piles = new int[hands.Length + (hasDeck ? 1 : 0)][];
        for (int i = 0; i < hands.Length; i++) piles[i] = hands[i];
        if (hasDeck) piles[hands.Length] = deck;

        var order = new int[piles.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;

        _sm.Apply(new CollectOperation(piles, order));
    }

    private Button Btn(string label)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = 88f;
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
        txt.font      = CardVisuals.DefaultFont;
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
