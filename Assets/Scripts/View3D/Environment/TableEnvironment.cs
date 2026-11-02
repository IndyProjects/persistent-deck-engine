using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Builds the table environment from the active TableTheme and rebuilds it when the theme changes:
/// a plain room with a smooth round table, a chair at every seat and stick figures in the opponents'
/// chairs (or the theme's own environment prefab), one key light, ambient light, optional fog and
/// post-processing, and the seated camera.
/// Everything it creates lives under one child object, so a rebuild never leaks or duplicates.
public class TableEnvironment : MonoBehaviour
{
    [SerializeField] bool _buildOnAwake = true;

    public Camera     Camera { get; private set; }
    public TableTheme Theme  { get; private set; }

    /// Whether the local player's figure is shown too. Off in play, where the camera is that figure.
    public bool ShowLocalFigure
    {
        get => _showLocalFigure;
        set
        {
            _showLocalFigure = value;
            if (_localFigure != null) _localFigure.SetActive(value);
        }
    }

    private bool       _showLocalFigure;
    private GameObject _localFigure;

    private Transform _built;
    private readonly System.Collections.Generic.List<Object> _ownedAssets = new System.Collections.Generic.List<Object>();

    private void Awake()
    {
        if (_buildOnAwake) Build(Camera.main, ThemeService.Current);
    }

    private void OnEnable()  => ThemeService.Changed += OnThemeChanged;
    private void OnDisable() => ThemeService.Changed -= OnThemeChanged;
    private void OnDestroy() => Clear();

    private void OnThemeChanged(TableTheme theme) => Build(Camera, theme);

    /// (Re)builds everything for <paramref name="theme"/> and configures <paramref name="cam"/>
    /// (a camera is created if null). Safe to call outside play mode.
    public void Build(Camera cam, TableTheme theme)
    {
        Clear();
        Theme  = theme;
        _built = new GameObject($"Environment ({theme.DisplayName})").transform;
        _built.SetParent(transform, false);

        if (theme.EnvironmentPrefab != null)
            Instantiate(theme.EnvironmentPrefab, _built);
        else
        {
            BuildRoom(theme.Table);
            BuildBasicTable(theme.Table);
            BuildSeats(theme.Table);
        }

        BuildLighting(theme.Lighting);
        if (theme.Lighting.PostProcessing) BuildPostProcessing();
        SetupCamera(cam, theme);
    }

    // ---- Table ----

    private const float RoomSize = 8f, RoomHeight = 3f;

    private void BuildRoom(TableStyle style)
    {
        if (style.ShowFloor)
        {
            var floor = Own(Materials3D.Lit("Floor", style.FloorColor, smoothness: 0.15f));
            Primitive(PrimitiveType.Plane, "Floor", Vector3.zero, new Vector3(RoomSize * 0.1f, 1f, RoomSize * 0.1f), floor);
        }

        var wall = Own(Materials3D.Lit("Walls", style.WallColor, smoothness: 0.05f));
        for (int i = 0; i < 4; i++)
        {
            var outward = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;
            var panel = Primitive(PrimitiveType.Quad, $"Wall {i}", outward * (RoomSize * 0.5f) + Vector3.up * (RoomHeight * 0.5f),
                                  new Vector3(RoomSize, RoomHeight, 1f), wall);
            panel.transform.rotation = Quaternion.LookRotation(outward);
            LetLightThrough(panel);
        }
        var ceiling = Primitive(PrimitiveType.Quad, "Ceiling", Vector3.up * RoomHeight, new Vector3(RoomSize, RoomSize, 1f), wall);
        ceiling.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        LetLightThrough(ceiling);
    }

    /// The key light comes from above and outside the room, so its shell mustn't shadow the table.
    private static void LetLightThrough(GameObject surface)
        => surface.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

    private void BuildBasicTable(TableStyle style)
    {
        float r = TableLayout.TableRadius, top = TableLayout.SurfaceY;

        var feltMat = Own(Materials3D.Lit("Felt", style.FeltColor, style.FeltTexture, smoothness: 0.05f));
        var wood    = Own(Materials3D.Lit("Table Wood", style.EdgeColor, smoothness: 0.45f));

        var surface = Turned("Felt", feltMat, new Vector2(0f, top), new Vector2(r, top));
        // Accurate round collider for pointing at the table
        surface.AddComponent<MeshCollider>().sharedMesh = surface.GetComponent<MeshFilter>().sharedMesh;

        // A padded rail standing just proud of the felt, then the underside
        Turned("Rail", wood,
            new Vector2(r - 0.002f, top - 0.01f), new Vector2(r - 0.002f, top + 0.008f), new Vector2(r + 0.02f, top + 0.022f),
            new Vector2(r + 0.05f, top + 0.024f), new Vector2(r + 0.075f, top + 0.012f), new Vector2(r + 0.08f, top - 0.012f),
            new Vector2(r + 0.07f, top - 0.04f), new Vector2(r + 0.04f, top - 0.05f), new Vector2(r - 0.05f, top - 0.05f),
            new Vector2(r - 0.05f, top - 0.1f), new Vector2(0f, top - 0.1f));

        float legTop = top - 0.1f;
        for (int i = 0; i < 4; i++)
        {
            // Legs sit between the seats, clear of everyone's knees
            var at = Quaternion.Euler(0f, 45f + i * 90f, 0f) * Vector3.forward * (r * 0.68f);
            Primitive(PrimitiveType.Cylinder, $"Leg {i}", at + Vector3.up * (legTop * 0.5f), new Vector3(0.09f, legTop * 0.5f, 0.09f), wood);
        }
    }

