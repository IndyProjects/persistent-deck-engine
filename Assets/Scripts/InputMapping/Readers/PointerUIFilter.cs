using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Lets pointer readers ignore presses that land on interactive UI (buttons, sliders, …),
/// so clicking a HUD button never doubles as a table gesture.
/// Decorative graphics (backgrounds, labels) don't block — only hits with a Selectable in their parents.
public static class PointerUIFilter
{
    private static readonly List<RaycastResult> _hits = new List<RaycastResult>();
    private static PointerEventData _eventData;
    private static EventSystem      _eventDataOwner;

    public static bool IsOverInteractableUI(Vector2 screenPosition)
    {
        var es = EventSystem.current;
        if (es == null) return false;

        if (_eventDataOwner != es)
        {
            _eventData      = new PointerEventData(es);
            _eventDataOwner = es;
        }
        _eventData.position = screenPosition;

        _hits.Clear();
        es.RaycastAll(_eventData, _hits);
        foreach (var hit in _hits)
            if (hit.gameObject != null && hit.gameObject.GetComponentInParent<Selectable>() != null)
                return true;
        return false;
    }
}
