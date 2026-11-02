using UnityEngine;
using UnityEngine.UI;

/// HUD bar drawn over the 3D table: round, what the rules expect next, the last rejection, and the
/// action buttons.
public class GameStateView : MonoBehaviour, IStateManagerConsumer
{
    /// Injected by GameStateManagerBridge before Start.
    public GameStateManager StateManager { get; set; }

    /// Hooks into interactions, panels and navigation. Set by GameStateManagerBridge before Start.
    public HudHandlers Handlers { get; set; }

    /// Which game is running, and an optional note shown under the status (set before Start).
    public string GameName { get; set; }
    public string GameNote { get; set; }

    private GameStateManager _sm;

    private Text         _statusLabel;
    private string       _lastRejection;
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

        _actionPanel.Initialise(_sm, Handlers);
        _sm.StateChanged      += OnStateChanged;
        _sm.OperationRejected += OnRejected;

        OnStateChanged(_sm.TakeSnapshot());
    }

    private void OnDestroy()
    {
        if (_sm == null) return;
        _sm.StateChanged      -= OnStateChanged;
        _sm.OperationRejected -= OnRejected;
    }

    private void OnStateChanged(GameStateSnapshot s)
    {
        _lastRejection = null;
        RefreshStatus(s.RoundNumber);
        _actionPanel.RefreshButtons(s);
    }

    private void OnRejected(GameOperation op, string reason)
    {
        _lastRejection = reason;
        RefreshStatus(_sm.RoundNumber);
    }

    private void RefreshStatus(int round)
    {
        string game = string.IsNullOrEmpty(GameName) ? "" : $"<b>{GameName}</b>  ·  ";
        string status = $"{game}Round {round}  ·  {_sm.Rules.Describe(_sm.Table)}";
        string second = _lastRejection != null ? $"<color=#ffb070>{_lastRejection}</color>"
                      : !string.IsNullOrEmpty(GameNote) ? $"<color=#b8b2a6><i>{GameNote}</i></color>"
                      : null;
        _statusLabel.text = second == null ? status : $"{status}\n{second}";
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

        var slGO = Rect("Status", parent,
            new Vector2(0, 0), new Vector2(0.32f, 1),
            new Vector2(12f, 0f), Vector2.zero);
        _statusLabel = slGO.AddComponent<Text>();
        StyleLabel(_statusLabel, 13, TextAnchor.MiddleLeft);
        _statusLabel.supportRichText = true;

        var apGO = Rect("Actions", parent,
            new Vector2(0.32f, 0), Vector2.one,
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
