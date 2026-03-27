using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandsPanel : MonoBehaviour
{
    private readonly List<HandView> _hands = new List<HandView>();

    private void Awake()
    {
        var layout = gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing              = 10f;
        layout.childAlignment       = TextAnchor.UpperLeft;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(8, 8, 8, 8);
    }

    public void Refresh(int[][] hands)
    {
        while (_hands.Count < hands.Length)
        {
            var go = new GameObject($"Hand{_hands.Count}", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = CardView.Size.y + 12f;
            _hands.Add(go.AddComponent<HandView>());
        }

        for (int i = 0; i < _hands.Count; i++)
        {
            bool active = i < hands.Length;
            _hands[i].gameObject.SetActive(active);
            if (active) _hands[i].Refresh(i, hands[i]);
        }
    }
}
