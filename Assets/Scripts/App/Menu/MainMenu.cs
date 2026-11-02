using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Title screen: "JOHOTI" over a slow, circling view of the themed table showing the player's saved
/// deck. Play opens the game picker (Johoti, Free Play, Blackjack…); Settings holds theme, display,
/// quality and the Controls screen for rebinding keys. Built in code like the HUD; the animation is deliberately light —
/// a fade in from black, the title rising into place, buttons following, and a gentle camera drift.
public class MainMenu : MonoBehaviour
{
    public const string Title = "JOHOTI";

    [Header("Backdrop camera")]
    [SerializeField] float _orbitSpeed  = 3.5f;  // degrees per second
    [SerializeField] float _orbitRadius = 2.1f;
    [SerializeField] float _orbitHeight = 1.75f;

    [Header("Intro timing (seconds)")]
    [SerializeField] float _fadeIn     = 0.9f;
    [SerializeField] float _titleDelay = 0.3f;
    [SerializeField] float _titleRise  = 1.1f;
    [SerializeField] float _buttonsAt  = 1.1f;

    public Canvas Canvas { get; private set; }

    private Camera        _camera;
    private float         _angle = 20f;
    private float         _clock;
    private bool          _leaving, _loading;
    private enum Screen { Main, Games, Settings }
    private Screen        _screen = Screen.Main;

    private RectTransform _title;
    private CanvasGroup   _titleGroup, _mainGroup, _gamesGroup, _settingsGroup, _fade;
    private Text          _themeValue, _displayValue, _qualityValue;

    private static readonly Color Ink    = new Color(0.96f, 0.93f, 0.86f);
    private static readonly Color Button = new Color(0.12f, 0.12f, 0.15f, 0.82f);

    private void Awake() => Build(Camera.main);

    /// Builds the backdrop and the menu UI. Safe to call outside play mode (preview capture).
    public void Build(Camera cam)
    {
        if (Canvas != null) return;
        _camera = cam;
        BuildBackdrop();
        BuildUI();
        RefreshSettings();
    }

    /// Opens or closes the settings panel.
    public void ShowSettings(bool show) => _screen = show ? Screen.Settings : Screen.Main;

    /// Opens or closes the game picker.
    public void ShowGames(bool show) => _screen = show ? Screen.Games : Screen.Main;

    /// Skips the intro animation (previews).
    public void ShowImmediately()
    {
        _clock = 100f;
        Animate(0f);
    }

    // ---- Backdrop: the themed table with the saved deck ----

    private void BuildBackdrop()
    {
        var env = new GameObject("Table Environment").AddComponent<TableEnvironment>();
        if (env.Theme == null) env.Build(_camera, ThemeService.Current); // play mode builds in Awake
        env.ShowLocalFigure = true; // from out here, every seat is someone else's
        _camera = env.Camera;

        // The menu flies the camera itself
        var rig = _camera.GetComponent<SeatedCameraRig>();
        if (rig != null) rig.enabled = false;

        var sm = new GameStateManager();
        TableSaveStore.TryLoad(sm);
        var cards = new GameObject("Cards").AddComponent<TableCardView>();
        cards.StateManager = sm;
        if (!Application.isPlaying) cards.Initialise(sm);

        PlaceCamera();
    }

