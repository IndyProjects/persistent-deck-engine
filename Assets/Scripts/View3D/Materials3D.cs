using UnityEngine;
using UnityEngine.Rendering;

/// Creates URP Lit materials at runtime, so the table needs no material assets.
public static class Materials3D
{
    private static Shader _lit;
    private static Shader LitShader => _lit != null ? _lit
        : (_lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

    public static Material Lit(string name, Color color, Texture texture = null, float smoothness = 0.2f,
                               float metallic = 0f)
    {
        var m = new Material(LitShader) { name = name, hideFlags = HideFlags.DontSave };
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        if (texture != null)
        {
            m.SetTexture("_BaseMap", texture);
            m.SetTexture("_MainTex", texture);
        }
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", metallic);
        return m;
    }

    /// Lit material whose texture alpha cuts out the shape (rounded card corners).
    public static Material Cutout(string name, Texture texture, float smoothness = 0.3f)
    {
        var m = Lit(name, Color.white, texture, smoothness);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        return m;
    }
}
