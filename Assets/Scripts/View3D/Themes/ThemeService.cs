using System;
using System.Collections.Generic;
using UnityEngine;

/// Holds the active TableTheme and tells views when it changes. Themes come from two places:
/// built-in themes defined in code (always available, no assets needed) and TableTheme assets in any
/// Resources/Themes folder. An asset whose Id matches a built-in replaces it.
/// The player's choice is remembered between sessions.
public static class ThemeService
{
    public const string PrefsKey     = "pde.theme";
    public const string ResourcePath = "Themes";

    private static List<TableTheme> _available;
    private static TableTheme       _current;

    /// Raised after the active theme changes.
    public static event Action<TableTheme> Changed;

    public static IReadOnlyList<TableTheme> Available => _available ?? (_available = Discover());

    /// The active theme: the player's saved choice if it still exists, otherwise the first available.
    public static TableTheme Current => _current != null ? _current : (_current = Find(SavedId()) ?? Available[0]);

    public static TableTheme Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var theme in Available)
            if (theme.Id == id) return theme;
        return null;
    }

    /// Activates a theme, remembers it for next time, and notifies views.
    public static void Select(TableTheme theme, bool remember = true)
    {
        if (theme == null) throw new ArgumentNullException(nameof(theme));
        if (theme == _current) return;
        _current = theme;
        if (remember)
        {
            PlayerPrefs.SetString(PrefsKey, theme.Id);
            PlayerPrefs.Save();
        }
        Changed?.Invoke(theme);
    }

    public static bool Select(string id, bool remember = true)
    {
        var theme = Find(id);
        if (theme == null) return false;
        Select(theme, remember);
        return true;
    }

    /// Steps through the available themes (wraps around).
    public static void Cycle(int direction = 1)
    {
        var list = Available;
        int i = 0;
        for (int k = 0; k < list.Count; k++)
            if (list[k] == Current) { i = k; break; }
        Select(list[((i + direction) % list.Count + list.Count) % list.Count]);
    }

    /// Re-scans for theme assets (e.g. after downloading new themes) and drops the cached selection.
    public static void Refresh()
    {
        _available = null;
        _current   = null;
    }

    private static string SavedId() => PlayerPrefs.GetString(PrefsKey, null);

    private static List<TableTheme> Discover()
    {
        var themes = new List<TableTheme>(BuiltInThemes.Create());
        foreach (var asset in Resources.LoadAll<TableTheme>(ResourcePath))
        {
            int existing = themes.FindIndex(t => t.Id == asset.Id);
            if (existing >= 0) themes[existing] = asset;
            else themes.Add(asset);
        }
        return themes;
    }

    // Static state survives play-mode entry when domain reload is disabled; start clean each time
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _available = null;
        _current   = null;
        Changed    = null;
    }
}
