using UnityEngine;

/// Themes that ship in code, so the game always has a look even with no theme assets.
/// Keep these minimal; richer themes belong in TableTheme assets.
public static class BuiltInThemes
{
    public static TableTheme[] Create() => new[] { Classic(), Midnight() };

    /// Green felt, red backs, neutral daylight. The defaults of TableTheme.
    public static TableTheme Classic()
    {
        var t = ScriptableObject.CreateInstance<TableTheme>();
        t.name = t.DisplayName = "Classic";
        t.Id = "classic";
        t.hideFlags = HideFlags.DontSave;
        return t;
    }

    /// Navy felt, blue backs, a single cool overhead light.
    public static TableTheme Midnight()
    {
        var t = ScriptableObject.CreateInstance<TableTheme>();
        t.name = t.DisplayName = "Midnight";
        t.Id = "midnight";
        t.hideFlags = HideFlags.DontSave;

        t.Cards.BackColor   = new Color(0.07f, 0.12f, 0.30f);
        t.Cards.BackPattern = new Color(0.16f, 0.24f, 0.48f);
        t.Cards.BackBorder  = new Color(0.86f, 0.88f, 0.93f);

        t.Table.FeltColor  = new Color(0.07f, 0.13f, 0.30f);
        t.Table.EdgeColor  = new Color(0.08f, 0.08f, 0.09f);
        t.Table.FloorColor = new Color(0.06f, 0.06f, 0.08f);
        t.Table.WallColor  = new Color(0.09f, 0.10f, 0.15f);
        t.Table.ChairColor = new Color(0.12f, 0.12f, 0.15f);
        t.Table.FigureColor = new Color(0.48f, 0.53f, 0.63f);
        t.Table.Background = new Color(0.02f, 0.025f, 0.04f);

        t.Lighting.KeyLight       = KeyLightKind.OverheadSpot;
        t.Lighting.KeyColor       = new Color(0.85f, 0.9f, 1f);
        t.Lighting.KeyIntensity   = 4f;
        t.Lighting.Ambient        = new Color(0.08f, 0.09f, 0.13f);
        t.Lighting.PostProcessing = true;
        return t;
    }
}