    private void BuildSeats(TableStyle style)
    {
        var chairMat  = Own(Materials3D.Lit("Chair", style.ChairColor, smoothness: 0.3f));
        var figureMat = Own(Materials3D.Lit("Stick Figure", style.FigureColor, smoothness: 0.2f));
        var layout = new TableLayout();

        for (int seat = 0; seat < TableLayout.DefaultSeats; seat++)
        {
            var chair = new GameObject($"Seat {seat}").transform;
            chair.SetParent(_built, false);
            chair.SetPositionAndRotation(layout.SeatDirection(seat) * (TableLayout.TableRadius + 0.42f), layout.SeatFacing(seat));
            SeatedFigure.BuildChair(chair, chairMat);

            var figure = SeatedFigure.Build(chair, figureMat);
            if (seat != 0) continue;
            _localFigure = figure;
            figure.SetActive(_showLocalFigure); // in play the camera is this figure's eyes
        }
    }

    private GameObject Turned(string name, Material material, params Vector2[] profile)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(_built, false);
        go.GetComponent<MeshFilter>().sharedMesh = Own(MeshShapes.Lathe(name, profile));
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        // These surfaces are open shells: both sides must cast, or light leaks through the shadow
        renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
        return go;
    }

    // ---- Lighting ----

    private void BuildLighting(LightingStyle style)
    {
        // The theme owns the lighting: switch off any directional light the scene brought with it
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional && !light.transform.IsChildOf(_built))
                light.enabled = false;

        var key = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
        key.transform.SetParent(_built, false);
        key.color     = style.KeyColor;
        key.intensity = style.KeyIntensity;
        key.shadows   = style.Shadows ? LightShadows.Soft : LightShadows.None;

        if (style.KeyLight == KeyLightKind.OverheadSpot)
        {
            key.type           = LightType.Spot;
            key.spotAngle      = 120f;
            key.innerSpotAngle = 70f;
            key.range          = 5f;
            key.transform.SetPositionAndRotation(new Vector3(0f, TableLayout.SurfaceY + 1.1f, 0f), Quaternion.Euler(90f, 0f, 0f));
        }
        else
        {
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(style.KeyAngles.x, style.KeyAngles.y, 0f);
        }

        RenderSettings.skybox       = null;
        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = style.Ambient;
        RenderSettings.fog          = style.Fog;
        RenderSettings.fogMode      = FogMode.ExponentialSquared;
        RenderSettings.fogColor     = style.FogColor;
        RenderSettings.fogDensity   = style.FogDensity;
    }

    private void BuildPostProcessing()
    {
        var volume = new GameObject("Post Processing", typeof(Volume)).GetComponent<Volume>();
        volume.transform.SetParent(_built, false);
        volume.isGlobal = true;
        volume.priority = 100f;

        var profile = Own(ScriptableObject.CreateInstance<VolumeProfile>());
        profile.name = "Theme Post Processing";
        volume.sharedProfile = profile;

        profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
        var vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.3f);
        vignette.smoothness.Override(0.4f);
    }

    // ---- Camera ----

    private void SetupCamera(Camera cam, TableTheme theme)
    {
        if (cam == null)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            cam = go.GetComponent<Camera>();
        }
        Camera = cam;

        cam.fieldOfView     = 55f;
        cam.nearClipPlane   = 0.02f;
        cam.farClipPlane    = 30f;
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = theme.Table.Background;

        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = theme.Lighting.PostProcessing;
        data.antialiasing         = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        var rig = cam.GetComponent<SeatedCameraRig>();
        if (rig == null) rig = cam.gameObject.AddComponent<SeatedCameraRig>();
        rig.ApplyImmediate();
    }

    // ---- Helpers ----

    private void Clear()
    {
        if (_built != null) DestroySafe(_built.gameObject);
        foreach (var asset in _ownedAssets) DestroySafe(asset);
        _ownedAssets.Clear();
        _built = null;
    }

    /// Tracks a runtime-created asset so the next rebuild destroys it.
    private T Own<T>(T asset) where T : Object
    {
        _ownedAssets.Add(asset);
        return asset;
    }

    private GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(_built, false);
        go.transform.localPosition = position;
        go.transform.localScale    = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        DestroySafe(go.GetComponent<Collider>());
        return go;
    }

    private static void DestroySafe(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
