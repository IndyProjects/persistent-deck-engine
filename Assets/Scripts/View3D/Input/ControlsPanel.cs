using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// The Controls screen: every keyboard action with its key. Click a key to rebind it, then press the new
/// key (Esc cancels). Taking a key another action needs at the same time swaps the two. Opens from the
/// main menu's Settings and in game with the Cancel key; builds its own overlay canvas.
public class ControlsPanel : MonoBehaviour
{
    public bool IsOpen => _root != null && _root.activeSelf;

    /// The panel's own overlay canvas (null until first opened).
    public Canvas Canvas { get; private set; }

    /// True while any Controls screen is open: game input should ignore keys (they may be rebinding).
    public static bool AnyOpen { get; private set; }

    private GameObject _root;
    private Text       _status;
    private Action     _onClosed;
    private ControlAction? _listening;
    private int        _openedFrame = -1;
    private readonly Dictionary<ControlAction, Text> _keyLabels = new Dictionary<ControlAction, Text>();

    private static readonly Color Ink   = new Color(0.94f, 0.92f, 0.86f);
    private static readonly Color Muted = new Color(0.7f, 0.68f, 0.62f);

    /// Shows the panel; <paramref name="onClosed"/> runs when the player closes it.
    public void Open(Action onClosed = null)
    {
        if (_root == null) Build();
        _onClosed = onClosed;
        _listening = null;
        _status.text = "Click a key to change it.";
        _openedFrame = Time.frameCount; // the key that opened the panel mustn't also close it
        Refresh();
        _root.SetActive(true);
        AnyOpen = true;
    }

    public void Close()
    {
        if (!IsOpen) return;
        _listening = null;
        _root.SetActive(false);
        AnyOpen = false;
        var callback = _onClosed;
        _onClosed = null;
        callback?.Invoke();
    }

    private void OnDestroy()
    {
        if (IsOpen) AnyOpen = false;
    }

    private void Update()
    {
        if (!IsOpen || Time.frameCount == _openedFrame) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (_listening == null)
        {
            if (kb.escapeKey.wasPressedThisFrame) Close();
            return;
        }

        var action = _listening.Value;
        if (kb.escapeKey.wasPressedThisFrame && action != ControlAction.Cancel)
        {
            _listening = null;
            _status.text = "Unchanged.";
            Refresh();
            return;
        }

        foreach (var control in kb.allKeys)
        {
            if (control == null || !control.wasPressedThisFrame) continue;
            var key = control.keyCode;
            if (Controls.IsReserved(key))
            {
                _status.text = "The number keys choose the packet size — pick another key.";
                return;
            }
            var swapped = Controls.Bind(action, key);
            _listening = null;
            _status.text = swapped.HasValue
                ? $"{Controls.Label(action)}: {Controls.KeyName(key)}  (swapped with {Controls.Label(swapped.Value)})"
                : $"{Controls.Label(action)}: {Controls.KeyName(key)}";
            Refresh();
            return;
        }
    }

    private void Listen(ControlAction action)
    {
        // Deselect the clicked button so Space/Enter as the new key doesn't re-press it
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        _listening = action;
        _status.text = $"Press a key for “{Controls.Label(action)}”" +
                       (action == ControlAction.Cancel ? "" : " · Esc to keep the current one");
        Refresh();
    }

    private void Refresh()
    {
        foreach (var pair in _keyLabels)
            pair.Value.text = _listening == pair.Key ? "…" : Controls.Name(pair.Key);
    }

    // ---- UI ----

    private void Build()
    {
        var canvasGo = new GameObject("Controls Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = Canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        _root = canvasGo;

        // Dim everything behind
        var dim = Child("Dim", canvasGo.transform, typeof(Image));
        Stretch(dim);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        var panel = Child("Panel", canvasGo.transform, typeof(Image), typeof(VerticalLayoutGroup));
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(620f, 700f);
        panel.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 1f);
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(26, 26, 18, 18);
        layout.spacing = 2f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Line(panel.transform, "CONTROLS", 24, FontStyle.Bold, Ink, 34f);
        Section(panel.transform, "At the table");
        for (int i = 0; i < Controls.Count; i++)
        {
            var action = (ControlAction)i;
            if (action == ControlAction.RiffleLeft) Section(panel.transform, "While shuffling");
            if (action == ControlAction.PlayCards)  Section(panel.transform, "Playing a game");
            Row(panel.transform, action);
        }
        Line(panel.transform, "Mouse: left drags cards and drops from the left hand · right drops from the right hand.\n" +
                              "Number keys 1–5 or the scroll wheel set the packet size.", 12, FontStyle.Italic, Muted, 34f);
        _status = Line(panel.transform, "", 13, FontStyle.Normal, new Color(1f, 0.8f, 0.5f), 22f);

        var buttons = Child("Buttons", panel.transform, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        buttons.GetComponent<LayoutElement>().preferredHeight = 36f;
        var hl = buttons.GetComponent<HorizontalLayoutGroup>();
        hl.spacing = 12f;
        hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = true;
        Button(buttons.transform, "Reset to defaults", () =>
        {
            Controls.ResetToDefaults();
            _listening = null;
            _status.text = "Controls reset to the defaults.";
            Refresh();
        });
        Button(buttons.transform, "Back", Close);

        _root.SetActive(false);
    }

    private void Section(Transform parent, string title)
        => Line(parent, title.ToUpperInvariant(), 12, FontStyle.Bold, Muted, 20f);

    private void Row(Transform parent, ControlAction action)
    {
        var row = Child(action.ToString(), parent, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.GetComponent<LayoutElement>().preferredHeight = 22f;
        var hl = row.GetComponent<HorizontalLayoutGroup>();
        hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.spacing = 10f;

        var label = Line(row.transform, Controls.Label(action), 13, FontStyle.Normal, Ink, 22f);
        label.alignment = TextAnchor.MiddleLeft;
        label.GetComponent<LayoutElement>().flexibleWidth = 1f;

        var button = Button(row.transform, Controls.Name(action), () => Listen(action));
        button.GetComponent<LayoutElement>().preferredWidth = 150f;
        _keyLabels[action] = button.GetComponentInChildren<Text>();
    }

    private static GameObject Child(string name, Transform parent, params Type[] components)
    {
        var all = new Type[components.Length + 1];
        all[0] = typeof(RectTransform);
        components.CopyTo(all, 1);
        var go = new GameObject(name, all);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(GameObject go)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Text Line(Transform parent, string text, int size, FontStyle style, Color color, float height)
    {
        var go = Child("Text", parent, typeof(Text), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().preferredHeight = height;
        var t = go.GetComponent<Text>();
        t.font = HudStyle.DefaultFont;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleLeft;
        t.raycastTarget = false;
        return t;
    }

    private static Button Button(Transform parent, string label, Action onClick)
    {
        var go = Child(label, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
        go.GetComponent<Image>().color = Color.white;
        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor      = new Color(0.16f, 0.16f, 0.21f);
        colors.highlightedColor = new Color(0.30f, 0.24f, 0.16f);
        colors.pressedColor     = new Color(0.45f, 0.34f, 0.18f);
        colors.selectedColor    = colors.normalColor;
        button.colors = colors;
        button.onClick.AddListener(() => onClick());

        var text = Line(go.transform, label, 15, FontStyle.Bold, Ink, 28f);
        text.alignment = TextAnchor.MiddleCenter;
        Stretch(text.gameObject);
        return button;
    }
}
