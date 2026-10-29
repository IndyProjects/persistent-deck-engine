using UnityEngine;
using UnityEngine.UI;

/// HUD bar drawn over the 3D table: phase, round and the action buttons.
public class GameStateView : MonoBehaviour, IStateManagerConsumer
{
    /// Injected by GameStateManagerBridge before Start.
    public GameStateManager StateManager { get; set; }

    private GameStateManager _sm;

    private Text         _phaseLabel;
    private Text         _roundLabel;
    private UIActionPanel _actionPanel;

    private void Start()
    {
        BuildCanvas();

        _sm = StateManager;
        if (_sm == null)
        {
            Debug.LogError("[GameStateView] StateManager not set. Add GameStateManagerBridge to the scene.");
            return;
        }

        _actionPanel.Initialise(_sm);
        _sm.StateChanged += OnStateChanged;

        OnStateChanged(_sm.TakeSnapshot());
    }

    private void OnDestroy()
    {
        if (_sm != null) _sm.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameStateSnapshot s)
    {
        _phaseLabel.text = $"Phase:  {s.Phase}";
        _roundLabel.text = $"Round:  {s.RoundNumber}";
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

        const float hudH = 54f;
        var hudGO = Img("HUD", canvasGO.transform,
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -hudH), Vector2.zero,
            HudStyle.PanelColor);

        BuildHUD(hudGO.transform, hudH);
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
        img.sprite = HudStyle.SharedSprite;
        img.color  = color;
        return go;
    }

    private static void StyleLabel(Text t, int size, TextAnchor anchor)
    {
        t.font      = HudStyle.DefaultFont;
        t.fontSize  = size;
        t.alignment = anchor;
        t.color     = Color.white;
    }
}
