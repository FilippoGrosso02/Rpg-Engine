using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    public class TurnOrderIcon : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Image background;
        [SerializeField] private Color allyColor = new Color(0.35f, 0.55f, 0.95f);
        [SerializeField] private Color enemyColor = new Color(0.85f, 0.35f, 0.35f);

        public void Refresh(CombatantRuntime combatant)
        {
            if (label != null)
            {
                label.text = combatant.displayName;
            }

            if (background != null)
            {
                background.color = combatant.isPlayerSide ? allyColor : enemyColor;
            }
        }
    }
}
