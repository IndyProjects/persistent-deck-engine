using UnityEditor;

public static class GameStateTestsMenu
{
    [MenuItem("Tools/GameState/Run Tests")]
    public static void RunTests() => GameStateTests.RunAll();
}
