using UnityEngine;
using UnityEngine.UI;

public class DeckView : MonoBehaviour
{
    private readonly Image[] _backs = new Image[3];
    private Text _countLabel;

    private void Awake()
    {
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject($"Back{i}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta      = CardView.Size;
            rt.anchoredPosition = new Vector2(i * 3f, -(i * 3f));
            _backs[i]        = go.GetComponent<Image>();
            _backs[i].sprite = CardVisuals.SharedSprite;
            _backs[i].color  = CardVisuals.BackColor;
        }

        var lblGO = new GameObject("Count", typeof(RectTransform), typeof(Text));
        lblGO.transform.SetParent(transform, false);
        var lrt = lblGO.GetComponent<RectTransform>();
        lrt.anchoredPosition = new Vector2(4f, -CardView.Size.y * 0.5f - 16f);
        lrt.sizeDelta        = new Vector2(90f, 22f);
        _countLabel          = lblGO.GetComponent<Text>();
        _countLabel.font      = CardVisuals.DefaultFont;
        _countLabel.fontSize  = 13;
        _countLabel.alignment = TextAnchor.MiddleCenter;
        _countLabel.color     = Color.white;
    }

    public void Refresh(int deckSize)
    {
        bool hasCards = deckSize > 0;
        foreach (var b in _backs) b.gameObject.SetActive(hasCards);
        _countLabel.text = hasCards ? $"Deck  {deckSize}" : "Empty";
    }
}
