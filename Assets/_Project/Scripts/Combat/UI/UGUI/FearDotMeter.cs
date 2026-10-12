using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// A 10-dot Fear meter (one dot per point) instead of a plain fill bar — each dot is
    /// tier-colored by the Duelwright fear-penalty thresholds (1-3 safe, 4-6 -1 ATK/DEF,
    /// 7-9 -2 ATK/DEF, 10 freeze) so the bands are visible even before fear reaches them,
    /// with only the dots up to the current fear value actually lit.
    /// </summary>
    public class FearDotMeter : MonoBehaviour
    {
        [SerializeField] private Image[] dots;

        private static readonly Color EmptyColor = new Color(0.25f, 0.25f, 0.28f);
        private static readonly Color SafeColor = new Color(0.78f, 0.72f, 0.5f);
        private static readonly Color WarningColor = new Color(0.85f, 0.62f, 0.2f);
        private static readonly Color DangerColor = new Color(0.82f, 0.32f, 0.22f);
        private static readonly Color FreezeColor = new Color(0.55f, 0.12f, 0.12f);

        public void SetFear(int fear)
        {
            if (dots == null)
            {
                return;
            }

            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null)
                {
                    continue;
                }

                int dotValue = i + 1;
                dots[i].color = dotValue <= fear ? ColorForTier(dotValue) : EmptyColor;
            }
        }

        private static Color ColorForTier(int dotValue)
        {
            if (dotValue >= 10) return FreezeColor;
            if (dotValue >= 7) return DangerColor;
            if (dotValue >= 4) return WarningColor;
            return SafeColor;
        }
    }
}
