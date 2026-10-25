using System.Collections.Generic;
using Game.Combat.UI.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Overworld.Menu
{
    /// <summary>One frame's worth of menu input, already mapped from the keyboard (W/S/A/D or arrows, Enter/Space, Esc).</summary>
    public struct MenuInput
    {
        public int moveX;
        public int moveY;
        public bool confirm;
    }

    /// <summary>A tab of the overworld menu. The controller handles opening, closing and switching tabs; a tab only handles what happens inside it.</summary>
    public abstract class MenuTab
    {
        public RectTransform Root { get; private set; }

        public abstract string Title { get; }

        public void Attach(RectTransform content)
        {
            Root = UiFactory.Rect(content, Title, 0, 0, content.sizeDelta.x, content.sizeDelta.y);
            Build(Root);
        }

        protected abstract void Build(RectTransform root);

        /// <summary>The tab was just shown (or something it displays changed): re-read the game state and redraw.</summary>
        public abstract void Refresh();

        public abstract void Handle(MenuInput input);

        /// <summary>Esc was pressed. Return true if the tab used it to step back inside itself; false lets the menu close.</summary>
        public virtual bool HandleBack() => false;

        /// <summary>Called when the menu closes or another tab is chosen, so a half-finished choice does not linger.</summary>
        public virtual void Reset() { }
    }

    /// <summary>A vertical list of text rows with a selection bar, scrolling when it has more entries than rows.</summary>
    public sealed class MenuList
    {
        public const float RowHeight = 11f;

        static readonly Color BarFocused = new Color(UiFactory.Gold.r, UiFactory.Gold.g, UiFactory.Gold.b, 0.55f);
        static readonly Color BarUnfocused = new Color(UiFactory.KeyBorder.r, UiFactory.KeyBorder.g, UiFactory.KeyBorder.b, 0.45f);

        readonly Image[] _bars;
        readonly PixelText[] _rows;
        readonly int _visible;
        readonly int _maxChars;
        List<string> _labels = new List<string>();
        int _top;

        public int Index { get; private set; }

        public int Count => _labels.Count;

        /// <summary>A focused list shows a bright bar; an unfocused one (the user is working in another column) a dim one.</summary>
        public bool Focused { get; set; } = true;

        public string Selected => Index >= 0 && Index < _labels.Count ? _labels[Index] : null;

        public MenuList(RectTransform parent, float x, float y, float width, int visibleRows)
        {
            _visible = visibleRows;
            _maxChars = Mathf.FloorToInt((width - 8f) / PixelFont.Advance);
            _bars = new Image[visibleRows];
            _rows = new PixelText[visibleRows];

            for (int i = 0; i < visibleRows; i++)
            {
                _bars[i] = UiFactory.Box(parent, "Bar" + i, BarFocused, x, y + i * RowHeight, width, RowHeight - 1f);
                _rows[i] = UiFactory.Text(parent, "Row" + i, "", x + 4f, y + i * RowHeight + 1f, width - 8f, 8f, PixelText.Align.Left, UiFactory.Cream);
            }
        }

        public void SetItems(IList<string> labels, bool keepIndex = false)
        {
            _labels = new List<string>(labels ?? new List<string>());
            Index = keepIndex ? Mathf.Clamp(Index, 0, Mathf.Max(0, _labels.Count - 1)) : 0;
            _top = 0;
            Redraw();
        }

        /// <summary>Moves the selection by <paramref name="step"/> rows, wrapping at the ends.</summary>
        public bool Move(int step)
        {
            if (_labels.Count == 0 || step == 0) return false;

            Index = ((Index + step) % _labels.Count + _labels.Count) % _labels.Count;
            Redraw();
            return true;
        }

        public void Redraw()
        {
            if (Index < _top) _top = Index;
            if (Index >= _top + _visible) _top = Index - _visible + 1;

            for (int i = 0; i < _visible; i++)
            {
                int entry = _top + i;
                bool exists = entry < _labels.Count;
                _rows[i].text = exists ? MenuText.Fit(_labels[entry], _maxChars) : "";
                _bars[i].enabled = exists && entry == Index;
                _bars[i].color = Focused ? BarFocused : BarUnfocused;
            }
        }
    }
}
