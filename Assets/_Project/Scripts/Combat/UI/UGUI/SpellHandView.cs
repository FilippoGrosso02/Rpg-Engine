using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// The spell hand: a carousel of 50x70 cards whose centres are 32 px apart, the selected card always at x = 240 and lifted 6 px.
    /// At rest (main menu) the hand is lowered 12 px with every card face-up and none selected; opening the spell list raises it.
    /// At most five cards show (the selection plus two each side); with more spells the window follows the selection and wraps.
    /// Cards slide to their new positions over about 0.12 s and always land on whole pixels.
    /// </summary>
    public class SpellHandView : MonoBehaviour
    {
        private const float CardWidth = 50f;
        private const float CardHeight = 70f;
        private const float SelectedWidth = 58f;
        private const float SelectedHeight = 76f;
        private const float CentreX = 240f;
        // The hand is drawn at 80% size. Resting cards end at BottomY, just above the hit badge (top at y = 221) that sits over the
        // damage box / description at the bottom centre.
        private const float Scale = 0.8f;
        private const float BottomY = 219f;
        private const float LiftSelected = 6f;
        private const float RestingDrop = 12f;
        private const float Step = 26f; // centre-to-centre distance on screen (32 at full size)
        private const float SlideSpeed = 520f; // canvas units per second: one 32 px step takes about 0.06-0.12 s

        private class Slot
        {
            public RectTransform root;
            public Image image;
            public PixelText cost;
            public Vector2 current;
            public Vector2 target;
            public bool visible;
        }

        [SerializeField] private BattleUiSprites sprites;

        private readonly Dictionary<int, Slot> _slots = new Dictionary<int, Slot>();
        private IReadOnlyList<MenuEntry> _entries = new List<MenuEntry>();

        public void Build(BattleUiSprites spriteSource)
        {
            sprites = spriteSource;
        }

        /// <summary>Describes the hand: which cards exist, which is selected, and whether the hand is raised (spell list open).</summary>
        public void Show(IReadOnlyList<MenuEntry> entries, int selectedIndex, bool raised)
        {
            _entries = entries ?? new List<MenuEntry>();
            int count = _entries.Count;
            var visibleOffsets = new Dictionary<int, int>(); // card index -> offset from the selection

            foreach (int offset in OffsetsFor(count))
            {
                int index = ((selectedIndex + offset) % count + count) % count;
                if (!visibleOffsets.ContainsKey(index))
                {
                    visibleOffsets[index] = offset;
                }
            }

            foreach (var pair in _slots)
            {
                if (!visibleOffsets.ContainsKey(pair.Key))
                {
                    pair.Value.visible = false;
                    pair.Value.root.gameObject.SetActive(false);
                }
            }

            foreach (var pair in visibleOffsets)
            {
                int index = pair.Key;
                int offset = pair.Value;
                var slot = GetSlot(index);
                var entry = _entries[index];
                bool selected = raised && offset == 0;

                Style(slot, entry, selected);

                float width = selected ? SelectedWidth : CardWidth;
                float height = selected ? SelectedHeight : CardHeight;
                float top = BottomY - height * Scale + (raised ? 0f : RestingDrop) - (selected ? LiftSelected : 0f);
                slot.root.sizeDelta = new Vector2(width, height);
                slot.root.localScale = new Vector3(Scale, Scale, 1f);
                slot.target = new Vector2(Mathf.Round(CentreX + offset * Step - width * Scale * 0.5f), Mathf.Round(top));

                if (!slot.visible)
                {
                    slot.current = slot.target; // newly visible cards appear in place; only cards that stay slide
                    slot.visible = true;
                    slot.root.gameObject.SetActive(true);
                }

                if (selected)
                {
                    slot.root.SetAsLastSibling();
                }
            }

            ApplyPositions();
        }

        public void Hide()
        {
            foreach (var slot in _slots.Values)
            {
                slot.visible = false;
                slot.root.gameObject.SetActive(false);
            }
        }

        /// <summary>The offsets from the selected card that are drawn for a hand of <paramref name="count"/> cards (at most five).</summary>
        public static int[] OffsetsFor(int count)
        {
            switch (count)
            {
                case 0: return new int[0];
                case 1: return new[] { 0 };
                case 2: return new[] { 0, 1 };
                case 3: return new[] { -1, 0, 1 };
                case 4: return new[] { -1, 0, 1, 2 };
                default: return new[] { -2, -1, 0, 1, 2 };
            }
        }

        private void Update()
        {
            bool moved = false;
            foreach (var slot in _slots.Values)
            {
                if (slot.visible && slot.current != slot.target)
                {
                    slot.current = Vector2.MoveTowards(slot.current, slot.target, SlideSpeed * Time.unscaledDeltaTime);
                    moved = true;
                }
            }

            if (moved)
            {
                ApplyPositions();
            }
        }

        private void ApplyPositions()
        {
            foreach (var slot in _slots.Values)
            {
                if (slot.visible)
                {
                    slot.root.anchoredPosition = new Vector2(Mathf.Round(slot.current.x), -Mathf.Round(slot.current.y));
                }
            }
        }

        private Slot GetSlot(int index)
        {
            if (_slots.TryGetValue(index, out var existing))
            {
                return existing;
            }

            var rect = UiFactory.Rect(transform, "Card" + index, 0, 0, CardWidth, CardHeight);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.type = Image.Type.Simple;

            var cost = UiFactory.Text(rect, "Cost", "", 0, 0, 14, 8, PixelText.Align.Center, UiFactory.Ink, shadow: false);

            var slot = new Slot { root = rect, image = image, cost = cost };
            _slots[index] = slot;
            return slot;
        }

        private void Style(Slot slot, MenuEntry entry, bool selected)
        {
            string spriteName = selected ? "card_spell_selected" : entry.enabled ? "card_spell_face" : "card_spell_disabled";
            slot.image.sprite = sprites.Get(spriteName);
            // A selected card the player cannot afford still reads as selected, just greyed.
            slot.image.color = selected && !entry.enabled ? new Color(0.65f, 0.65f, 0.65f, 1f) : Color.white;

            slot.cost.text = entry.cost.ToString();
            // The cost sits in the corner diamond: its centre is (7, 7) on a normal card and (11, 10) on the selected one.
            var rect = slot.cost.rectTransform;
            if (selected)
            {
                UiFactory.Place(rect, 4, 6, 14, 8);
            }
            else
            {
                UiFactory.Place(rect, 0, 3, 14, 8);
            }
        }
    }
}