    private void PlaceCamera()
    {
        float a = _angle * Mathf.Deg2Rad;
        var centre = new Vector3(0f, TableLayout.SurfaceY, 0f);
        var eye = centre + new Vector3(Mathf.Sin(a) * _orbitRadius, _orbitHeight - TableLayout.SurfaceY, -Mathf.Cos(a) * _orbitRadius);
        _camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(centre + Vector3.up * 0.1f - eye, Vector3.up));
    }

    // ---- UI ----

    private void BuildUI()
    {
        var canvasGo = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas = canvasGo.GetComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight  = 0.5f;
        var root = canvasGo.transform;

        // Darken the backdrop so the title reads, a little more behind the buttons
        Panel("Shade", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.4f));
        Panel("Band", root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -200f), new Vector2(0f, 40f),
              new Color(0f, 0f, 0f, 0.35f));

        // Title
        _titleGroup = Group("Title", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(900f, 170f), out _title);
        var title = Label(_title, Title, 132, FontStyle.Bold, Ink);
        var shadow = title.gameObject.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(4f, -5f);

        // Main buttons
        _mainGroup = Group("Main", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(300f, 140f), out var main);
        Stack(main);
        MenuButton(main, "Play", () => _screen = Screen.Games);
        MenuButton(main, "Settings", () => _screen = Screen.Settings);

        // Game picker
        _gamesGroup = Group("Games", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -125f), new Vector2(560f, 372f), out var games);
        games.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 0.88f);
        Stack(games, 10, new RectOffset(24, 24, 16, 18));
        Sized(Label(games, "CHOOSE A GAME", 18, FontStyle.Bold, Ink), 0f).GetComponent<LayoutElement>().preferredHeight = 26f;
        foreach (var mode in GameModes.All) GameButton(games, mode);
        MenuButton(games, "Back", () => _screen = Screen.Main);
        _gamesGroup.alpha = 0f;
        _gamesGroup.blocksRaycasts = false;

        // Settings panel
        _settingsGroup = Group("Settings", root, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(520f, 300f), out var settings);
        settings.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 0.85f);
        Stack(settings, 10, new RectOffset(24, 24, 20, 20));
        _themeValue   = SettingRow(settings, "Theme",   () => ThemeService.Cycle(-1), () => ThemeService.Cycle(1));
        _displayValue = SettingRow(settings, "Display", () => GameSettings.Fullscreen = !GameSettings.Fullscreen,
                                                        () => GameSettings.Fullscreen = !GameSettings.Fullscreen);
        _qualityValue = SettingRow(settings, "Quality", () => GameSettings.Quality--, () => GameSettings.Quality++);
        MenuButton(settings, "Controls", OpenControls);
        MenuButton(settings, "Back", () => _screen = Screen.Main);
        _settingsGroup.alpha = 0f;
        _settingsGroup.blocksRaycasts = false;

        // Fade to/from black over everything
        _fade = Group("Fade", root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, out var fade);
        fade.anchorMin = Vector2.zero;
        fade.anchorMax = Vector2.one;
        fade.gameObject.AddComponent<Image>().color = Color.black;
        _fade.blocksRaycasts = false;

        Animate(0f);
    }

    private Text SettingRow(Transform parent, string name, System.Action previous, System.Action next)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 40f;
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth  = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth  = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleCenter;

        Sized(Label(row.transform, name, 20, FontStyle.Normal, Ink, TextAnchor.MiddleLeft), 130f);
        SmallButton(row.transform, "<", () => { previous(); RefreshSettings(); });
        var value = Sized(Label(row.transform, "", 20, FontStyle.Bold, Ink), 190f);
        SmallButton(row.transform, ">", () => { next(); RefreshSettings(); });
        return value;
    }

    private ControlsPanel _controls;

    /// Hides Settings while the Controls screen is up, and brings it back when that closes.
    private void OpenControls()
    {
        if (_controls == null) _controls = gameObject.AddComponent<ControlsPanel>();
        _settingsGroup.alpha = 0f;
        _settingsGroup.interactable = _settingsGroup.blocksRaycasts = false;
        _inControls = true;
        _controls.Open(() => _inControls = false);
    }

    private bool _inControls;

    private void RefreshSettings()
    {
        _themeValue.text   = ThemeService.Current.DisplayName;
        _displayValue.text = GameSettings.Fullscreen ? "Fullscreen" : "Windowed";
        _qualityValue.text = GameSettings.QualityName;
    }

    private void GameButton(Transform parent, GameMode mode)
    {
        string label = mode.Available
            ? $"<b>{mode.DisplayName}</b>\n<size=14>{mode.Description}</size>"
            : $"<b>{mode.DisplayName}</b>\n<size=14>Coming soon</size>";
        var button = MakeButton(parent, label, () => Play(mode), 0f, 66f, 22);
        button.interactable = mode.Available;
        if (!mode.Available) button.GetComponentInChildren<Text>().color = new Color(0.55f, 0.53f, 0.5f);
    }

    private void Play(GameMode mode)
    {
        if (_leaving || !GameModes.Select(mode)) return;
        _leaving = true;
        _gamesGroup.interactable = false;
    }

    // ---- Animation ----

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _clock += dt;

        _angle += _orbitSpeed * dt;
        PlaceCamera();
        Animate(dt);

        if (_leaving && !_loading && _fade.alpha >= 0.99f)
        {
            _loading = true;
            SceneManager.LoadScene(SceneNames.Table);
        }
    }

    private void Animate(float dt)
    {
        float t = _clock;

        // Fade in from black at the start, back to black when leaving
        _fade.alpha = _leaving ? Mathf.MoveTowards(_fade.alpha, 1f, dt / 0.45f) : 1f - Smooth(t / _fadeIn);

        // Title rises into place, then breathes very slightly
        float rise = Smooth((t - _titleDelay) / _titleRise);
        _titleGroup.alpha = rise;
        _title.anchoredPosition = new Vector2(0f, 150f - 40f * (1f - rise));
        _title.localScale = Vector3.one * (1f + 0.012f * Mathf.Sin(t * 1.1f) * rise);

        // Buttons follow; settings cross-fade with the main buttons
        float buttons = Smooth((t - _buttonsAt) / 0.6f);
        float k = dt > 0f ? 1f - Mathf.Exp(-12f * dt) : 1f;
        float mainTarget = _screen == Screen.Main ? buttons : 0f;
        _mainGroup.alpha     = dt > 0f ? Mathf.Lerp(_mainGroup.alpha, mainTarget, k) : mainTarget;
        bool gamesVisible    = _screen == Screen.Games;
        bool settingsVisible = _screen == Screen.Settings && !_inControls;
        _gamesGroup.alpha    = Mathf.Lerp(_gamesGroup.alpha, gamesVisible ? 1f : 0f, k);
        _settingsGroup.alpha = Mathf.Lerp(_settingsGroup.alpha, settingsVisible ? 1f : 0f, k);
        _mainGroup.interactable = _mainGroup.blocksRaycasts = _screen == Screen.Main && buttons > 0.9f && !_leaving;
        _gamesGroup.interactable = _gamesGroup.blocksRaycasts = gamesVisible && !_leaving;
        _settingsGroup.interactable = _settingsGroup.blocksRaycasts = settingsVisible;
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // ---- UI helpers ----

    private static CanvasGroup Group(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, out RectTransform rect)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go.GetComponent<CanvasGroup>();
    }

    private static void Panel(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private static void Stack(Transform parent, float spacing = 14f, RectOffset padding = null)
    {
        var layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth  = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;
        if (padding != null) layout.padding = padding;
    }

    private static Text Label(Transform parent, string text, int size, FontStyle style, Color color,
                              TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.GetComponent<Text>();
        t.font = HudStyle.DefaultFont;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    private static Text Sized(Text t, float width)
    {
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        return t;
    }

    private static void MenuButton(Transform parent, string label, System.Action onClick)
        => MakeButton(parent, label, onClick, 0f, 56f, 24);

    private static void SmallButton(Transform parent, string label, System.Action onClick)
        => MakeButton(parent, label, onClick, 40f, 36f, 20);

    private static Button MakeButton(Transform parent, string label, System.Action onClick, float width, float height, int fontSize)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(MenuButtonFx));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = height;
        if (width > 0f) le.preferredWidth = width;

        go.GetComponent<Image>().color = Color.white;
        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor      = Button;
        colors.highlightedColor = new Color(0.30f, 0.24f, 0.16f, 0.92f);
        colors.pressedColor     = new Color(0.45f, 0.34f, 0.18f, 1f);
        colors.selectedColor    = Button;
        colors.disabledColor    = new Color(0.08f, 0.08f, 0.10f, 0.6f);
        colors.fadeDuration     = 0.12f;
        button.colors = colors;
        button.onClick.AddListener(() => onClick());

        Label(go.transform, label, fontSize, FontStyle.Normal, Ink).supportRichText = true;
        return button;
    }
}

/// Grows a button slightly while the pointer is over it.
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private float _target = 1f;

    public void OnPointerEnter(PointerEventData _) => _target = 1.05f;
    public void OnPointerExit(PointerEventData _)  => _target = 1f;

    private void Update()
    {
        float s = Mathf.Lerp(transform.localScale.x, _target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(s, s, 1f);
    }
}
