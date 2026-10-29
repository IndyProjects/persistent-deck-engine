using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// Makes any scene playable without manual wiring: creates the game root
/// (InputCoordinator → GameStateManagerBridge → GameStateView) and a UI EventSystem
/// unless the scene already contains them. Hand-placed setups always take precedence.
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

        if (Object.FindAnyObjectByType<GameStateManagerBridge>() != null)
            return;

        // Order matters: the bridge's Awake looks up the coordinator on the same GameObject
        var root = new GameObject("GameRoot");
        root.AddComponent<InputCoordinator>();
        root.AddComponent<GameStateManagerBridge>();
        root.AddComponent<GameStateView>();
    }
}
