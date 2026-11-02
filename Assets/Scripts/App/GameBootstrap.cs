using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// Makes every scene playable without manual wiring. The main menu scene gets the title screen;
/// any other scene gets the game: the themed table environment, the cards with their hands-on controls
/// and history panel, the game root (HUD, state) and a UI EventSystem — unless the scene already
/// contains them. The legacy screen-gesture InputCoordinator is no longer added (TableControls replaces
/// it); add it to a scene by hand to use it alongside.
/// Hand-placed setups always take precedence. Runs for the first scene and every scene loaded after it.
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialise()
    {
        GameSettings.ApplySaved();
        SceneManager.sceneLoaded -= OnSceneLoaded; // play mode without domain reload keeps old handlers
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetUp(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) SetUp(scene);
    }

    private static void SetUp(Scene scene)
    {
        EnsureEventSystem();
        if (scene.name == SceneNames.Menu) SetUpMenu();
        else SetUpGame();
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        // Project uses the new Input System only, so the legacy StandaloneInputModule won't work
        es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static void SetUpMenu()
    {
        if (Object.FindAnyObjectByType<MainMenu>() == null)
            new GameObject("Main Menu").AddComponent<MainMenu>(); // builds itself in Awake
    }

    private static void SetUpGame()
    {
        if (Object.FindAnyObjectByType<TableEnvironment>() == null)
        {
            var env = new GameObject("Table Environment");
            env.AddComponent<TableEnvironment>(); // builds itself from ThemeService.Current in Awake
            env.AddComponent<ThemeHotkey>();       // T cycles themes until there is a theme menu
        }

        if (Object.FindAnyObjectByType<TableCardView>() == null)
        {
            var cards = new GameObject("Cards");
            cards.AddComponent<TableCardView>();
            cards.AddComponent<CutInteraction>();
            cards.AddComponent<RiffleInteraction>();
            cards.AddComponent<OverhandInteraction>();
            cards.AddComponent<HistoryPanel>();
            cards.AddComponent<BlackjackTable>(); // switches itself off unless Blackjack is being played
            cards.AddComponent<TableControls>();
        }

        if (Object.FindAnyObjectByType<GameStateManagerBridge>() != null)
            return;

        // The bridge's Awake injects the state manager into every consumer, so it is added last
        var root = new GameObject("GameRoot");
        root.AddComponent<GameStateView>();
        root.AddComponent<GameStateManagerBridge>();
    }
}
