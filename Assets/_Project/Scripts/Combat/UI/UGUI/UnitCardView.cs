using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// A 60x75 unit card tucked into a bottom corner (the hero card bottom-left, the aimed-at card bottom-right; the sheet's 72x90 art is
    /// drawn at 5/6 size). Layers, bottom to top: card frame, the HP number on the orb, the offence stat on the left (STR for a melee
    /// fighter, DEX for a ranged one) under a sword or boot icon, the armor class on the right under a shield, and a one-line name caption
    /// above the card.
    /// </summary>
    public class UnitCardView : MonoBehaviour
    {
        public const float Scale = 5f / 6f;
        public const float Width = 60f;
        public const float Height = 75f;

        [SerializeField] private BattleUiSprites sprites;
        [SerializeField] private Image frame;
        [SerializeField] private PixelText hpText;
        [SerializeField] private PixelText offenceText;
        [SerializeField] private PixelText armorText;
        [SerializeField] private PixelText captionText;
        [SerializeField] private Image offenceIcon;

        private CombatantRuntime _shown;

        public CombatantRuntime Shown => _shown;

        /// <summary>The HP number currently drawn on the orb (exposed so the HUD PlayMode test can verify the render).</summary>
        public int DisplayedHp { get; private set; }

        private static float S(float v) => Mathf.Round(v * Scale);

        public void Build(BattleUiSprites spriteSource, bool enemy)
        {
            sprites = spriteSource;
            frame = UiFactory.Image(transform, "Frame", sprites.Get(enemy ? "card_unit_enemy" : "card_unit_ally"), 0, 0, Width, Height);

            // The frame art carries no icons (they were painted out of the sheet), so both are drawn here: the offence icon on the left
            // (sword for STR, boot for DEX) and a shield on the right, each with its number underneath.
            offenceIcon = UiFactory.Image(transform, "OffenceIcon", sprites.Get("icon_sword"), S(10), S(58), S(11), S(12));
            offenceIcon.preserveAspect = true;
            var shield = UiFactory.Image(transform, "ArmorIcon", sprites.Get("icon_shield"), S(51), S(58), S(11), S(12));
            shield.preserveAspect = true;

            // The orb is centred at (30, 61) with a radius of about 8.
            hpText = UiFactory.Text(transform, "Hp", "", 15, 57, 30, 8, PixelText.Align.Center, UiFactory.Cream);
            offenceText = UiFactory.Text(transform, "Offence", "", S(4), S(71), S(24), 8, PixelText.Align.Center, UiFactory.Ink, shadow: false);
            armorText = UiFactory.Text(transform, "Armor", "", S(45), S(71), S(24), 8, PixelText.Align.Center, UiFactory.Ink, shadow: false);
            captionText = UiFactory.Text(transform, "Caption", "", -2, -10, Width + 4, 8, PixelText.Align.Center, UiFactory.Cream);
        }

        public void Show(CombatantRuntime combatant)
        {
            _shown = combatant;
            gameObject.SetActive(combatant != null);
            if (combatant != null)
            {
                frame.sprite = sprites.Get(combatant.isPlayerSide ? "card_unit_ally" : "card_unit_enemy");
                Refresh();
            }
        }

        public void Refresh()
        {
            if (_shown == null)
            {
                return;
            }

            DisplayedHp = Mathf.Max(0, _shown.currentHp);
            hpText.text = DisplayedHp.ToString();
            offenceText.text = OffenceStat(_shown, out bool dexterity).ToString();
            offenceIcon.sprite = sprites.Get(dexterity ? "icon_boot" : "icon_sword");
            armorText.text = _shown.ArmorClass.ToString();
            captionText.text = Shorten(_shown.displayName, 9);
        }

        /// <summary>
        /// The number shown on the card's left: the combatant's ATK (ability score plus weapon modifier for a hero, the stat block's ATK
        /// for a monster). The icon says which stat it comes from: boot for a ranged attacker (DEX), sword for a melee one.
        /// </summary>
        public static int OffenceStat(CombatantRuntime combatant, out bool dexterity)
        {
            dexterity = combatant.attackRangeMultiplier > 1;
            return combatant.attackBonus;
        }

        /// <summary>The card rests slightly desaturated and lights up while the reticle is on it.</summary>
        public void SetEmphasis(bool emphasised)
        {
            frame.color = emphasised ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        }

        private static string Shorten(string name, int max)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }

            return name.Length <= max ? name : name.Substring(0, max);
        }
    }
}
