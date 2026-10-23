using System;
using System.Collections.Generic;

namespace Game.Combat.UI.UGUI
{
    public enum MenuState
    {
        /// <summary>The main menu (nothing selected).</summary>
        Root,
        Spells,
        Items,
        /// <summary>The "Escape from combat?" modal.</summary>
        FleeConfirm,
        /// <summary>Enemy turn or an animation: all input ignored.</summary>
        Locked
    }

    public enum MenuKey
    {
        Q,
        E,
        R,
        Tab,
        Escape,
        /// <summary>Left mouse button or Space.</summary>
        Commit,
        Enter,
        /// <summary>Right mouse button.</summary>
        Back
    }

    /// <summary>One selectable spell or item.</summary>
    public class MenuEntry
    {
        public string id;
        public string displayName;
        public int cost;
        /// <summary>False when it cannot be used right now (not enough AP). Still selectable so it can be read.</summary>
        public bool enabled = true;
    }

    /// <summary>
    /// The battle menu's state machine, with no Unity input in it so every transition can be unit tested. Implements the spec's
    /// table exactly: Q guards from the root menu and rotates a list backwards, E opens spells and rotates forwards, R opens or
    /// swaps to items (and closes them), Tab opens the flee prompt, Escape / right click go back one level, and Enter confirms
    /// fleeing — and only inside the prompt.
    /// </summary>
    public sealed class BattleMenuModel
    {
        /// <summary>The flee prompt ignores every key for this long after it opens, so a key still being tapped in combat cannot confirm it.</summary>
        public const float PromptGraceSeconds = 0.25f;

        private readonly List<MenuEntry> _spells = new List<MenuEntry>();
        private readonly List<MenuEntry> _items = new List<MenuEntry>();
        private MenuState _stateBeforePrompt = MenuState.Root;
        private string _announcedSelection;

        public MenuState State { get; private set; } = MenuState.Locked;
        public IReadOnlyList<MenuEntry> Spells => _spells;
        public IReadOnlyList<MenuEntry> Items => _items;

        /// <summary>One remembered index per list; reopening a list resumes where it was left.</summary>
        public int SpellIndex { get; private set; }

        public int ItemIndex { get; private set; }

        /// <summary>Seconds since the flee prompt opened.</summary>
        public float PromptAge { get; private set; }

        /// <summary>The state that Tab / Escape returns to from the flee prompt.</summary>
        public MenuState StateBeforePrompt => _stateBeforePrompt;

        public MenuEntry SelectedSpell => _spells.Count > 0 ? _spells[SpellIndex] : null;
        public MenuEntry SelectedItem => _items.Count > 0 ? _items[ItemIndex] : null;

        /// <summary>The spell currently selected for casting, or null (not in the spell list, or the flee prompt is open).</summary>
        public string SelectedSpellId => State == MenuState.Spells && SelectedSpell != null ? SelectedSpell.id : null;

        /// <summary>Anything changed that a view should redraw.</summary>
        public event Action Changed;

        public event Action GuardChosen;
        public event Action FleeConfirmed;

        /// <summary>The selected spell id changed (null = no spell selected). The HUD forwards this to the combat runner.</summary>
        public event Action<string> SelectionChanged;

        /// <summary>The player tried to commit a spell or item that cannot be used (not enough AP...): shake, "no" sound, spend nothing.</summary>
        public event Action<MenuEntry> CommitRejected;

        public event Action<MenuEntry> ItemCommitted;

        /// <summary>Starts a player turn: the menu returns to the root with these lists.</summary>
        public void BeginTurn(IEnumerable<MenuEntry> spells, IEnumerable<MenuEntry> items)
        {
            _spells.Clear();
            _items.Clear();
            if (spells != null)
            {
                _spells.AddRange(spells);
            }

            if (items != null)
            {
                _items.AddRange(items);
            }

            SpellIndex = 0;
            ItemIndex = 0;
            PromptAge = 0f;
            _stateBeforePrompt = MenuState.Root;
            State = MenuState.Root;
            Finish();
        }

        /// <summary>Refreshes whether each spell/item is usable (AP changed) without disturbing the selection.</summary>
        public void RefreshEntries(IEnumerable<MenuEntry> spells, IEnumerable<MenuEntry> items)
        {
            Replace(_spells, spells);
            Replace(_items, items);
            SpellIndex = Math.Min(SpellIndex, Math.Max(0, _spells.Count - 1));
            ItemIndex = Math.Min(ItemIndex, Math.Max(0, _items.Count - 1));
            Changed?.Invoke();
        }

