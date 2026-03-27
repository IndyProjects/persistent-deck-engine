using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandView : MonoBehaviour
{
    private Text                _playerLabel;
    private Transform           _cardRow;
    private readonly List<CardView> _pool = new List<CardView>();

    private void Awake()
    {
        var hLayout = gameObject.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing              = 6f;
        hLayout.childAlignment       = TextAnchor.MiddleLeft;
        hLayout.childForceExpandWidth  = false;
        hLayout.childForceExpandHeight = false;
        hLayout.padding = new RectOffset(8, 8, 4, 4);

        var lblGO = new GameObject("Lbl", typeof(RectTransform), typeof(Text));
        lblGO.transform.SetParent(transform, false);
        var lle = lblGO.AddComponent<LayoutElement>();
        lle.preferredWidth  = 32f;
        lle.preferredHeight = CardView.Size.y;
        _playerLabel           = lblGO.GetComponent<Text>();
        _playerLabel.font      = CardVisuals.DefaultFont;
        _playerLabel.fontSize  = 13;
        _playerLabel.alignment = TextAnchor.MiddleCenter;
        _playerLabel.color     = Color.white;

        var rowGO = new GameObject("Cards", typeof(RectTransform));
        rowGO.transform.SetParent(transform, false);
        var rle = rowGO.AddComponent<LayoutElement>();
        rle.flexibleWidth = 1f;
        var rowLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing              = 4f;
        rowLayout.childForceExpandWidth  = false;
        rowLayout.childForceExpandHeight = false;
        _cardRow = rowGO.transform;
    }

    public void Refresh(int playerIndex, int[] cards)
    {
        _playerLabel.text = $"P{playerIndex + 1}";

        while (_pool.Count < cards.Length)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(CardView));
            go.transform.SetParent(_cardRow, false);
            go.GetComponent<RectTransform>().sizeDelta = CardView.Size;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = CardView.Size.x;
            le.preferredHeight = CardView.Size.y;
            _pool.Add(go.GetComponent<CardView>());
        }

        for (int i = 0; i < _pool.Count; i++)
        {
            bool active = i < cards.Length;
            _pool[i].gameObject.SetActive(active);
            if (active) _pool[i].Show(cards[i]);
        }
    }
}
