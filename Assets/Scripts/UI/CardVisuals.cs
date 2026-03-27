using UnityEngine;

/// Static helpers for placeholder card rendering. No art files required.
public static class CardVisuals
{
    private static readonly string[] Ranks = { "A","2","3","4","5","6","7","8","9","10","J","Q","K" };
    private static readonly string[] Suits = { "♥","♦","♣","♠" };

    private static readonly Color[] BgColors =
    {
        new Color(0.95f, 0.82f, 0.82f),  // Hearts   — light red
        new Color(0.96f, 0.91f, 0.78f),  // Diamonds — light orange
        new Color(0.82f, 0.93f, 0.82f),  // Clubs    — light green
        new Color(0.82f, 0.88f, 0.96f),  // Spades   — light blue
    };

    public static readonly Color BackColor  = new Color(0.13f, 0.38f, 0.22f);
    public static readonly Color BoardColor = new Color(0.10f, 0.12f, 0.18f);
    public static readonly Color PanelColor = new Color(0.18f, 0.18f, 0.28f);

    private static Sprite _shared;

    /// A 1×1 white sprite shared by all card Images.
    public static Sprite SharedSprite
    {
        get
        {
            if (_shared != null) return _shared;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _shared = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _shared;
        }
    }

    public static Font DefaultFont
    {
        get
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f != null ? f : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }

    public static Color  BgColor(int card)   => BgColors[DeckEngine.GetSuit(card)];
    public static Color  TextColor(int card)  => DeckEngine.GetSuit(card) < 2
                                                    ? new Color(0.75f, 0f,   0f)
                                                    : new Color(0.1f,  0.1f, 0.1f);
    public static string Label(int card)      => Ranks[DeckEngine.GetRank(card)] + Suits[DeckEngine.GetSuit(card)];
}
