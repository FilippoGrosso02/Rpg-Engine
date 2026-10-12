using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// A single always-visible card showing whoever's turn it currently is, ally or enemy —
    /// name, role/range blurb, HP, ATK/DEF, and the Fear meter. Replaces the old per-ally
    /// PartyStatusPanel roster list to match the Duelwright Table reference's single "ACTING"
    /// card; the tradeoff (other allies' HP isn't visible until their own turn) was a
    /// deliberate choice, not an oversight.
    /// ATK/DEF shown here are base stats, not fear/status-adjusted effective values — those
    /// are computed in Combat.TrailsBattle3D, which this assembly is never allowed to
    /// reference (see the asmdef boundary note on BattleHudRoot).
    /// </summary>
    public class ActingUnitCard : MonoBehaviour
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

        private CombatantRuntime _displayed;

        /// <summary>Exposed for the HUD PlayMode test to verify the render without reaching into UI internals.</summary>
        public int DisplayedHp { get; private set; }

        public void Clear()
        {
            _displayed = null;
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void SetActingCombatant(CombatantRuntime combatant)
        {
            _displayed = combatant;

            if (root != null)
            {
                root.SetActive(combatant != null);
            }

            if (combatant != null)
            {
                Refresh(combatant);
            }
        }

        /// <summary>Only re-renders if the changed combatant is the one currently displayed —
        /// e.g. an enemy taking damage on an ally's turn shouldn't touch this card.</summary>
        public void RefreshIfDisplayed(CombatantRuntime combatant)
        {
            if (combatant == _displayed)
            {
                Refresh(combatant);
            }
        }

        private void Refresh(CombatantRuntime combatant)
        {
            if (nameLabel != null)
            {
                nameLabel.text = combatant.displayName;
            }

            if (roleLabel != null)
            {
                var rangeWord = combatant.attackRangeMultiplier <= 1 ? "melee" : "ranged";
                roleLabel.text = string.IsNullOrEmpty(combatant.roleLabel)
                    ? rangeWord
                    : $"{combatant.roleLabel} · {rangeWord}";
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

            DisplayedHp = combatant.currentHp;
        }
    }
}
