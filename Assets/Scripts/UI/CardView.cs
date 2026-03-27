using UnityEngine;
using UnityEngine.UI;

/// One card: a colored Image background with a rank+suit Text overlay.
public class CardView : MonoBehaviour
{
    public static readonly Vector2 Size = new Vector2(60f, 90f);

    private Image _bg;
    private Text  _label;

    private void Awake()
    {
        _bg        = GetComponent<Image>();
        _bg.sprite = CardVisuals.SharedSprite;
        _bg.color  = CardVisuals.BackColor;

        var labelGO = new GameObject("Lbl", typeof(RectTransform), typeof(Text));
        labelGO.transform.SetParent(transform, false);
        var rt = labelGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _label           = labelGO.GetComponent<Text>();
        _label.font      = CardVisuals.DefaultFont;
        _label.fontSize  = 13;
        _label.alignment = TextAnchor.MiddleCenter;
    }

    public void Show(int cardIndex)
    {
        _bg.color    = CardVisuals.BgColor(cardIndex);
        _label.color = CardVisuals.TextColor(cardIndex);
        _label.text  = CardVisuals.Label(cardIndex);
    }

    public void ShowBack()
    {
        _bg.color   = CardVisuals.BackColor;
        _label.text = string.Empty;
    }
}
