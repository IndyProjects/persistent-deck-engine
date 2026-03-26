using UnityEngine;

public class GameStateManagerBridge : MonoBehaviour
{
    [SerializeField] InputCoordinator _coordinator;

    public GameStateManager StateManager { get; private set; }

    private void Awake()
    {
        if (_coordinator == null)
            _coordinator = GetComponent<InputCoordinator>();

        StateManager = new GameStateManager();
        _coordinator.StateManager = StateManager;
    }
}
