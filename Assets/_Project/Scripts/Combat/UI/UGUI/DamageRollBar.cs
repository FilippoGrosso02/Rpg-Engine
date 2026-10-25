using System.Collections;
using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// The damage calculation box at the bottom centre: a two-line readout of how the most recent attack or spell resolved — the d12
    /// attack roll against AC, the damage dice, any save. It appears after each attack and hides itself after a few seconds;
    /// overlapping results (a Fireball hitting several targets) just replace it.
    /// </summary>
    public class DamageRollBar : MonoBehaviour
    {
        public const float HoldSeconds = 3.5f;
        private const float RollSeconds = 0.7f;
        private const float RollTick = 0.05f;
        private const float SettleSeconds = 0.4f;
        private const float SumSeconds = 0.6f;
        public const float Width = 150f;
        public const float Height = 30f;

        [SerializeField] private PixelText lineOne;
        [SerializeField] private PixelText lineTwo;
        [SerializeField] private GameObject root;

        private float _hideAt;
        private bool _previewing;
        private bool _animating;
        private Coroutine _animation;
        private System.Action _onSettled;

        public void Build(BattleUiSprites sprites)
        {
            root = gameObject;
            var panel = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            panel.sprite = sprites.Get("panel_dark");
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = false;

            lineOne = UiFactory.Text(transform, "LineOne", "", 8, 6, Width - 16, 8, PixelText.Align.Center, UiFactory.Cream);
            lineTwo = UiFactory.Text(transform, "LineTwo", "", 8, 16, Width - 16, 8, PixelText.Align.Center, UiFactory.Damage);
            Hide();
        }

        public void Hide()
        {
            CancelAnimation();
            _previewing = false;
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        /// <summary>Stops a running dice animation; whatever waited for its final number gets it now.</summary>
        private void CancelAnimation()
        {
            if (_animation != null)
            {
                StopCoroutine(_animation);
                _animation = null;
            }

            _animating = false;
            Settle();
        }

        /// <summary>
        /// The outcome of an attack, as a number only: the dice tumble and settle, the modifier is added, and only then the final amount
        /// appears (staying up for <see cref="HoldSeconds"/>). <paramref name="onSettled"/> fires when the final amount is shown, or at once
        /// if a newer result interrupts this one, so whatever waits for the number is never dropped.
        /// </summary>
        public void Show(DamageRollInfo info, System.Action onSettled = null)
        {
            CancelAnimation();
            _onSettled = onSettled;
            lineTwo.text = "";
            PlaceLines(centred: true);
            _previewing = false;
            root.SetActive(true);
            _animation = StartCoroutine(Animate(info));
        }

        /// <summary>Before the player commits: just the damage dice plus modifier of the aimed action, e.g. "D8+2". Stays up until hidden.</summary>
        public void ShowPreview(string damageText)
        {
            CancelAnimation();
            lineOne.color = UiFactory.Cream;
            lineOne.text = string.IsNullOrEmpty(damageText) ? "0" : damageText.ToUpperInvariant();
            lineTwo.text = "";
            PlaceLines(centred: true);
            root.SetActive(true);
            _previewing = true;
        }

        /// <summary>Hides the aim preview; a damage result that is rolling or still being read is left alone.</summary>
        public void HidePreview()
        {
            if (_previewing)
            {
                Hide();
            }
        }

        private void PlaceLines(bool centred)
        {
            UiFactory.Place(lineOne.rectTransform, 8, centred ? 11 : 6, Width - 16, 8);
        }

        private void Update()
        {
            if (root.activeSelf && !_previewing && !_animating && Time.unscaledTime >= _hideAt)
            {
                Hide();
            }
        }

        /// <summary>The final readout: just the damage number (or MISS / RESISTED when nothing was dealt). No attack name, no wording.</summary>
        public static string DescribeCompact(DamageRollInfo info)
        {
            if (!info.hit)
            {
                return "MISS";
            }

            return info.effectNegated ? "RESISTED" : info.damage.ToString();
        }

        /// <summary>The dice plus modifier as they add up, e.g. "5+2" or "5-1" (acid corrosion folded into the modifier).</summary>
        public static string SumText(DamageRollInfo info)
        {
            int modifier = info.damageBonus - info.corrosionPenalty;
            return modifier == 0 ? info.diceTotal.ToString()
                : modifier > 0 ? $"{info.diceTotal}+{modifier}"
                : $"{info.diceTotal}{modifier}";
        }

        private IEnumerator Animate(DamageRollInfo info)
        {
            _animating = true;
            bool rolls = info.hit && !info.effectNegated && info.diceCount > 0;

            if (rolls)
            {
                // 1. The dice tumble: the number fluctuates, then settles on what was rolled.
                lineOne.color = UiFactory.Cream;
                float end = Time.unscaledTime + RollSeconds;
                while (Time.unscaledTime < end)
                {
                    lineOne.text = Random.Range(info.diceCount, info.diceCount * info.diceSides + 1).ToString();
                    yield return new WaitForSecondsRealtime(RollTick);
                }

                lineOne.text = info.diceTotal.ToString();
                yield return new WaitForSecondsRealtime(SettleSeconds);

                // 2. The modifier is added to the dice.
                if (info.damageBonus - info.corrosionPenalty != 0)
                {
                    lineOne.text = SumText(info);
                    yield return new WaitForSecondsRealtime(SumSeconds);
                }
            }

            // 3. Only now the final amount.
            lineOne.color = UiFactory.Damage;
            lineOne.text = DescribeCompact(info);
            _animating = false;
            _hideAt = Time.unscaledTime + HoldSeconds;
            Settle();
        }

        private void Settle()
        {
            var done = _onSettled;
            _onSettled = null;
            done?.Invoke();
        }

        private static string Subject(DamageRollInfo info)
        {
            return info.spellName ?? "Attack";
        }

        /// <summary>One-line readout with the full wording; kept for tests and a future combat log.</summary>
        public static string Describe(DamageRollInfo info)
        {
            var subject = Subject(info);

            if (info.autoHit)
            {
                return $"{subject}  auto-hit  {info.damageText} = {info.damage} dmg";
            }

            if (info.attackRoll > 0)
            {
                string total = $"d12 {info.attackRoll} + {info.attackBonus} = {info.attackRoll + info.attackBonus} vs AC {info.targetArmorClass}";
                if (!info.hit)
                {
                    string why = info.naturalOne ? " (natural 1)" : "";
                    return $"{subject}  {total} — MISS{why}";
                }

                string acid = info.corrosionPenalty > 0 ? $" (−{info.corrosionPenalty} corroded)" : "";
                string nat = info.naturalTwelve ? " natural 12!" : "";
                return $"{subject}  {total} — HIT{nat}  {info.damageText}{acid} = {info.damage} dmg";
            }

            if (info.hasSave)
            {
                string save = $"save d12 {info.saveRoll} + {info.saveBonus} = {info.saveRoll + info.saveBonus} vs 12";
                string damage = info.damageText != null && info.damageText != "0" ? $"{info.damageText}: " : "";

                if (!info.savePassed)
                {
                    return info.damage > 0
                        ? $"{subject}  {save} — FAILED  {damage}{info.damage} dmg"
                        : $"{subject}  {save} — FAILED";
                }

                return info.halved
                    ? $"{subject}  {save} — SAVED, half  {info.damage} dmg"
                    : $"{subject}  {save} — RESISTED";
            }

            return $"{subject}  {info.damage} dmg";
        }
    }
}
