using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// The hit-chance badge (56x16, centred over the enemy card): the ONLY place the hit percentage is shown. Shown while the reticle is
    /// on an enemy. 75% and up is the green badge, 25-74% the cream one, below 25% the red one; the percentage is drawn in matching colour.
    /// </summary>
    public class HitBadgeView : MonoBehaviour
    {
        public const int HighThreshold = 75;
        public const int LowThreshold = 25;

        [SerializeField] private BattleUiSprites sprites;
        [SerializeField] private Image frame;
        [SerializeField] private PixelText percent;

        public void Build(BattleUiSprites spriteSource)
        {
            sprites = spriteSource;
            frame = GetComponent<Image>();
            if (frame == null)
            {
                frame = gameObject.AddComponent<Image>();
            }

            frame.raycastTarget = false;
            percent = UiFactory.Text(transform, "Percent", "", 20, 4, 32, 8, PixelText.Align.Left, UiFactory.Cream);
            Hide();
        }

        /// <summary>Which badge (high / neutral / low) a chance uses.</summary>
        public static string SpriteNameFor(int chancePercent)
        {
            return chancePercent >= HighThreshold ? "tag_hit_chance_high"
                : chancePercent >= LowThreshold ? "tag_hit_chance_neutral"
                : "tag_hit_chance_low";
        }

        public void Show(int chancePercent)
        {
            gameObject.SetActive(true);
            frame.sprite = sprites.Get(SpriteNameFor(chancePercent));
            percent.text = chancePercent + "%";
            percent.color = chancePercent >= HighThreshold ? UiFactory.Heal
                : chancePercent >= LowThreshold ? UiFactory.Cream
                : UiFactory.Warning;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
