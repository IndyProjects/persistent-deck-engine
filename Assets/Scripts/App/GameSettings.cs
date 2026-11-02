using UnityEngine;

/// Scene names used for navigation. Both scenes must be in the build settings
/// (Tools → Persistent Deck → Set Up Scenes does this).
public static class SceneNames
{
    public const string Menu  = "MainMenu";
    public const string Table = "Table";
}

/// Player-facing settings that aren't part of a theme. Unity remembers the display mode itself;
/// the quality level is saved here and re-applied at startup.
public static class GameSettings
{
    private const string QualityKey = "pde.quality";

    public static void ApplySaved()
    {
        if (PlayerPrefs.HasKey(QualityKey))
            QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(QualityKey), 0, QualitySettings.names.Length - 1), true);
    }

    public static int Quality
    {
        get => QualitySettings.GetQualityLevel();
        set
        {
            int level = ((value % QualitySettings.names.Length) + QualitySettings.names.Length) % QualitySettings.names.Length;
            QualitySettings.SetQualityLevel(level, true);
            PlayerPrefs.SetInt(QualityKey, level);
            PlayerPrefs.Save();
        }
    }

    public static string QualityName => QualitySettings.names[Quality];

    public static bool Fullscreen
    {
        get => Screen.fullScreenMode != FullScreenMode.Windowed;
        set => Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }
}
