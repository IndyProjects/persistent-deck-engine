using UnityEditor;

/// Adds a menu item so DeckEngine tests can be run directly from the Unity

public static class DeckEngineTestsMenu
{
    [MenuItem("Tools/DeckEngine/Run Tests")]
    public static void RunTests() => DeckEngineTests.RunAll();
}
 