using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Renders the 3D card room to a PNG without entering play mode: builds the room in a temporary
/// scene, sets up a table state, snaps the cards into place and renders one or more camera views.
///
/// Menu: Tools → Persistent Deck → Capture Table Preview (writes Logs/TablePreview_seat.png).
/// Batch: Unity -batchmode -projectPath . -executeMethod TablePreviewCapture.CaptureBatch
///        -captureOut out.png [-captureScenario showcase|fresh|shuffle|held|dealing|blackjack] [-captureViews seat,top,side,cut]
///        [-captureTheme classic|midnight|…]
public static class TablePreviewCapture
{
    private const int Width = 1600, Height = 900;

    [MenuItem("Tools/Persistent Deck/Capture Table Preview")]
    private static void CaptureFromMenu()
    {
        string path = Path.GetFullPath("Logs/TablePreview.png");
        var written = Capture(path, "showcase", new[] { "seat" }, ThemeService.Current.Id);
        if (written.Length > 0) EditorUtility.RevealInFinder(written[0]);
    }

    /// Renders the main menu (title, buttons, backdrop) with the intro finished.
    /// Batch: -executeMethod TablePreviewCapture.CaptureMenuBatch -captureOut menu.png [-captureTheme id]
    public static void CaptureMenuBatch()
    {
        int exitCode = 0;
        try
        {
            string path = Arg("-captureOut") ?? Path.GetFullPath("Logs/MenuPreview.png");
            string themeId = Arg("-captureTheme");
            if (themeId != null && !ThemeService.Select(themeId, remember: false))
                throw new ArgumentException($"Unknown theme '{themeId}'.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            menu.Build(cam);
            menu.ShowSettings(Arg("-capturePanel") == "settings");
            if (Arg("-capturePanel") == "games") menu.ShowGames(true);
            menu.ShowImmediately();

            // Overlay canvases don't render into a camera's target; draw them through the camera instead
            var canvases = new System.Collections.Generic.List<Canvas> { menu.Canvas };
            if (Arg("-capturePanel") == "controls")
            {
                var controls = menu.gameObject.AddComponent<ControlsPanel>();
                controls.Open();
                canvases.Add(controls.Canvas);
            }
            for (int c = 0; c < canvases.Count; c++)
            {
                canvases[c].renderMode    = RenderMode.ScreenSpaceCamera;
                canvases[c].worldCamera   = cam;
                canvases[c].planeDistance = cam.nearClipPlane + 0.01f - c * 0.002f; // later panels in front
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvases[c].transform);
            }
            // Outside play mode uGUI never runs its layout pass on its own
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)menu.Canvas.transform);
            Canvas.ForceUpdateCanvases();

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            Render(cam, path);
            Debug.Log($"[TablePreviewCapture] Wrote {path}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            exitCode = 1;
        }
        EditorApplication.Exit(exitCode);
    }

    public static void CaptureBatch()
    {
        int exitCode = 0;
        try
        {
            string path     = Arg("-captureOut") ?? Path.GetFullPath("Logs/TablePreview.png");
            string scenario = Arg("-captureScenario") ?? "showcase";
            string[] views  = (Arg("-captureViews") ?? "seat").Split(',');
            string theme    = Arg("-captureTheme") ?? ThemeService.Current.Id;
            foreach (var file in Capture(path, scenario, views, theme))
                Debug.Log($"[TablePreviewCapture] Wrote {file}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            exitCode = 1;
        }
        EditorApplication.Exit(exitCode);
    }

    public static string[] Capture(string path, string scenario, string[] views, string themeId)
    {
        var theme = ThemeService.Find(themeId)
            ?? throw new ArgumentException($"Unknown theme '{themeId}'. Available: {string.Join(", ", ThemeIds())}");

        var previous = SceneManager.GetActiveScene();
        // An unsaved untitled scene can't have another opened alongside it, so replace it instead
        bool additive = !string.IsNullOrEmpty(previous.path);
        if (!additive && !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return Array.Empty<string>();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
        SceneManager.SetActiveScene(scene); // so fog/ambient settings apply to this scene
        var written = new string[views.Length];
        try
        {
            var cam = new GameObject("Preview Camera", typeof(Camera)).GetComponent<Camera>();
            new GameObject("Table Environment").AddComponent<TableEnvironment>().Build(cam, theme);

            var sm = new GameStateManager();
            RunScenario(sm, scenario);
            var cards = new GameObject("Cards").AddComponent<TableCardView>();
            cards.Initialise(sm);
            cards.ApplyTheme(theme);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i].Trim() == "cut") PreviewCut(sm, cards, cam);
                else if (views[i].Trim() == "riffle") PreviewRiffle(sm, cards, cam);
                else PlaceCamera(cam, views[i].Trim());
                written[i] = Path.ChangeExtension(path, null) + $"_{theme.Id}_{views[i].Trim()}.png";
                Render(cam, written[i]);
            }
        }
        finally
        {
            if (additive)
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        return written;
    }

    private static void RunScenario(GameStateManager sm, string scenario)
    {
        switch (scenario)
        {
            case "fresh":
                break;

            case "shuffle": // mid-riffle: the deck split between the dealer's hands
                sm.Apply(new MovePacketOperation(CardMove.TopToTop(ZoneId.Deck, 26, ZoneId.LeftHand)));
                sm.Apply(new MovePacketOperation(CardMove.TopToTop(ZoneId.Deck, 26, ZoneId.RightHand)));
                break;

            case "held": // dealer holding the whole deck, ready to shuffle or cut
                sm.Apply(new PickUpDeckOperation());
                break;

            case "blackjack": // a Blackjack game a dozen moves in: hands, discard pile, draw pile
            {
                var rules = new BlackjackRules(4);
                sm.Rules = rules;
                var rng = new System.Random(7);
                sm.Apply(new PickUpDeckOperation());
                sm.Apply(new RiffleOperation(26, new[] { 1, 2 }, ZoneId.RightHand));
                rules.Dealing.PacketSize = 7;
                while (rules.Dealing.PhaseOf(sm.Table) != RoundPhase.Dealt) sm.Apply(rules.Dealing.NextPacket(sm.Table));
                sm.Apply(new PutDownDeckOperation());
                sm.Apply(new StartDiscardOperation());
                for (int i = 0; i < 12 && rules.Winner < 0; i++)
                {
                    var op = BlackjackAI.ChooseMove(rules, sm.Table, rules.Turn, rng, out bool call);
                    if (call) sm.Apply(new AnnounceLastCardOperation(rules.Turn));
                    sm.Apply(op);
                }
                break;
            }

            case "dealing": // dealer holding the stub, two-at-a-time deal in progress
            {
                var rules = new DealerRules();
                sm.Rules = rules;
                sm.Apply(new PickUpDeckOperation());
                sm.Apply(new RiffleOperation(26, new[] { 2, 1 }, ZoneId.RightHand));
                for (int i = 0; i < 5; i++) sm.Apply(rules.NextPacket(2));
                break;
            }

            default: // showcase: shuffled, dealt to four, community cards out, a couple of discards
                sm.Apply(new RiffleOperation(26, new[] { 1 }));
                sm.Apply(new CutOperation(20));
                sm.Apply(new DealOperation(4, 5));
                for (int i = 0; i < 3; i++)
                    sm.Apply(new MoveCardOperation(sm.CurrentDeck[0], ZoneId.Pile(i + 1), flip: true));
                sm.Apply(new MoveCardOperation(sm.CurrentHands[2][0], ZoneId.Discard(), flip: true));
                sm.Apply(new MoveCardOperation(sm.CurrentHands[2][0], ZoneId.Discard(), flip: true));
                break;
        }
    }

    /// The hands-on riffle mid-way: split 26/26, twenty cards dropped alternately onto the table.
    private static void PreviewRiffle(GameStateManager sm, TableCardView cards, Camera cam)
    {
        var riffle = cards.GetComponent<RiffleInteraction>();
        if (riffle == null) riffle = cards.gameObject.AddComponent<RiffleInteraction>(); // not ??: Unity fake-null
        var rig = cam.GetComponent<SeatedCameraRig>();
        if (!riffle.Begin(sm, cards, rig))
            throw new InvalidOperationException($"Riffle preview needs the deck in hand: {riffle.LastMessage}");
        riffle.PreviewDrops(26, 20);
        cards.SnapAll();
        rig.ApplyImmediate();
    }

    /// The interactive cut's close-up: deck spread apart, lower 40% selected to go on top.
    private static void PreviewCut(GameStateManager sm, TableCardView cards, Camera cam)
    {
        var cut = cards.GetComponent<CutInteraction>();
        if (cut == null) cut = cards.gameObject.AddComponent<CutInteraction>(); // not ??: Unity fake-null
        var rig = cam.GetComponent<SeatedCameraRig>();
        if (!cut.IsActive && !cut.Begin(sm, cards, rig))
            throw new InvalidOperationException("Cut preview needs a deck of 2+ cards that may be cut.");
        int n = sm.Table.Count(sm.Table.Count(ZoneId.RightHand) > 0 ? ZoneId.RightHand : ZoneId.Deck);
        cut.SetSelection(n - n * 2 / 5, n * 2 / 5);
        cards.SnapAll();
        rig.ApplyImmediate();
    }

    private static void PlaceCamera(Camera cam, string view)
    {
        var rig = cam.GetComponent<SeatedCameraRig>();

        switch (view)
        {
            case "top":
                cam.transform.SetPositionAndRotation(new Vector3(0f, 2.3f, -0.35f), Quaternion.LookRotation(new Vector3(0f, -1f, 0.25f)));
                break;
            case "side":
                var eye = new Vector3(1.7f, 1.55f, -1.7f);
                cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(new Vector3(0f, 0.7f, 0f) - eye));
                break;
            default:
                rig.ApplyImmediate();
                break;
        }
    }

    private static void Render(Camera cam, string file)
    {
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        bool async = ShaderUtil.allowAsyncCompilation;
        try
        {
            // Async compilation would draw not-yet-compiled shaders as invisible placeholders
            ShaderUtil.allowAsyncCompilation = false;
            cam.targetTexture = rt;
            cam.Render(); // warm-up: shadows, volumes and temporal effects settle
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
        }
        finally
        {
            ShaderUtil.allowAsyncCompilation = async;
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    private static string[] ThemeIds()
    {
        var ids = new string[ThemeService.Available.Count];
        for (int i = 0; i < ids.Length; i++) ids[i] = ThemeService.Available[i].Id;
        return ids;
    }

    private static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
