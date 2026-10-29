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
///        -captureOut out.png [-captureScenario showcase|fresh|shuffle] [-captureViews seat,top,side]
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
                PlaceCamera(cam, views[i].Trim());
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
