using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// Shows whichever combatant (ally or enemy) is currently under the cursor — name, role,
    /// HP, ATK/DEF, Fear — independent of whose turn it is. Structurally a near-twin of
    /// ActingUnitCard (same stat set), kept as a separate component since it's driven by a
    /// different event (OnHoverChanged, not OnTurnStarted) and can be visible at the same time
    /// as the acting card.
    /// </summary>
    public class HoverStatsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text roleLabel;
        [SerializeField] private Text hpValueLabel;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Text atkValueLabel;
        [SerializeField] private Text defValueLabel;
        [SerializeField] private Text fearValueLabel;
        [SerializeField] private FearDotMeter fearDotMeter;

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void SetHoveredCombatant(CombatantRuntime combatant)
        {
            if (combatant == null)
            {
                Hide();
                return;
            }

            if (root != null)
            {
                root.SetActive(true);
            }

            if (nameLabel != null)
            {
                nameLabel.text = combatant.displayName;
            }

            if (roleLabel != null)
            {
                var rangeWord = combatant.attackRangeMultiplier <= 1 ? "melee" : "ranged";
                var side = combatant.isPlayerSide ? "Ally" : "Enemy";
                roleLabel.text = string.IsNullOrEmpty(combatant.roleLabel)
                    ? $"{side} · {rangeWord}"
                    : $"{side} · {combatant.roleLabel} · {rangeWord}";
            }

            if (hpValueLabel != null)
            {
                hpValueLabel.text = $"{combatant.currentHp}/{combatant.maxHp}";
            }

            if (hpSlider != null && combatant.maxHp > 0)
            {
                hpSlider.value = (float)combatant.currentHp / combatant.maxHp;
            }

            if (atkValueLabel != null)
            {
                atkValueLabel.text = combatant.atk.ToString();
            }

            if (defValueLabel != null)
            {
                defValueLabel.text = combatant.def.ToString();
            }

            if (fearValueLabel != null)
            {
                fearValueLabel.text = $"{combatant.fear}/10";
            }

            fearDotMeter?.SetFear(combatant.fear);
        }
    }
}
