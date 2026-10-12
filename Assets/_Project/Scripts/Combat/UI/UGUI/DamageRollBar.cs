using System.Collections;
using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// A bar showing the damage formula for the most recent hit — "ATK x + Roll y - DEF z =
    /// damage" — with the roll digit cycling rapidly through random faces before settling on
    /// the real value, like a rolling die. Fires off OnDamageRolled; overlapping hits (e.g. an
    /// AOE skill hitting several targets) restart the animation on the latest one rather than
    /// queuing, since this is a glanceable readout, not a combat log.
    /// </summary>
    public class DamageRollBar : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text formulaLabel;
        [SerializeField] private Text rollDigitLabel;
        [SerializeField] private float rollAnimationSeconds = 0.6f;
        [SerializeField] private float holdSeconds = 1.6f;

        private Coroutine _activeRoutine;
        private static readonly System.Random RollRng = new System.Random();

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }
        }

        public void Show(DamageRollInfo info)
        {
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }

            // Must activate before StartCoroutine — root is this component's own GameObject,
            // and Unity refuses to start a coroutine on an inactive GameObject at all (throws
            // immediately, not just skips execution until active).
            if (root != null)
            {
                root.SetActive(true);
            }

            _activeRoutine = StartCoroutine(PlayRoutine(info));
        }

        private IEnumerator PlayRoutine(DamageRollInfo info)
        {
            var defText = info.defIgnored ? "DEF ignored" : $"DEF {info.effectiveDef}";
            if (formulaLabel != null)
            {
                formulaLabel.text = $"ATK {info.effectiveAtk}  +  Roll  −  {defText}  =  ?";
            }

            bool hasRoll = info.diceRoll > 0;
            float elapsed = 0f;
            while (elapsed < rollAnimationSeconds)
            {
                if (rollDigitLabel != null)
                {
                    rollDigitLabel.text = hasRoll ? RollRng.Next(1, 7).ToString() : "—";
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (rollDigitLabel != null)
            {
                rollDigitLabel.text = hasRoll ? info.diceRoll.ToString() : "—";
            }

            if (formulaLabel != null)
            {
                var critText = info.wasCrit ? "  CRIT!" : "";
                var rollText = hasRoll ? info.diceRoll.ToString() : "—";
                formulaLabel.text = $"ATK {info.effectiveAtk}  +  Roll {rollText}  −  {defText}  =  {info.damage} dmg{critText}";
            }

            yield return new WaitForSeconds(holdSeconds);

            Hide();
        }
    }
}
