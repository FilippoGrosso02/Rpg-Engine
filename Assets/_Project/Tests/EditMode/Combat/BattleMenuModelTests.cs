using System.Collections.Generic;
using Game.Combat.UI.UGUI;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    /// <summary>
    /// The battle menu's state machine against the UI spec's state table. Q, E, R, Tab, Esc / right click, Commit and Enter each mean
    /// something different per state; Q and E have two meanings (Guard / open spells in the root menu, rotate once a list is open).
    /// </summary>
    public class BattleMenuModelTests
    {
        private BattleMenuModel _menu;
        private List<string> _events;
        private List<string> _selections;

        private static MenuEntry Spell(string id, int cost = 1, bool enabled = true)
        {
            return new MenuEntry { id = id, displayName = id, cost = cost, enabled = enabled };
        }

        [SetUp]
        public void SetUp()
        {
            _menu = new BattleMenuModel();
            _events = new List<string>();
            _selections = new List<string>();
            _menu.GuardChosen += () => _events.Add("guard");
            _menu.FleeConfirmed += () => _events.Add("flee");
            _menu.CommitRejected += entry => _events.Add("rejected:" + entry.id);
            _menu.ItemCommitted += entry => _events.Add("item:" + entry.id);
            _menu.SelectionChanged += id => _selections.Add(id ?? "none");
        }

        private void StartTurn(int spells = 3, int items = 0)
        {
            var spellList = new List<MenuEntry>();
            for (int i = 0; i < spells; i++)
            {
                spellList.Add(Spell("s" + i));
            }

            var itemList = new List<MenuEntry>();
            for (int i = 0; i < items; i++)
            {
                itemList.Add(Spell("i" + i));
            }

            _menu.BeginTurn(spellList, itemList);
        }

        // ---------------------------------------------------------------- ROOT

        [Test]
        public void Root_QGuardsAndLocksTheMenu()
        {
            StartTurn();
            _menu.Press(MenuKey.Q);

            CollectionAssert.AreEqual(new[] { "guard" }, _events);
            Assert.AreEqual(MenuState.Locked, _menu.State);
        }

        [Test]
        public void Root_EOpensSpells_AndSelectsTheFirst()
        {
            StartTurn();
            _menu.Press(MenuKey.E);

            Assert.AreEqual(MenuState.Spells, _menu.State);
            Assert.AreEqual("s0", _menu.SelectedSpellId);
            CollectionAssert.AreEqual(new[] { "s0" }, _selections);
        }

        [Test]
        public void Root_ROpensItems_OnlyWhenThereAreItems()
        {
            StartTurn(items: 0);
            _menu.Press(MenuKey.R);
            Assert.AreEqual(MenuState.Root, _menu.State, "No items: the button is disabled and the key is inert.");

            StartTurn(items: 2);
            _menu.Press(MenuKey.R);
            Assert.AreEqual(MenuState.Items, _menu.State);
        }

        [Test]
        public void Root_EIsInertWithoutSpells()
        {
            StartTurn(spells: 0);
            _menu.Press(MenuKey.E);

            Assert.AreEqual(MenuState.Root, _menu.State);
        }

        [Test]
        public void Root_EscapeAndRightClickDoNothing_AndCommitAndEnterDoNothing()
        {
            StartTurn();
            _menu.Press(MenuKey.Escape);
            _menu.Press(MenuKey.Back);
            _menu.Press(MenuKey.Commit);
            _menu.Press(MenuKey.Enter);

            Assert.AreEqual(MenuState.Root, _menu.State);
            Assert.IsEmpty(_events, "Enter is only valid inside the flee prompt; a click in the root menu is the runner's Basic Attack, not the menu's.");
        }

        // ---------------------------------------------------------------- SPELLS

        [Test]
        public void Spells_QAndERotate_AndWrapAround()
        {
            StartTurn(spells: 3);
            _menu.Press(MenuKey.E);          // open: s0

            _menu.Press(MenuKey.Q);          // wraps to the last
            Assert.AreEqual("s2", _menu.SelectedSpellId);

            _menu.Press(MenuKey.E);          // forward again: wraps to the first
            Assert.AreEqual("s0", _menu.SelectedSpellId);

            _menu.Press(MenuKey.E);
            Assert.AreEqual("s1", _menu.SelectedSpellId);
        }

        [Test]
        public void Spells_RSwitchesToItems_EscapeAndRightClickReturnToRoot()
        {
            StartTurn(spells: 2, items: 2);
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.R);
            Assert.AreEqual(MenuState.Items, _menu.State);

            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.Escape);
            _menu.Press(MenuKey.E);
            Assert.AreEqual(MenuState.Spells, _menu.State);

            _menu.Press(MenuKey.Back);
            Assert.AreEqual(MenuState.Root, _menu.State);
        }

        [Test]
        public void Spells_LeavingTheListClearsTheSelection()
        {
            StartTurn();
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.Escape);

            Assert.IsNull(_menu.SelectedSpellId);
            CollectionAssert.AreEqual(new[] { "s0", "none" }, _selections);
        }

        [Test]
        public void Spells_AGuardIsOnlyReachableFromTheRoot()
        {
            StartTurn();
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.Q);          // rotates, does not guard

            Assert.IsEmpty(_events);
            _menu.Press(MenuKey.Escape);
            _menu.Press(MenuKey.Q);          // now it guards
            CollectionAssert.AreEqual(new[] { "guard" }, _events);
        }

        [Test]
        public void Spells_CommittingADisabledSpellIsRejected_AndAnEnabledOneIsLeftToTheRunner()
        {
            _menu.BeginTurn(new[] { Spell("fireball", 3, enabled: false), Spell("missile", 1, enabled: true) }, null);
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.Commit);
            CollectionAssert.AreEqual(new[] { "rejected:fireball" }, _events);

            _events.Clear();
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.Commit);
            Assert.IsEmpty(_events, "An affordable spell is cast by the combat runner, which owns the click.");
        }

        [Test]
        public void Spells_ADisabledEntryStaysSelectable()
        {
            _menu.BeginTurn(new[] { Spell("a"), Spell("broke", 9, enabled: false) }, null);
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.E);

            Assert.AreEqual("broke", _menu.SelectedSpellId, "Disabled entries remain selectable so they can be read.");
        }

        [Test]
        public void Spells_AListIndexIsRememberedWhenReopened()
        {
            StartTurn(spells: 4);
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.E);          // s2
            _menu.Press(MenuKey.Escape);
            _menu.Press(MenuKey.E);

            Assert.AreEqual("s2", _menu.SelectedSpellId);
        }

        // ---------------------------------------------------------------- ITEMS

        [Test]
        public void Items_QAndERotate_RAndEscapeCloseTheList()
        {
            StartTurn(spells: 1, items: 3);
            _menu.Press(MenuKey.R);
            _menu.Press(MenuKey.E);
            Assert.AreEqual(1, _menu.ItemIndex);
            _menu.Press(MenuKey.Q);
            _menu.Press(MenuKey.Q);
            Assert.AreEqual(2, _menu.ItemIndex);

            _menu.Press(MenuKey.R);
            Assert.AreEqual(MenuState.Root, _menu.State, "R closes the item list.");

            _menu.Press(MenuKey.R);
            _menu.Press(MenuKey.Escape);
            Assert.AreEqual(MenuState.Root, _menu.State);
        }

        [Test]
        public void Items_AreNotAnnouncedAsSpellSelections()
        {
            StartTurn(spells: 2, items: 2);
            _menu.Press(MenuKey.R);

            Assert.IsNull(_menu.SelectedSpellId);
            Assert.IsEmpty(_selections);
        }

        // ---------------------------------------------------------------- FLEE

        [Test]
        public void Tab_OpensTheFleePrompt_FromEveryUnlockedState()
        {
            foreach (var setup in new System.Action[]
            {
                () => { },
                () => _menu.Press(MenuKey.E),
                () => _menu.Press(MenuKey.R)
            })
            {
                StartTurn(spells: 2, items: 2);
                setup();
                _menu.Press(MenuKey.Tab);
                Assert.AreEqual(MenuState.FleeConfirm, _menu.State);
            }
        }

        [Test]
        public void FleePrompt_EnterIsIgnoredDuringTheGracePeriod_ThenConfirms()
        {
            StartTurn();
            _menu.Press(MenuKey.Tab);

            _menu.Press(MenuKey.Enter);
            Assert.AreEqual(MenuState.FleeConfirm, _menu.State);
            Assert.IsEmpty(_events);

            _menu.Tick(BattleMenuModel.PromptGraceSeconds + 0.01f);
            _menu.Press(MenuKey.Enter);

            CollectionAssert.AreEqual(new[] { "flee" }, _events);
            Assert.AreEqual(MenuState.Locked, _menu.State);
        }

        [Test]
        public void FleePrompt_OnlyEnterConfirms_ClickAndSpaceDoNot()
        {
            StartTurn();
            _menu.Press(MenuKey.Tab);
            _menu.Tick(1f);

            _menu.Press(MenuKey.Commit);
            _menu.Press(MenuKey.Q);
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.R);

            Assert.IsEmpty(_events);
            Assert.AreEqual(MenuState.FleeConfirm, _menu.State);
        }

        [Test]
        public void FleePrompt_TabEscapeAndRightClickCancel_BackToWhereYouWere()
        {
            foreach (var cancel in new[] { MenuKey.Tab, MenuKey.Escape, MenuKey.Back })
            {
                StartTurn(spells: 2);
                _menu.Press(MenuKey.E);
                _menu.Press(MenuKey.Tab);
                _menu.Press(cancel);

                Assert.AreEqual(MenuState.Spells, _menu.State, $"cancelled with {cancel}");
                Assert.IsEmpty(_events);
            }
        }

        [Test]
        public void FleePrompt_PausesTheSelection_AndCancelingRestoresIt()
        {
            StartTurn(spells: 2);
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.Tab);
            _menu.Press(MenuKey.Escape);

            // selected s0, then cleared while the prompt is open, then re-announced on cancel
            CollectionAssert.AreEqual(new[] { "s0", "none", "s0" }, _selections);
        }

        // ---------------------------------------------------------------- LOCKED

        [Test]
        public void Locked_IgnoresEveryKey()
        {
            _menu.BeginTurn(new[] { Spell("a") }, null);
            _menu.Lock();

            foreach (MenuKey key in System.Enum.GetValues(typeof(MenuKey)))
            {
                _menu.Press(key);
            }

            Assert.AreEqual(MenuState.Locked, _menu.State);
            Assert.IsEmpty(_events);
        }

        [Test]
        public void ANewTurnUnlocksTheMenuAtTheRoot_AndForgetsTheLastTurnsIndexes()
        {
            StartTurn(spells: 3);
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.E);
            _menu.Lock();

            StartTurn(spells: 3);

            Assert.AreEqual(MenuState.Root, _menu.State);
            Assert.AreEqual(0, _menu.SpellIndex);
        }

        [Test]
        public void RefreshEntries_UpdatesAvailability_WithoutMovingTheSelection()
        {
            _menu.BeginTurn(new[] { Spell("a"), Spell("b", 3, enabled: false) }, null);
            _menu.Press(MenuKey.E);
            _menu.Press(MenuKey.E);
            Assert.IsFalse(_menu.SelectedSpell.enabled);

            _menu.RefreshEntries(new[] { Spell("a"), Spell("b", 3, enabled: true) }, null);

            Assert.AreEqual("b", _menu.SelectedSpellId);
            Assert.IsTrue(_menu.SelectedSpell.enabled);
        }
    }
}
