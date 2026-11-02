using UnityEngine;
using UnityEngine.InputSystem;

/// Cycles through the available themes with the Theme key (T by default; Shift goes backwards).
public class ThemeHotkey : MonoBehaviour
{
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || ControlsPanel.AnyOpen || !Controls.Pressed(ControlAction.Theme)) return;

        ThemeService.Cycle(kb.shiftKey.isPressed ? -1 : 1);
        Debug.Log($"[Theme] {ThemeService.Current.DisplayName}");
    }
}
