using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// Makes any scene playable without manual wiring: builds the themed table environment, the card
/// view, the game root (input, HUD, state) and a UI EventSystem, unless the scene already contains them.
/// Hand-placed setups always take precedence.
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSceneSetup()
    {
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            // Project uses the new Input System only, so the legacy StandaloneInputModule won't work
            es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        if (Object.FindAnyObjectByType<TableEnvironment>() == null)
        {
            var env = new GameObject("Table Environment");
            env.AddComponent<TableEnvironment>(); // builds itself from ThemeService.Current in Awake
            env.AddComponent<ThemeHotkey>();       // T cycles themes until there is a theme menu
        }

        if (Object.FindAnyObjectByType<TableCardView>() == null)
            new GameObject("Cards").AddComponent<TableCardView>();

        if (Object.FindAnyObjectByType<GameStateManagerBridge>() != null)
            return;

        // The bridge's Awake injects the state manager into every consumer, so it is added last
        var root = new GameObject("GameRoot");
        root.AddComponent<InputCoordinator>();
        root.AddComponent<GameStateView>();
        root.AddComponent<GameStateManagerBridge>();
    }
}