        /// <summary>Enemy turn, an animation, or after committing: input is ignored until the next BeginTurn.</summary>
        public void Lock()
        {
            State = MenuState.Locked;
            Finish();
        }

        public void Tick(float deltaSeconds)
        {
            if (State == MenuState.FleeConfirm)
            {
                PromptAge += deltaSeconds;
            }
        }

        public void Press(MenuKey key)
        {
            if (State == MenuState.Locked)
            {
                return;
            }

            if (State == MenuState.FleeConfirm)
            {
                PressInPrompt(key);
                return;
            }

            if (key == MenuKey.Tab)
            {
                _stateBeforePrompt = State;
                State = MenuState.FleeConfirm;
                PromptAge = 0f;
                Finish();
                return;
            }

            switch (State)
            {
                case MenuState.Root:
                    PressInRoot(key);
                    break;
                case MenuState.Spells:
                    PressInSpells(key);
                    break;
                case MenuState.Items:
                    PressInItems(key);
                    break;
            }
        }

        private void PressInPrompt(MenuKey key)
        {
            if (key == MenuKey.Enter)
            {
                if (PromptAge > PromptGraceSeconds)
                {
                    FleeConfirmed?.Invoke();
                    Lock();
                }

                return;
            }

            if (key == MenuKey.Tab || key == MenuKey.Escape || key == MenuKey.Back)
            {
                State = _stateBeforePrompt;
                Finish();
            }
        }

        private void PressInRoot(MenuKey key)
        {
            switch (key)
            {
                case MenuKey.Q:
                    GuardChosen?.Invoke();
                    Lock();
                    break;
                case MenuKey.E:
                    Open(MenuState.Spells);
                    break;
                case MenuKey.R:
                    Open(MenuState.Items);
                    break;
            }
        }

        private void PressInSpells(MenuKey key)
        {
            switch (key)
            {
                case MenuKey.Q:
                    SpellIndex = Wrap(SpellIndex - 1, _spells.Count);
                    Finish();
                    break;
                case MenuKey.E:
                    SpellIndex = Wrap(SpellIndex + 1, _spells.Count);
                    Finish();
                    break;
                case MenuKey.R:
                    Open(MenuState.Items);
                    break;
                case MenuKey.Escape:
                case MenuKey.Back:
                    State = MenuState.Root;
                    Finish();
                    break;
                case MenuKey.Commit:
                    if (SelectedSpell != null && !SelectedSpell.enabled)
                    {
                        CommitRejected?.Invoke(SelectedSpell);
                    }

                    break;
            }
        }

        private void PressInItems(MenuKey key)
        {
            switch (key)
            {
                case MenuKey.Q:
                    ItemIndex = Wrap(ItemIndex - 1, _items.Count);
                    Finish();
                    break;
                case MenuKey.E:
                    ItemIndex = Wrap(ItemIndex + 1, _items.Count);
                    Finish();
                    break;
                case MenuKey.R:
                case MenuKey.Escape:
                case MenuKey.Back:
                    State = MenuState.Root;
                    Finish();
                    break;
                case MenuKey.Commit:
                    if (SelectedItem != null)
                    {
                        if (SelectedItem.enabled)
                        {
                            ItemCommitted?.Invoke(SelectedItem);
                        }
                        else
                        {
                            CommitRejected?.Invoke(SelectedItem);
                        }
                    }

                    break;
            }
        }

        /// <summary>Opens a list. A list with nothing in it stays closed, which is also why its command-bar button is shown disabled.</summary>
        private void Open(MenuState list)
        {
            if ((list == MenuState.Spells ? _spells.Count : _items.Count) == 0)
            {
                return;
            }

            State = list;
            Finish();
        }

        /// <summary>Announces the (possibly changed) selected spell and tells views to redraw.</summary>
        private void Finish()
        {
            string selection = SelectedSpellId;
            if (selection != _announcedSelection)
            {
                _announcedSelection = selection;
                SelectionChanged?.Invoke(selection);
            }

            Changed?.Invoke();
        }

        private static int Wrap(int index, int count)
        {
            return count <= 0 ? 0 : ((index % count) + count) % count;
        }

        private static void Replace(List<MenuEntry> target, IEnumerable<MenuEntry> source)
        {
            target.Clear();
            if (source != null)
            {
                target.AddRange(source);
            }
        }
    }
}
