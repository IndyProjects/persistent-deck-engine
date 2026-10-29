using System;
using System.Collections.Generic;
using UnityEngine;

/// Procedurally painted card texture atlas — 52 faces, a back and an edge tile — so the game needs
/// no card art. Colours come from the theme's CardStyle. Shapes are drawn from signed distance
/// functions, which gives anti-aliased edges. A theme may instead supply its own atlas in this layout.
///
/// Layout: 14 columns × 4 rows of tiles. Column = rank (0 = 2 … 12 = Ace), row = suit (DeckEngine order).
/// Column 13: row 0 = card back, row 1 = edge.
public static class CardAtlas
{
    public const int TileW = 180, TileH = 252, Pad = 4;
    private const int Cols = 14, Rows = 4;
    private const int CellW = TileW + 2 * Pad, CellH = TileH + 2 * Pad;

    public static int Width  => Cols * CellW;
    public static int Height => Rows * CellH;

    public static Rect FaceUV(int card) => TileUV(DeckEngine.GetRank(card), DeckEngine.GetSuit(card));
    public static Rect BackUV => TileUV(13, 0);
    public static Rect EdgeUV => TileUV(13, 1);

    private static Rect TileUV(int col, int row)
        => new Rect((col * CellW + Pad) / (float)Width, (row * CellH + Pad) / (float)Height,
                    TileW / (float)Width, TileH / (float)Height);

