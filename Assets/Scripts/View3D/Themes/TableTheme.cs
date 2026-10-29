using System;
using UnityEngine;

/// Everything that defines how the table looks: cards, table, lighting and (optionally) a whole
/// environment prefab. Views read the active theme from ThemeService and rebuild when it changes,
/// so adding a theme never requires code changes: create one via
/// Assets → Create → Persistent Deck → Table Theme and put it in a Resources/Themes folder.
[CreateAssetMenu(menuName = "Persistent Deck/Table Theme", fileName = "New Table Theme")]
public class TableTheme : ScriptableObject
{
    [Tooltip("Stable identifier saved with the player's choice. Keep it unique and don't rename it once shipped.")]
    public string Id = "classic";
    public string DisplayName = "Classic";

    public CardStyle     Cards    = new CardStyle();
    public TableStyle    Table    = new TableStyle();
    public LightingStyle Lighting = new LightingStyle();

    [Tooltip("Optional prefab that replaces the built-in table and floor. Its playing surface must sit at " +
             "TableLayout.SurfaceY (0.76 m) with radius TableLayout.TableRadius, centred on the origin.")]
    public GameObject EnvironmentPrefab;
}

[Serializable]
public class CardStyle
{
    [Header("Generated faces")]
    public Color Paper    = new Color(0.97f, 0.955f, 0.92f);
    public Color RedInk   = new Color(0.76f, 0.09f, 0.11f);
    public Color BlackInk = new Color(0.10f, 0.10f, 0.12f);

    [Header("Generated back")]
    public Color BackColor   = new Color(0.42f, 0.06f, 0.09f);
    public Color BackPattern = new Color(0.62f, 0.16f, 0.17f);
    public Color BackBorder  = new Color(0.93f, 0.89f, 0.80f);

    [Header("Custom art (optional)")]
    [Tooltip("A complete atlas replacing the generated one. Must follow CardAtlas's layout: 14 × 4 cells " +
             "of (TileW + 2·Pad) × (TileH + 2·Pad) px; column = rank (2…A), row = suit (♥ ♦ ♣ ♠); " +
             "column 13 holds the back (row 0) and edge (row 1). Alpha cuts out the rounded corners.")]
    public Texture2D AtlasOverride;

    [Range(0f, 1f)] public float Smoothness = 0.3f;
}

[Serializable]
public class TableStyle
{
    public Color FeltColor = new Color(0.08f, 0.30f, 0.17f);
    [Tooltip("Optional felt texture (tinted by FeltColor).")]
    public Texture2D FeltTexture;
    public Color EdgeColor  = new Color(0.25f, 0.14f, 0.08f);
    public bool  ShowFloor  = true;
    public Color FloorColor = new Color(0.18f, 0.17f, 0.17f);
    [Tooltip("Camera clear colour behind everything.")]
    public Color Background = new Color(0.10f, 0.10f, 0.11f);
}

public enum KeyLightKind { Directional, OverheadSpot }

[Serializable]
public class LightingStyle
{
    public KeyLightKind KeyLight = KeyLightKind.Directional;
    public Color   KeyColor     = new Color(1f, 0.96f, 0.9f);
    public float   KeyIntensity = 1.2f;
    [Tooltip("Directional light angles (pitch, yaw). Ignored for OverheadSpot.")]
    public Vector2 KeyAngles    = new Vector2(60f, -25f);
    public bool    Shadows      = true;
    public Color   Ambient      = new Color(0.35f, 0.35f, 0.38f);

    [Header("Atmosphere")]
    public bool  Fog        = false;
    public Color FogColor   = new Color(0.1f, 0.1f, 0.11f);
    public float FogDensity = 0.08f;
    [Tooltip("ACES tonemapping and a light vignette.")]
    public bool  PostProcessing = false;
}
