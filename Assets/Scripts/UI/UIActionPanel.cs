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

    /// Enables each button only if its operation would currently succeed (rules + physical dry run).
    public void RefreshButtons(GameStateSnapshot s)
    {
        if (_sm == null) return;
        _dealBtn.interactable    = _sm.CanApply(DealOp()).Success;
        _cutBtn.interactable     = _sm.CanApply(CutOp()).Success;
        _shuffleBtn.interactable = _sm.CanApply(ShuffleOp()).Success;
        _collectBtn.interactable = _sm.CanApply(CollectOperation.All()).Success;
        _undoBtn.interactable    = _sm.CanUndo;
        _resetBtn.interactable   = true;
    }

    private int DeckCount => _sm.Table.Count(ZoneId.Deck);

    // Cut and shuffle act on whatever is in the deck, split at the middle
    private GameOperation DealOp()    => new DealOperation(2, 5);
    private GameOperation CutOp()     => new CutOperation(DeckCount / 2);
    private GameOperation ShuffleOp() => DeckCount < 2
        ? new OverhandOperation(new[] { DeckCount })
        : new OverhandOperation(new[] { DeckCount / 2, DeckCount - DeckCount / 2 });

    private void OnDeal()    => _sm?.Apply(DealOp());
    private void OnCut()     => _sm?.Apply(CutOp());
    private void OnShuffle() => _sm?.Apply(ShuffleOp());
    private void OnCollect() => _sm?.Apply(CollectOperation.All());

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
