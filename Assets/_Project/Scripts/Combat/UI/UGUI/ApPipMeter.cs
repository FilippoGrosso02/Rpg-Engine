using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// A caster's spell points as a row of pips (`pip_full` for points left, `pip_empty` for spent room). Only built for the hero card;
    /// the HUD hides it for heroes who do not use AP.
    /// </summary>
    public class ApPipMeter : MonoBehaviour
    {
        public const int MaxPips = 10;
        private const float PipSize = 13f;
        private const float PipStep = 14f;

        [SerializeField] private Image[] dots = new Image[0];
        [SerializeField] private Sprite fullSprite;
        [SerializeField] private Sprite emptySprite;

        public void Build(BattleUiSprites sprites)
        {
            fullSprite = sprites.Get("pip_full");
            emptySprite = sprites.Get("pip_empty");
            dots = new Image[MaxPips];
            for (int i = 0; i < MaxPips; i++)
            {
                dots[i] = UiFactory.Image(transform, "Pip" + (i + 1), emptySprite, i * PipStep, 0, PipSize, PipSize);
            }
        }

        /// <summary>Lights the first <paramref name="lit"/> pips and shows <paramref name="capacity"/> in total.</summary>
        public void SetPips(int lit, int capacity)
        {
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null)
                {
                    continue;
                }

                dots[i].gameObject.SetActive(i < capacity);
                dots[i].sprite = i < lit ? fullSprite : emptySprite;
            }
        }
    }
}
