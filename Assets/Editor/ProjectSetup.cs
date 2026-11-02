using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Creates the scenes the game needs and registers them in the build settings, menu first.
/// Both scenes are near-empty on purpose: GameBootstrap builds their contents at runtime.
///
/// Menu: Tools → Persistent Deck → Set Up Scenes.
/// Batch: Unity -batchmode -projectPath . -executeMethod ProjectSetup.SetUpScenesBatch
public static class ProjectSetup
{
    public const string MenuScenePath  = "Assets/Scenes/" + SceneNames.Menu + ".unity";
    public const string TableScenePath = "Assets/Scenes/" + SceneNames.Table + ".unity";

    [MenuItem("Tools/Persistent Deck/Set Up Scenes")]
    public static void SetUpScenes()
    {
        if (!File.Exists(MenuScenePath))
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            EditorSceneManager.SaveScene(scene, MenuScenePath);
            Debug.Log($"[ProjectSetup] Created {MenuScenePath}");
        }

        if (!File.Exists(TableScenePath))
            Debug.LogWarning($"[ProjectSetup] {TableScenePath} is missing; the build will only contain the menu.");

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(TableScenePath, File.Exists(TableScenePath)),
        };
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] Build scenes: " + SceneNames.Menu + ", " + SceneNames.Table);
    }

    public static void SetUpScenesBatch()
    {
        SetUpScenes();
        EditorApplication.Exit(0);
    }
}
