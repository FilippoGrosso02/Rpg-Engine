using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>The parchment tooltip (140x26 at x = 170, y = 58) naming the selected spell or item, its cost and its effect.</summary>
    public class DetailTooltipView : MonoBehaviour
    {
        [SerializeField] private PixelText titleText;
        [SerializeField] private PixelText effectText;

        public void Build(BattleUiSprites sprites)
        {
            var panel = GetComponent<Image>();
            if (panel == null)
            {
                panel = gameObject.AddComponent<Image>();
            }

            panel.sprite = sprites.Get("panel_parchment");
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = false;

            titleText = UiFactory.Text(transform, "Title", "", 7, 4, 126, 8, PixelText.Align.Left, UiFactory.Ink, shadow: false);
            effectText = UiFactory.Text(transform, "Effect", "", 7, 14, 126, 8, PixelText.Align.Left, UiFactory.Ink, shadow: false);
        }

        public void Show(string title, string effect)
        {
            gameObject.SetActive(true);
            titleText.text = title;
            effectText.text = effect;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
