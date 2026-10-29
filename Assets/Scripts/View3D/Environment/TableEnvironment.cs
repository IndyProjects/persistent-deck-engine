using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Builds the table environment from the active TableTheme and rebuilds it when the theme changes:
/// a basic round table (or the theme's own environment prefab), optional floor, one key light,
/// ambient light, optional fog and post-processing, and the seated camera.
/// Everything it creates lives under one child object, so a rebuild never leaks or duplicates.
public class TableEnvironment : MonoBehaviour
{
    [SerializeField] bool _buildOnAwake = true;

    public Camera     Camera { get; private set; }
    public TableTheme Theme  { get; private set; }

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
            BuildBasicTable(theme.Table);

        BuildLighting(theme.Lighting);
        if (theme.Lighting.PostProcessing) BuildPostProcessing();
        SetupCamera(cam, theme);
    }

    // ---- Table ----

    private void BuildBasicTable(TableStyle style)
    {
        float r = TableLayout.TableRadius, top = TableLayout.SurfaceY;

        var felt = Own(Materials3D.Lit("Felt", style.FeltColor, style.FeltTexture, smoothness: 0.05f));
        var edge = Own(Materials3D.Lit("Table Edge", style.EdgeColor, smoothness: 0.45f));

        var surface = Primitive(PrimitiveType.Cylinder, "Felt", new Vector3(0f, top - 0.015f, 0f), new Vector3(r * 2f, 0.015f, r * 2f), felt);
        // Accurate round collider for pointing at the table
        surface.AddComponent<MeshCollider>().sharedMesh = surface.GetComponent<MeshFilter>().sharedMesh;

        // Edge top sits 5 mm below the felt so the two faces never coincide (z-fighting)
        Primitive(PrimitiveType.Cylinder, "Edge",     new Vector3(0f, top - 0.035f, 0f), new Vector3(r * 2f + 0.08f, 0.03f, r * 2f + 0.08f), edge);
        Primitive(PrimitiveType.Cylinder, "Pedestal", new Vector3(0f, (top - 0.06f) * 0.5f, 0f), new Vector3(0.16f, (top - 0.06f) * 0.5f, 0.16f), edge);
        Primitive(PrimitiveType.Cylinder, "Foot",     new Vector3(0f, 0.02f, 0f), new Vector3(0.8f, 0.02f, 0.8f), edge);

        if (style.ShowFloor)
        {
            var floor = Own(Materials3D.Lit("Floor", style.FloorColor, smoothness: 0.1f));
            Primitive(PrimitiveType.Plane, "Floor", Vector3.zero, new Vector3(2f, 1f, 2f), floor, keepCollider: true);
        }
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

    private GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(_built, false);
        go.transform.localPosition = position;
        go.transform.localScale    = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!keepCollider) DestroySafe(go.GetComponent<Collider>());
        return go;
    }

    private static void DestroySafe(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
