using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// Screen-space "whose turn it is" banner over the battlefield — a text companion to the
    /// existing world-space ActiveTurnIndicator marker, not a replacement for it.
    /// </summary>
    public class TurnBanner : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text hintLabel;

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void Show(CombatantRuntime combatant)
        {
            if (nameLabel != null)
            {
                nameLabel.text = $"{combatant.displayName}'s move";
            }

            if (hintLabel != null)
            {
                var targetHint = combatant.attackRangeMultiplier <= 1 ? "strike an adjacent foe" : "strike a foe in range";
                hintLabel.text = $"{combatant.currentAp}/{combatant.maxAp} AP — {targetHint}";
            }

            if (root != null)
            {
                root.SetActive(true);
            }
        }
    }
}
