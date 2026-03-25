using UnityEditor;

public static class InputMappingTestsMenu
{
    [MenuItem("Tools/InputMapping/Run Tests")]
    public static void RunTests() => InputMappingTests.RunAll();
}
