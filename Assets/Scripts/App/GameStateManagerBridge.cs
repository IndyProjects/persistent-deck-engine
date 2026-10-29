using UnityEngine;

/// Composition root: creates the GameStateManager, restores the saved table, and hands the manager
/// to the input and view layers. Saves the table when the app is paused or closed, so the deck
/// keeps its exact order between sessions.
public class GameStateManagerBridge : MonoBehaviour
{
    [SerializeField] InputCoordinator _coordinator;
    [SerializeField] GameStateView    _view;
    [Tooltip("Load the table on start and save it on pause/quit.")]
    [SerializeField] bool             _persistTable = true;

    public GameStateManager StateManager { get; private set; }

    private void Awake()
    {
        if (_coordinator == null) _coordinator = GetComponent<InputCoordinator>();
        if (_coordinator == null) _coordinator = FindAnyObjectByType<InputCoordinator>();
        if (_view == null)        _view        = GetComponent<GameStateView>();
        if (_view == null)        _view        = FindAnyObjectByType<GameStateView>();

        StateManager = new GameStateManager(new FreePlayRules());
        if (_persistTable)
            TableSaveStore.TryLoad(StateManager);

        if (_coordinator != null) _coordinator.StateManager = StateManager;
        else Debug.LogWarning("[GameStateManagerBridge] No InputCoordinator found; gestures are disabled.");

        if (_view != null) _view.StateManager = StateManager;
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
