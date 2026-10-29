/// Implemented by components that need the GameStateManager (input, 2D HUD, 3D table view, …).
/// GameStateManagerBridge injects it into every consumer in the scene during its Awake,
/// so consumers can rely on it from Start onwards.
public interface IStateManagerConsumer
{
    GameStateManager StateManager { set; }
}
