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
        public const float Width = 150f;
        public const float Height = 30f;

        [SerializeField] private PixelText lineOne;
        [SerializeField] private PixelText lineTwo;
        [SerializeField] private GameObject root;

        private float _hideAt;

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
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void Show(DamageRollInfo info)
        {
            var lines = DescribeCompact(info).Split('\n');
            lineOne.text = lines[0];
            lineTwo.text = lines.Length > 1 ? lines[1] : "";
            root.SetActive(true);
            _hideAt = Time.unscaledTime + HoldSeconds;
        }

        private void Update()
        {
            if (root.activeSelf && Time.unscaledTime >= _hideAt)
            {
                Hide();
            }
        }

        private static string Subject(DamageRollInfo info)
        {
            return info.spellName ?? "Attack";
        }

        /// <summary>
        /// A short two-line version of <see cref="Describe"/> that fits the 140 px readout, e.g.
        /// "D12 9+10=19 VS AC13" / "HIT D8+2 = 7 DMG".
        /// </summary>
        public static string DescribeCompact(DamageRollInfo info)
        {
            return CompactText(info).ToUpperInvariant();
        }

        private static string CompactText(DamageRollInfo info)
        {
            if (info.autoHit)
            {
                return $"{Subject(info)}\nAUTO-HIT {info.damageText} = {info.damage} DMG";
            }

            if (info.attackRoll > 0)
            {
                string line1 = $"D12 {info.attackRoll}+{info.attackBonus}={info.attackRoll + info.attackBonus} VS AC{info.targetArmorClass}";
                if (!info.hit)
                {
                    return line1 + (info.naturalOne ? "\nMISS (NAT 1)" : "\nMISS");
                }

                string nat = info.naturalTwelve ? " NAT 12!" : "";
                string acid = info.corrosionPenalty > 0 ? $" -{info.corrosionPenalty}" : "";
                return line1 + $"\nHIT{nat} {info.damageText}{acid} = {info.damage} DMG";
            }

            if (info.hasSave)
            {
                string line1 = $"{Subject(info)}: SAVE {info.saveRoll}+{info.saveBonus}={info.saveRoll + info.saveBonus}";
                if (!info.savePassed)
                {
                    return line1 + (info.damage > 0 ? $"\nFAILED {info.damageText} = {info.damage} DMG" : "\nFAILED");
                }

                return line1 + (info.halved ? $"\nSAVED HALF = {info.damage} DMG" : "\nRESISTED");
            }

            return $"{Subject(info)}\n{info.damage} DMG";
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
