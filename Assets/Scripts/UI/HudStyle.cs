using UnityEngine;

/// Shared look for the HUD bar: panel colour, a plain sprite for flat-coloured Images, and the font.
/// No art files required.
public static class HudStyle
{
    public static readonly Color PanelColor = new Color(0.18f, 0.18f, 0.28f);

    private static Sprite _shared;

    /// A 1×1 white sprite, tinted per Image.
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
}
