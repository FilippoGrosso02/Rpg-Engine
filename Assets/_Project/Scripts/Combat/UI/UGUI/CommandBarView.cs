using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// The four command circles (a 2x2 grid of 29 px slots beside the hero card): Q top-left, E top-right, R bottom-left, Tab bottom-right.
    /// Each shows an icon for what the key does in the current menu state, with the key cap hanging off the circle's lower-right edge.
    /// They are pure output — nothing here can be clicked.
    /// </summary>
    public class CommandBarView : MonoBehaviour
    {
        private enum Look
        {
            Normal,
            Active,
            Pressed,
            Disabled
        }

        // Relative to the hero corner group (its top-left), so the circles sit right of the 60x75 hero card, bottom-aligned with it.
        public const float OriginX = 62f;
        public const float OriginY = 13f;
        public const float Step = 33f;
        private const float SlotSize = 29f;
        private const string TextIconPrefix = "txt:";

        private static readonly MenuKey[] Keys = { MenuKey.Q, MenuKey.E, MenuKey.R, MenuKey.Tab };
        private static readonly string[] KeyLetters = { "Q", "E", "R", "TAB" };

        [SerializeField] private BattleUiSprites sprites;
        [SerializeField] private Image[] backgrounds = new Image[4];
        [SerializeField] private Image[] icons = new Image[4];
        [SerializeField] private PixelText[] textIcons = new PixelText[4];

        private BattleInputController _input;
        private readonly Look[] _base = new Look[4];
        private readonly bool[] _enabled = new bool[4];

        public void Build(BattleUiSprites spriteSource)
        {
            sprites = spriteSource;
            backgrounds = new Image[4];
            icons = new Image[4];
            textIcons = new PixelText[4];

            for (int i = 0; i < 4; i++)
            {
                float x = OriginX + (i % 2) * Step;
                float y = OriginY + (i / 2) * Step;
                var bg = UiFactory.Image(transform, "Command" + KeyLetters[i], sprites.Get("slot_ability_empty"), x, y, SlotSize, SlotSize);
                backgrounds[i] = bg;

                icons[i] = UiFactory.Image(bg.transform, "Icon", null, 8, 8, 13, 13);
                icons[i].preserveAspect = true;
                textIcons[i] = UiFactory.Text(bg.transform, "TextIcon", "", 0, 11, SlotSize, 8, PixelText.Align.Center, UiFactory.Cream);

                // Key cap on the lower-right of the circle: a 1 px border around a cream square, with the key inside.
                float capWidth = i == 3 ? 19f : 11f;
                var cap = UiFactory.Box(bg.transform, "CapBorder", UiFactory.KeyBorder, SlotSize - capWidth + 2, SlotSize - 9, capWidth, 11);
                UiFactory.Box(cap.transform, "CapFill", UiFactory.Cream, 1, 1, capWidth - 2, 9);
                UiFactory.Text(cap.transform, "Key", KeyLetters[i], 0, 2, capWidth, 8, PixelText.Align.Center, UiFactory.Ink, shadow: false);
            }
        }

        public void Bind(BattleInputController input)
        {
            _input = input;
        }

        /// <summary>What each key does per menu state (Q, E, R, Tab). Kept as words for tests and tooltips; the circles draw <see cref="IconsFor"/>.</summary>
        public static string[] LabelsFor(MenuState state)
        {
            switch (state)
            {
                case MenuState.Spells: return new[] { "PREV", "NEXT", "ITEMS", "FLEE" };
                case MenuState.Items: return new[] { "PREV", "NEXT", "BACK", "FLEE" };
                case MenuState.FleeConfirm: return new[] { "GUARD", "SPELLS", "ITEMS", "STAY" };
                default: return new[] { "GUARD", "SPELLS", "ITEMS", "FLEE" };
            }
        }

        /// <summary>The icon for each key per menu state: a sprite name, or "txt:" plus a glyph drawn in the pixel font.</summary>
        public static string[] IconsFor(MenuState state)
        {
            switch (state)
            {
                case MenuState.Spells: return new[] { TextIconPrefix + "<", TextIconPrefix + ">", "icon_heart", "icon_boot" };
                case MenuState.Items: return new[] { TextIconPrefix + "<", TextIconPrefix + ">", "glyph_x_red", "icon_boot" };
                case MenuState.FleeConfirm: return new[] { "icon_shield", "orb_small_red", "icon_heart", "glyph_x_red" };
                default: return new[] { "icon_shield", "orb_small_red", "icon_heart", "icon_boot" };
            }
        }

        /// <summary>Which of the four buttons can be used in <paramref name="state"/> (Q, E, R, Tab).</summary>
        public static bool[] EnabledFor(MenuState state, bool hasSpells, bool hasItems)
        {
            switch (state)
            {
                case MenuState.Root: return new[] { true, hasSpells, hasItems, true };
                case MenuState.Spells: return new[] { true, true, true, true };
                case MenuState.Items: return new[] { true, true, true, true };
                case MenuState.FleeConfirm: return new[] { false, false, false, true };
                default: return new[] { false, false, false, false };
            }
        }

        public void SetState(MenuState state, bool hasSpells, bool hasItems)
        {
            var iconNames = IconsFor(state);
            var enabled = EnabledFor(state, hasSpells, hasItems);

            for (int i = 0; i < 4; i++)
            {
                bool asText = iconNames[i].StartsWith(TextIconPrefix);
                icons[i].gameObject.SetActive(!asText);
                textIcons[i].gameObject.SetActive(asText);
                if (asText)
                {
                    textIcons[i].text = iconNames[i].Substring(TextIconPrefix.Length);
                }
                else
                {
                    icons[i].sprite = sprites.Get(iconNames[i]);
                }

                _enabled[i] = enabled[i];
                _base[i] = !enabled[i] ? Look.Disabled : Look.Normal;
            }

            // The circle for the open list is lit as the "mode active" highlight (E in SPELLS, R in ITEMS).
            if (state == MenuState.Spells) _base[1] = Look.Active;
            if (state == MenuState.Items) _base[2] = Look.Active;

            Apply();
        }

        private void Update()
        {
            if (_input != null)
            {
                Apply();
            }
        }

        private void Apply()
        {
            for (int i = 0; i < 4; i++)
            {
                var look = _base[i];
                if (_enabled[i] && _input != null && _input.IsHeld(Keys[i]))
                {
                    look = Look.Pressed;
                }

                var tint = look == Look.Disabled ? new Color(1f, 1f, 1f, 0.45f)
                    : look == Look.Pressed ? new Color(0.7f, 0.7f, 0.7f, 1f)
                    : look == Look.Active ? new Color(1f, 0.85f, 0.55f, 1f)
                    : Color.white;
                backgrounds[i].color = tint;
                icons[i].color = new Color(1f, 1f, 1f, look == Look.Disabled ? 0.45f : 1f);
                textIcons[i].color = new Color(UiFactory.Cream.r, UiFactory.Cream.g, UiFactory.Cream.b, look == Look.Disabled ? 0.45f : 1f);
            }
        }
    }
}
