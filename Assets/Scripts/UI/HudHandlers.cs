using System;

/// Hooks the HUD calls for things that live outside the UI layer (interactions, panels, navigation).
/// Set by GameStateManagerBridge before the HUD starts; any left null fall back or hide their button.
public sealed class HudHandlers
{
    /// Start the hands-on cut / riffle / overhand; return false if it can't start (the button then
    /// makes an automatic one instead).
    public Func<bool> Cut, Riffle, Overhand;

    /// Toggle the deck history panel (button hidden when null).
    public Action History;

    /// Return to the main menu (button hidden when null).
    public Action Menu;
}
