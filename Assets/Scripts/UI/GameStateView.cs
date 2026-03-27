using UnityEngine;
using UnityEngine.UI;

public class GameStateView : MonoBehaviour
{
    private GameStateManager _sm;

    private Text         _phaseLabel;
    private Text         _roundLabel;
    private DeckView     _deckView;
    private HandsPanel   _handsPanel;
    private UIActionPanel _actionPanel;

    private void Awake()
    {
        BuildCanvas();
    }

    private void Start()
    {
        var bridge = GetComponent<GameStateManagerBridge>()
                  ?? FindObjectOfType<GameStateManagerBridge>();

        if (bridge == null)
        {
            Debug.LogError("[GameStateView] GameStateManagerBridge not found.");
            return;
        }

        _sm = bridge.StateManager;
        _actionPanel.Initialise(_sm);
        _sm.StateChanged += OnStateChanged;

        OnStateChanged(new GameStateSnapshot(_sm.Phase, _sm.CurrentDeck, _sm.CurrentHands, _sm.RoundNumber));
    }

    private void OnDestroy()
    {
        if (_sm != null) _sm.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameStateSnapshot s)
    {
        _phaseLabel.text = $"Phase:  {s.Phase}";
        _roundLabel.text = $"Round:  {s.RoundNumber}";
        _deckView.Refresh(s.Deck != null ? s.Deck.Length : 0);
        _handsPanel.Refresh(s.Hands ?? System.Array.Empty<int[]>());
        _actionPanel.RefreshButtons(s);
    }


    private void BuildCanvas()
    {
        var canvasGO = new GameObject("Canvas",
            typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode   = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Img("BG", canvasGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            CardVisuals.BoardColor);

        const float hudH = 54f;
        var hudGO = Img("HUD", canvasGO.transform,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -hudH), Vector2.zero,
            CardVisuals.PanelColor);

        BuildHUD(hudGO.transform, hudH);

        var deckAreaGO = Rect("DeckArea", canvasGO.transform,
            new Vector2(0, 0), new Vector2(0.28f, 1),
            Vector2.zero, new Vector2(0, -hudH));

        var deckGO = Rect("DeckView", deckAreaGO.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-45f, -60f), new Vector2(45f, 60f));
        _deckView = deckGO.AddComponent<DeckView>();

        var handsAreaGO = Rect("HandsArea", canvasGO.transform,
            new Vector2(0.28f, 0), Vector2.one,
            Vector2.zero, new Vector2(0, -hudH));

        var hpGO = Rect("HandsPanel", handsAreaGO.transform,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -500f), Vector2.zero);
        _handsPanel = hpGO.AddComponent<HandsPanel>();
    }

    private void BuildHUD(Transform parent, float hudH)
    {

        var plGO = Rect("PhaseLabel", parent,
            new Vector2(0, 0), new Vector2(0.2f, 1),
            Vector2.zero, Vector2.zero);
        _phaseLabel = plGO.AddComponent<Text>();
        StyleLabel(_phaseLabel, 14, TextAnchor.MiddleCenter);

        var rlGO = Rect("RoundLabel", parent,
            new Vector2(0.2f, 0), new Vector2(0.36f, 1),
            Vector2.zero, Vector2.zero);
        _roundLabel = rlGO.AddComponent<Text>();
        StyleLabel(_roundLabel, 14, TextAnchor.MiddleCenter);

        var apGO = Rect("Actions", parent,
            new Vector2(0.36f, 0), Vector2.one,
            Vector2.zero, Vector2.zero);
        _actionPanel = apGO.AddComponent<UIActionPanel>();
    }


    private static GameObject Rect(string name, Transform parent,
        Vector2 ancMin, Vector2 ancMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = ancMin;
        rt.anchorMax = ancMax;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        return go;
    }

    private static GameObject Img(string name, Transform parent,
        Vector2 ancMin, Vector2 ancMax, Vector2 offMin, Vector2 offMax, Color color)
    {
        var go = Rect(name, parent, ancMin, ancMax, offMin, offMax);
        var img = go.AddComponent<Image>();
        img.sprite = CardVisuals.SharedSprite;
        img.color  = color;
        return go;
    }

    private static void StyleLabel(Text t, int size, TextAnchor anchor)
    {
        t.font      = CardVisuals.DefaultFont;
        t.fontSize  = size;
        t.alignment = anchor;
        t.color     = Color.white;
    }
}
