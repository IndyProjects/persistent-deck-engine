using UnityEngine;
using UnityEngine.SceneManagement;

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
        // The game picked in the menu decides the rules; the deck itself is shared by every game
        var mode = GameModes.Selected;
        StateManager = new GameStateManager(mode.CreateRules());
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

        WireInteractions();

        var hud = FindAnyObjectByType<GameStateView>();
        if (hud != null)
        {
            hud.GameName = mode.DisplayName;
            hud.GameNote = mode.Note;
        }
    }

    /// Connects the hands-on interactions, history panel and navigation to the HUD. If the legacy
    /// screen-gesture input is in the scene, it stands down while an interaction owns the mouse.
    private void WireInteractions()
    {
        var hud      = FindAnyObjectByType<GameStateView>();
        var cut      = FindAnyObjectByType<CutInteraction>();
        var riffle   = FindAnyObjectByType<RiffleInteraction>();
        var overhand = FindAnyObjectByType<OverhandInteraction>();
        var history  = FindAnyObjectByType<HistoryPanel>();
        var input    = FindAnyObjectByType<InputCoordinator>();

        if (hud != null)
            hud.Handlers = new HudHandlers
            {
                Cut      = cut      != null ? cut.TryBegin      : (System.Func<bool>)null,
                Riffle   = riffle   != null ? riffle.TryBegin   : (System.Func<bool>)null,
                Overhand = overhand != null ? overhand.TryBegin : (System.Func<bool>)null,
                History  = history  != null ? history.Toggle    : (System.Action)null,
                Menu     = ReturnToMenu
            };

        if (input == null) return;
        foreach (TableInteraction interaction in new TableInteraction[] { cut, riffle, overhand })
            if (interaction != null) interaction.ActiveChanged += active => input.enabled = !active;
    }

    private void ReturnToMenu()
    {
        Save();
        SceneManager.LoadScene(SceneNames.Menu);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    private void OnApplicationQuit() => Save();

    // Leaving the table scene (e.g. back to the menu) keeps the deck exactly as it was
    private void OnDestroy() => Save();

    private void Save()
    {
        if (_persistTable && StateManager != null)
            TableSaveStore.Save(StateManager);
    }
}
