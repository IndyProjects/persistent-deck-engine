using UnityEngine;

/// Composition root: creates the GameStateManager, restores the saved table, and injects the manager
/// into every IStateManagerConsumer in the scene (input, HUD, 3D card view, …). Saves the table when
/// the app is paused or closed, so the deck keeps its exact order between sessions.
public class GameStateManagerBridge : MonoBehaviour
{
    [Tooltip("Load the table on start and save it on pause/quit.")]
    [SerializeField] bool _persistTable = true;

    public GameStateManager StateManager { get; private set; }

    private void Awake()
    {
        StateManager = new GameStateManager(new FreePlayRules());
        if (_persistTable)
            TableSaveStore.TryLoad(StateManager);

        int injected = 0;
        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (behaviour is IStateManagerConsumer consumer)
            {
                consumer.StateManager = StateManager;
                injected++;
            }

        if (injected == 0)
            Debug.LogWarning("[GameStateManagerBridge] No IStateManagerConsumer found; nothing will show or respond.");
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    private void OnApplicationQuit() => Save();

    private void Save()
    {
        if (_persistTable && StateManager != null)
            TableSaveStore.Save(StateManager);
    }
}