    /// Paints a new atlas in the given style. The caller owns (and should destroy) the texture.
    public static Texture2D Build(CardStyle style)
    {
        var p = new Painter(Width, Height);
        for (int card = 0; card < DeckEngine.DeckSize; card++)
            PaintFace(p, DeckEngine.GetRank(card), DeckEngine.GetSuit(card), style);
        PaintBack(p, style);
        PaintEdge(p, style);

        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, true)
        {
            name       = "CardAtlas",
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 8,
            hideFlags  = HideFlags.DontSave
        };
        tex.SetPixels32(p.Pixels);
        tex.Apply(true, true);
        return tex;
    }

    // ---- Faces ----

    private static readonly string[] RankText = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };

    /// Pip positions for ranks 2–10 as (x, t): x across 0–1, t from the top 0–1.
    /// Pips in the lower half are drawn upside down, as on real cards.
    private static readonly float[][] Pips =
    {
        new[] { .5f, .2f, .5f, .8f },
        new[] { .5f, .2f, .5f, .5f, .5f, .8f },
        new[] { .25f, .2f, .75f, .2f, .25f, .8f, .75f, .8f },
        new[] { .25f, .2f, .75f, .2f, .5f, .5f, .25f, .8f, .75f, .8f },
        new[] { .25f, .2f, .75f, .2f, .25f, .5f, .75f, .5f, .25f, .8f, .75f, .8f },
        new[] { .25f, .2f, .75f, .2f, .5f, .35f, .25f, .5f, .75f, .5f, .25f, .8f, .75f, .8f },
        new[] { .25f, .2f, .75f, .2f, .5f, .35f, .25f, .5f, .75f, .5f, .5f, .65f, .25f, .8f, .75f, .8f },
        new[] { .25f, .2f, .75f, .2f, .25f, .4f, .75f, .4f, .5f, .5f, .25f, .6f, .75f, .6f, .25f, .8f, .75f, .8f },
        new[] { .25f, .2f, .75f, .2f, .5f, .3f, .25f, .4f, .75f, .4f, .25f, .6f, .75f, .6f, .5f, .7f, .25f, .8f, .75f, .8f },
    };

    private static void PaintFace(Painter p, int rank, int suit, CardStyle style)
    {
        p.BeginTile(rank, suit, style.Paper);
        Color ink = suit < 2 ? style.RedInk : style.BlackInk;
        var suitShape = Suits[suit];

        PaintCardBody(p, style.Paper);

        // Corner indices (top-left upright, bottom-right rotated)
        foreach (bool flip in new[] { false, true })
        {
            Vector2 rankAt = Corner(new Vector2(19, TileH - 27), flip);
            Vector2 suitAt = Corner(new Vector2(19, TileH - 62), flip);
            DrawText(p, RankText[rank], rankAt, 32f, flip, ink);
            p.Shape(suitAt, 11f, flip, 1.1f, suitShape, ink);
        }

        if (rank <= 8) // 2–10: pip layout
        {
            var pips = Pips[rank];
            for (int i = 0; i < pips.Length; i += 2)
            {
                var at = new Vector2(TileW * 0.5f + (pips[i] - 0.5f) * 112f, 30f + (1f - pips[i + 1]) * 192f);
                p.Shape(at, 15f, pips[i + 1] > 0.5f, 1.1f, suitShape, ink);
            }
        }
        else if (rank == 12) // Ace: one large pip
        {
            p.Shape(new Vector2(TileW * 0.5f, TileH * 0.5f), 46f, false, 1.1f, suitShape, ink);
        }
        else // J, Q, K: framed panel with a large letter
        {
            var c = new Vector2(TileW * 0.5f, TileH * 0.5f);
            p.Shape(c, 1f, false, 92f, q => Mathf.Abs(Sdf.RoundedBox(q, new Vector2(50, 80), 8)) - 1.4f, ink);
            p.Shape(c, 1f, false, 92f, q => Sdf.RoundedBox(q, new Vector2(46, 76), 6), new Color(ink.r, ink.g, ink.b, 0.07f));
            DrawText(p, RankText[rank], c, 74f, false, ink);
            p.Shape(c + new Vector2(-32, 58), 11f, false, 1.1f, suitShape, ink);
            p.Shape(c + new Vector2(32, -58), 11f, true, 1.1f, suitShape, ink);
        }
    }

    private static Vector2 Corner(Vector2 topLeft, bool flip)
        => flip ? new Vector2(TileW - topLeft.x, TileH - topLeft.y) : topLeft;

    private static void PaintCardBody(Painter p, Color fill)
    {
        var c = new Vector2(TileW * 0.5f, TileH * 0.5f);
        var half = new Vector2(TileW * 0.5f - 1f, TileH * 0.5f - 1f);
        var outline = new Color(fill.r * 0.64f, fill.g * 0.63f, fill.b * 0.61f);
        p.Shape(c, 1f, false, TileH, q => Sdf.RoundedBox(q, half, 13f), fill);
        p.Shape(c, 1f, false, TileH, q => Mathf.Abs(Sdf.RoundedBox(q, half, 13f) + 0.8f) - 0.8f, outline);
    }

    private static void PaintBack(Painter p, CardStyle style)
    {
        Color back = style.BackColor, line = style.BackPattern, border = style.BackBorder;
        p.BeginTile(13, 0, back);
        PaintCardBody(p, border);

        var c = new Vector2(TileW * 0.5f, TileH * 0.5f);
        var inner = new Vector2(TileW * 0.5f - 11f, TileH * 0.5f - 11f);
        p.Shape(c, 1f, false, TileH, q => Sdf.RoundedBox(q, inner, 8f), back);

        // Diamond lattice inside the inner panel
        const float period = 16f;
        p.Shape(c, 1f, false, TileH, q =>
        {
            float panel = Sdf.RoundedBox(q, inner - new Vector2(5, 5), 5f);
            // Distance to the nearest line of each diagonal family (x ± y = k·period)
            float s1 = (q.x + q.y) / period, s2 = (q.x - q.y) / period;
            float d1 = Mathf.Abs(s1 - Mathf.Round(s1)) * period * 0.7071f;
            float d2 = Mathf.Abs(s2 - Mathf.Round(s2)) * period * 0.7071f;
            return Mathf.Max(panel, Mathf.Min(d1, d2) - 1.1f);
        }, line);

        // Central emblem: a cream diamond with a red diamond inside
        p.Shape(c, 1f, false, 60f, q => Sdf.Rhombus(q, new Vector2(30, 46)), border);
        p.Shape(c, 1f, false, 60f, q => Sdf.Rhombus(q, new Vector2(22, 36)), back);
        p.Shape(c, 1f, false, 60f, q => Mathf.Abs(Sdf.Rhombus(q, new Vector2(14, 24))) - 1.4f, border);
    }

    private static void PaintEdge(Painter p, CardStyle style)
    {
        var edge = style.Paper * 0.93f;
        edge.a = 1f;
        p.BeginTile(13, 1, edge);
        p.Shape(new Vector2(TileW * 0.5f, TileH * 0.5f), 1f, false, TileH,
            q => Sdf.Box(q, new Vector2(TileW, TileH)), edge);
    }

    // ---- Suits (local space roughly [-1, 1], y up) ----

    private static readonly Func<Vector2, float>[] Suits = { Heart, Diamond, Club, Spade };

    private static readonly Vector2[] HeartTip = { new Vector2(-0.585f, 0.17f), new Vector2(0f, -0.78f), new Vector2(0.585f, 0.17f) };
    private static readonly Vector2[] Stem     = { new Vector2(0f, -0.05f), new Vector2(-0.3f, -0.9f), new Vector2(0.3f, -0.9f) };

    private static float Heart(Vector2 q)
        => Mathf.Min(Mathf.Min(Sdf.Circle(q, new Vector2(-0.28f, 0.3f), 0.32f),
                               Sdf.Circle(q, new Vector2(0.28f, 0.3f), 0.32f)),
                     Sdf.ConvexPolygon(q, HeartTip));

    private static float Diamond(Vector2 q) => Sdf.Rhombus(q, new Vector2(0.66f, 0.95f));

    private static float Spade(Vector2 q)
        => Mathf.Min(Heart(new Vector2(q.x, 0.14f - q.y)), Sdf.ConvexPolygon(q, Stem));

    private static float Club(Vector2 q)
    {
        float lobes = Mathf.Min(Sdf.Circle(q, new Vector2(0f, 0.4f), 0.31f),
                      Mathf.Min(Sdf.Circle(q, new Vector2(-0.36f, -0.08f), 0.31f),
                                Sdf.Circle(q, new Vector2(0.36f, -0.08f), 0.31f)));
        lobes = Mathf.Min(lobes, Sdf.Circle(q, new Vector2(0f, 0.08f), 0.2f));
        return Mathf.Min(lobes, Sdf.ConvexPolygon(q, Stem));
    }

    // ---- Stroke font for ranks (glyph space: x 0–0.6, y 0–1) ----

    private const float StrokeHalfWidth = 0.068f;

    private static readonly Dictionary<char, float[][]> Glyphs = BuildGlyphs();

    private static Dictionary<char, float[][]> BuildGlyphs()
    {
        var g = new Dictionary<char, float[][]>
        {
            ['A'] = new[] { new[] { 0f, 0f, .3f, 1f, .6f, 0f }, new[] { .1f, .33f, .5f, .33f } },
            ['K'] = new[] { new[] { 0f, 0f, 0f, 1f }, new[] { .6f, 1f, 0f, .38f }, new[] { .17f, .55f, .6f, 0f } },
            ['J'] = new[] { new[] { .15f, 1f, .6f, 1f }, new[] { .48f, 1f, .48f, .25f, .42f, .07f, .26f, 0f, .1f, .05f, .02f, .2f } },
            ['Q'] = new[] { Ellipse(.3f, .52f, .3f, .48f), new[] { .36f, .24f, .62f, -.06f } },
            ['0'] = new[] { Ellipse(.3f, .5f, .28f, .5f) },
            ['1'] = new[] { new[] { .1f, .78f, .32f, 1f, .32f, 0f }, new[] { .1f, 0f, .54f, 0f } },
            ['2'] = new[] { new[] { .02f, .76f, .1f, .93f, .3f, 1f, .5f, .93f, .58f, .76f, .52f, .57f, 0f, 0f, .6f, 0f } },
            ['3'] = new[] { new[] { .02f, .9f, .22f, 1f, .45f, .97f, .56f, .82f, .5f, .64f, .25f, .56f },
                            new[] { .25f, .56f, .5f, .5f, .6f, .3f, .53f, .09f, .3f, 0f, .02f, .08f } },
            ['4'] = new[] { new[] { .45f, 0f, .45f, 1f, 0f, .32f, .6f, .32f } },
            ['5'] = new[] { new[] { .56f, 1f, .08f, 1f, .03f, .56f, .25f, .62f, .48f, .58f, .6f, .36f, .53f, .1f, .3f, 0f, .02f, .08f } },
            ['6'] = new[] { new[] { .55f, .93f, .33f, 1f, .12f, .9f, .02f, .62f, .02f, .3f, .1f, .07f, .3f, 0f, .5f, .07f, .6f, .3f, .52f, .52f, .3f, .6f, .1f, .52f, .02f, .36f } },
            ['7'] = new[] { new[] { 0f, 1f, .6f, 1f, .2f, 0f } },
            ['8'] = new[] { Ellipse(.3f, .76f, .23f, .24f), Ellipse(.3f, .27f, .29f, .27f) },
        };
        // 9 is 6 rotated half a turn
        var six = g['6'][0];
        var nine = new float[six.Length];
        for (int i = 0; i < six.Length; i += 2) { nine[i] = .6f - six[i]; nine[i + 1] = 1f - six[i + 1]; }
        g['9'] = new[] { nine };
        return g;
    }

    private static float[] Ellipse(float cx, float cy, float rx, float ry, int segments = 28)
    {
        var pts = new float[(segments + 1) * 2];
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            pts[i * 2]     = cx + Mathf.Cos(a) * rx;
            pts[i * 2 + 1] = cy + Mathf.Sin(a) * ry;
        }
        return pts;
    }

    /// Draws a rank string centred at <paramref name="center"/>, <paramref name="height"/> pixels tall.
    /// "10" is condensed to fit the same width as single characters.
    private static void DrawText(Painter p, string text, Vector2 center, float height, bool flip, Color color)
    {
        float condense = text.Length > 1 ? 0.62f : 1f;
        float advance  = 0.66f * condense;
        float totalW   = advance * (text.Length - 1) + 0.6f * condense;

        for (int i = 0; i < text.Length; i++)
        {
            var strokes = Glyphs[text[i]];
            float originX = -totalW * 0.5f + i * advance;
            p.Shape(center, height, flip, 0.75f, q =>
            {
                // q is in units of glyph height, centred on the string
                var g = new Vector2((q.x - originX) / condense, q.y + 0.5f);
                float d = float.MaxValue;
                foreach (var s in strokes)
                    for (int k = 0; k + 3 < s.Length; k += 2)
                        d = Mathf.Min(d, Sdf.Segment(g, new Vector2(s[k], s[k + 1]), new Vector2(s[k + 2], s[k + 3])));
                return (d - StrokeHalfWidth) * condense;
            }, color);
        }
    }

    // ---- Rasteriser ----

    private sealed class Painter
    {
        public readonly Color32[] Pixels;
        private readonly int _w;
        private int _ox, _oy;

        public Painter(int w, int h)
        {
            _w = w;
            Pixels = new Color32[w * h];
        }

        /// Selects a tile and fills it (padding included) with <paramref name="bleed"/> at zero alpha,
        /// so mipmaps fade to the card's own colour instead of black.
        public void BeginTile(int col, int row, Color bleed)
        {
            _ox = col * CellW + Pad;
            _oy = row * CellH + Pad;
            var c = (Color32)new Color(bleed.r, bleed.g, bleed.b, 0f);
            for (int y = -Pad; y < TileH + Pad; y++)
                for (int x = -Pad; x < TileW + Pad; x++)
                    Pixels[(_oy + y) * _w + _ox + x] = c;
        }

        /// Fills an SDF shape. <paramref name="sdf"/> takes local coordinates
        /// ((pixel − center) / scale, rotated 180° if flipped) and returns distance in local units.
        /// <paramref name="extent"/> bounds the shape in local units.
        public void Shape(Vector2 center, float scale, bool flip, float extent, Func<Vector2, float> sdf, Color color)
        {
            int x0 = Mathf.Max(0,     Mathf.FloorToInt(center.x - extent * scale) - 1);
            int x1 = Mathf.Min(TileW, Mathf.CeilToInt (center.x + extent * scale) + 1);
            int y0 = Mathf.Max(0,     Mathf.FloorToInt(center.y - extent * scale) - 1);
            int y1 = Mathf.Min(TileH, Mathf.CeilToInt (center.y + extent * scale) + 1);

            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    var local = (new Vector2(x + 0.5f, y + 0.5f) - center) / scale;
                    if (flip) local = -local;
                    float coverage = Mathf.Clamp01(0.5f - sdf(local) * scale) * color.a;
                    if (coverage <= 0f) continue;
                    Blend(ref Pixels[(_oy + y) * _w + _ox + x], color, coverage);
                }
        }

        private static void Blend(ref Color32 dst, Color src, float a)
        {
            Color d = dst;
            float outA = a + d.a * (1f - a);
            if (outA <= 0f) return;
            float k = d.a * (1f - a);
            dst = new Color((src.r * a + d.r * k) / outA, (src.g * a + d.g * k) / outA,
                            (src.b * a + d.b * k) / outA, outA);
        }
    }

    // ---- Distance functions ----

    private static class Sdf
    {
        public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        public static float Box(Vector2 p, Vector2 half)
        {
            var d = new Vector2(Mathf.Abs(p.x) - half.x, Mathf.Abs(p.y) - half.y);
            return Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        public static float RoundedBox(Vector2 p, Vector2 half, float r) => Box(p, half - new Vector2(r, r)) - r;

        public static float Rhombus(Vector2 p, Vector2 half)
        {
            float a = half.x, b = half.y;
            return (Mathf.Abs(p.x) / a + Mathf.Abs(p.y) / b - 1f) / Mathf.Sqrt(1f / (a * a) + 1f / (b * b));
        }

        public static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
            return (p - (a + ab * t)).magnitude;
        }

        /// Counter-clockwise convex polygon; exact inside, close enough outside for 1-pixel anti-aliasing.
        public static float ConvexPolygon(Vector2 p, Vector2[] v)
        {
            float d = float.MinValue;
            for (int i = 0; i < v.Length; i++)
            {
                var a = v[i];
                var e = v[(i + 1) % v.Length] - a;
                var n = new Vector2(e.y, -e.x).normalized;
                d = Mathf.Max(d, Vector2.Dot(p - a, n));
            }
            return d;
        }
    }
}
