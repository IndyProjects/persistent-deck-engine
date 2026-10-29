using UnityEngine;
using UnityEngine.InputSystem;

/// Development shortcut until there is a theme menu: press T to cycle through the available themes.
public class ThemeHotkey : MonoBehaviour
{
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.tKey.wasPressedThisFrame) return;

        ThemeService.Cycle(kb.shiftKey.isPressed ? -1 : 1);
        Debug.Log($"[Theme] {ThemeService.Current.DisplayName}");
    }
}
