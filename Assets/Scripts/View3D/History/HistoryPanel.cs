using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// The deck's memory, on screen (the History key, Tab by default, toggles it). Shows how well mixed the deck is, the recent actions,
/// and — after clicking any card — that card's journey through every logged shuffle, deal and play,
/// across sessions. "Replay last shuffle" re-enacts the most recent riffle, overhand or cut slowly.
public class HistoryPanel : MonoBehaviour, IStateManagerConsumer
{
    [SerializeField] int _recentActions = 10;
    [SerializeField] int _journeySteps  = 8;

    public GameStateManager StateManager { private get; set; }

    public bool IsOpen { get; private set; }

    private GameStateManager _sm;
    private TableCardView    _view;
    private GameObject       _panel;
    private Text             _body;
    private int              _tracked = -1;

    private void Start()
    {
        _sm = StateManager;
        var local = GetComponent<TableCardView>();
        _view = local != null ? local : FindAnyObjectByType<TableCardView>();
        if (_sm != null) _sm.StateChanged += OnStateChanged;
        Build();
    }

    private void OnDestroy()
    {
        if (_sm != null) _sm.StateChanged -= OnStateChanged;
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (_panel == null || _sm == null) return;
        IsOpen = true;
        _panel.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        _panel.SetActive(false);
        Track(-1);
    }

    private void OnStateChanged(GameStateSnapshot _)
    {
        if (IsOpen) Refresh();
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Controls.Pressed(ControlAction.Cancel))
        {
            Close();
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        var cam = Camera.main;
        if (cam == null) return;
        int card = TableCardView.CardAt(cam.ScreenPointToRay(mouse.position.ReadValue()));
        if (card >= 0) Track(card);
    }

    /// Follows one card (−1 stops); it is tinted on the table while the panel is open.
    public void Track(int card)
    {
        if (_view != null && _tracked >= 0) _view.SetTint(_tracked, null);
        _tracked = card;
        if (_view != null && _tracked >= 0) _view.SetTint(_tracked, TableCardView.TrackedTint);
        if (IsOpen) Refresh();
    }

    public void ReplayLastShuffle()
    {
        var history = _sm.History;
        int index = history.LastShuffleIndex();
        if (index < 0 || _view == null) return;
        _view.PlayReplay(history.StateBefore(index), history.Entries[index].Moves);
    }

    // ---- Content ----

    private void Refresh()
    {
        if (_body != null) _body.text = Describe(_sm, _tracked, _recentActions, _journeySteps);
    }

    /// The panel's text; separate from the UI so it can be tested.
    public static string Describe(GameStateManager sm, int tracked, int recentActions = 10, int journeySteps = 8)
    {
        var history = sm.History;
        var sb = new StringBuilder();

        sb.AppendLine("<b>HOW MIXED IS THE DECK?</b>");
        var packet = FullPacket(sm.Table);
        if (packet == null)
            sb.AppendLine("<i>Gather all 52 cards (deck or hand) to measure.</i>");
        else
        {
            sb.AppendLine($"Rising sequences: <b>{DeckMetrics.RisingSequences(packet)}</b>  (fresh deck 1 · random ≈ {DeckMetrics.RandomRisingSequences:0.#})");
            sb.AppendLine($"Neighbours still together: <b>{DeckMetrics.AdjacentPairs(packet)}</b>  (random ≈ {DeckMetrics.RandomAdjacentPairs:0})");
        }
        int riffles = history.CountSinceCollect(ActionKind.Riffle);
        sb.AppendLine($"This round: {riffles} riffle{(riffles == 1 ? "" : "s")} (≈{DeckMetrics.RifflesToMix} mix a deck) · " +
                      $"{history.CountSinceCollect(ActionKind.Overhand)} overhand · {history.CountSinceCollect(ActionKind.Cut)} cut");
        sb.AppendLine();

        sb.AppendLine("<b>FOLLOW A CARD</b>");
        if (tracked < 0)
            sb.AppendLine("<i>Click any card on the table.</i>");
        else
        {
            var journey = history.JourneyOf(tracked);
            sb.AppendLine($"<b>{CardNames.Of(tracked)}</b> — now {Where(sm.Table.LocationOf(tracked), sm.Table.IndexOf(tracked))}, " +
                          $"moved {journey.Count} time{(journey.Count == 1 ? "" : "s")}");
            for (int i = Mathf.Max(0, journey.Count - journeySteps); i < journey.Count; i++)
            {
                var step = journey[i];
                var entry = history.Entries[step.EntryIndex];
                string face = step.FaceUpAfter == step.FaceUpBefore ? "" : step.FaceUpAfter ? ", turned face up" : ", turned face down";
                sb.AppendLine($"  R{entry.Round} {entry.Name}: {Where(step.From, step.FromIndex)} → {Where(step.To, step.ToIndex)}{face}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("<b>RECENT ACTIONS</b>");
        if (history.Entries.Count == 0) sb.AppendLine("<i>Nothing yet.</i>");
        for (int i = history.Entries.Count - 1; i >= 0 && i >= history.Entries.Count - recentActions; i--)
            sb.AppendLine($"  R{history.Entries[i].Round}  {history.Entries[i].Name}");

        return sb.ToString();
    }

    private static string Where(ZoneId zone, int index) => $"{CardNames.Of(zone)} #{index + 1}";

    /// All 52 cards if they are together in the deck or the dealer's hand, else null.
    private static IReadOnlyList<int> FullPacket(CardTable table)
    {
        if (table.Count(ZoneId.Deck) == DeckEngine.DeckSize) return table.Cards(ZoneId.Deck);
        if (table.Count(ZoneId.RightHand) == DeckEngine.DeckSize) return table.Cards(ZoneId.RightHand);
        return null;
    }

    // ---- UI ----

    private void Build()
    {
        var canvasGo = new GameObject("History Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        _panel = new GameObject("History Panel", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)_panel.transform;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 0.5f);
        rt.offsetMin = new Vector2(-430f, 90f);
        rt.offsetMax = new Vector2(-12f, -66f);
        _panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 0.9f);

        var textGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(_panel.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(14f, 52f);
        trt.offsetMax = new Vector2(-14f, -12f);
        _body = textGo.GetComponent<Text>();
        _body.font = HudStyle.DefaultFont;
        _body.fontSize = 13;
        _body.color = new Color(0.92f, 0.9f, 0.85f);
        _body.supportRichText = true;
        _body.alignment = TextAnchor.UpperLeft;
        _body.horizontalOverflow = HorizontalWrapMode.Wrap;
        _body.verticalOverflow = VerticalWrapMode.Truncate;

        PanelButton("Replay last shuffle", new Vector2(14f, 10f), 200f, ReplayLastShuffle);
        PanelButton("Close (H)", new Vector2(222f, 10f), 120f, Close);
        _panel.SetActive(false);
    }

    private void PanelButton(string label, Vector2 position, float width, System.Action onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_panel.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(width, 32f);
        go.GetComponent<Image>().color = new Color(0.28f, 0.28f, 0.40f);
        go.GetComponent<Button>().onClick.AddListener(() => onClick());

        var textGo = new GameObject("T", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.font = HudStyle.DefaultFont;
        text.fontSize = 13;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = label;
    }
}
