using System.Collections.Generic;
using Game.Audio;
using Game.Combat.UI.UGUI;
using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Data.Registry;
using Game.Data.Rules;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld.Menu
{
    /// <summary>
    /// Three columns: the party, the selected hero's gear slots, and the spare gear that fits the chosen slot. Enter (or D) steps right,
    /// Esc (or A) steps back left. Equipping swaps through the <see cref="Inventory"/>, and battle reads the result.
    /// </summary>
    public sealed class EquipmentTab : MenuTab
    {
        enum Column
        {
            Heroes,
            Slots,
            Choices
        }

        static readonly string[] SlotNames = { "WEAPON", "BODY", "SHIELD" };
        static readonly EquipmentSlot[] ArmorSlots = { EquipmentSlot.Body, EquipmentSlot.OffHand };

        MenuList _heroes;
        MenuList _slots;
        MenuList _choices;
        PixelText _choicesHeader;
        PixelText _stats;
        readonly List<string> _heroIds = new List<string>();
        readonly List<string> _choiceIds = new List<string>(); // parallel to the choices list; null = "take it off"
        Column _column = Column.Heroes;

        public override string Title => "EQUIPMENT";

        protected override void Build(RectTransform root)
        {
            _heroes = new MenuList(root, 0, 0, 104, 12);
            _slots = new MenuList(root, 112, 0, 150, 3);
            _choicesHeader = UiFactory.Text(root, "ChoicesHeader", "", 272, 0, 150, 8, PixelText.Align.Left, UiFactory.Gold);
            _choices = new MenuList(root, 272, 12, 150, 10);
            _stats = UiFactory.Text(root, "Stats", "", 112, 44, 150, 60, PixelText.Align.Left, UiFactory.Cream);
        }

        public override void Reset()
        {
            _column = Column.Heroes;
        }

        public override void Refresh()
        {
            _heroIds.Clear();
            var labels = new List<string>();
            foreach (var id in PartyRoster.Members)
            {
                var actor = DatabaseRegistry.GetActor(id);
                if (actor == null) continue;

                _heroIds.Add(id);
                labels.Add(actor.displayName);
            }

            _heroes.SetItems(labels, keepIndex: true);
            RefreshSlots();
            RefreshChoices();
            ApplyFocus();
        }

        public override void Handle(MenuInput input)
        {
            if (_heroIds.Count == 0) return;

            var list = ListFor(_column);
            if (input.moveY != 0 && list.Move(input.moveY))
            {
                if (_column == Column.Heroes) RefreshSlots();
                if (_column != Column.Choices) RefreshChoices();
                RefreshStats();
            }

            if (input.confirm || input.moveX > 0)
            {
                Forward();
            }
            else if (input.moveX < 0)
            {
                HandleBack();
            }
        }

        public override bool HandleBack()
        {
            if (_column == Column.Heroes) return false;

            _column = _column == Column.Choices ? Column.Slots : Column.Heroes;
            ApplyFocus();
            return true;
        }

        void Forward()
        {
            switch (_column)
            {
                case Column.Heroes:
                    _column = Column.Slots;
                    break;
                case Column.Slots:
                    if (_choices.Count == 0) return;
                    _column = Column.Choices;
                    break;
                case Column.Choices:
                    EquipChoice();
                    return;
            }

            ApplyFocus();
        }

        MenuList ListFor(Column column) => column == Column.Heroes ? _heroes : column == Column.Slots ? _slots : _choices;

        void ApplyFocus()
        {
            _heroes.Focused = _column == Column.Heroes;
            _slots.Focused = _column == Column.Slots;
            _choices.Focused = _column == Column.Choices;
            _heroes.Redraw();
            _slots.Redraw();
            _choices.Redraw();
        }

        ActorDefinition CurrentHero() =>
            _heroIds.Count > 0 ? DatabaseRegistry.GetActor(_heroIds[_heroes.Index]) : null;

        static string NameOf(EquipmentDefinition gear) => gear != null ? gear.displayName.ToUpperInvariant() : "NONE";

        void RefreshSlots()
        {
            var hero = CurrentHero();
            if (hero == null)
            {
                _slots.SetItems(new List<string>());
                return;
            }

            var weapon = DatabaseRegistry.GetWeapon(Loadout.GetWeaponId(hero));
            var body = DatabaseRegistry.GetArmor(Loadout.GetArmorIdInSlot(hero, EquipmentSlot.Body));
            var shield = DatabaseRegistry.GetArmor(Loadout.GetArmorIdInSlot(hero, EquipmentSlot.OffHand));
            _slots.SetItems(new List<string>
            {
                SlotNames[0] + ": " + NameOf(weapon),
                SlotNames[1] + ": " + NameOf(body),
                SlotNames[2] + ": " + NameOf(shield)
            }, keepIndex: true);
            RefreshStats();
        }

        /// <summary>Lists the spare gear that fits the highlighted slot, with a first row to take the current piece off.</summary>
        void RefreshChoices()
        {
            _choiceIds.Clear();
            var labels = new List<string>();
            var hero = CurrentHero();
            if (hero == null)
            {
                _choices.SetItems(labels);
                _choicesHeader.text = "";
                return;
            }

            bool weaponSlot = _slots.Index == 0;
            _choicesHeader.text = "SPARE " + SlotNames[_slots.Index];
            _choiceIds.Add(null);
            labels.Add(weaponSlot ? "- BARE HANDS -" : "- TAKE OFF -");

            if (weaponSlot)
            {
                foreach (var id in Inventory.SpareWeapons)
                {
                    var weapon = DatabaseRegistry.GetWeapon(id);
                    if (weapon == null) continue;

                    _choiceIds.Add(id);
                    labels.Add(weapon.displayName.ToUpperInvariant() + " " + weapon.damage.ToString().ToUpperInvariant());
                }
            }
            else
            {
                var slot = ArmorSlots[_slots.Index - 1];
                foreach (var id in Inventory.SpareArmor)
                {
                    var armor = DatabaseRegistry.GetArmor(id);
                    if (armor == null || armor.equipSlot != slot) continue;

                    _choiceIds.Add(id);
                    labels.Add(armor.displayName.ToUpperInvariant() + " +" + armor.acBonus);
                }
            }

            _choices.SetItems(labels);
        }

        void RefreshStats()
        {
            var sheet = HeroSheet.Build(CurrentHero());
            MenuText.SetBlock(_stats, sheet == null
                ? ""
                : "DEF " + sheet.armorClass + "\nATK " + sheet.attackBonus + "\nDAMAGE " + sheet.damageText.ToUpperInvariant());
        }

        void EquipChoice()
        {
            var hero = CurrentHero();
            if (hero == null || _choices.Count == 0) return;

            var id = _choiceIds[_choices.Index];
            if (_slots.Index == 0)
            {
                Loadout.EquipWeapon(hero, id);
                GameAudio.Play(AudioCue.EquipItem);
            }
            else if (id == null)
            {
                Loadout.UnequipArmor(hero, ArmorSlots[_slots.Index - 1]);
                GameAudio.Play(AudioCue.UnequipItem);
            }
            else
            {
                Loadout.EquipArmor(hero, id);
                GameAudio.Play(AudioCue.EquipItem);
            }

            // The gear lists changed under the cursor: redraw everything and drop back to the slot list.
            _column = Column.Slots;
            RefreshSlots();
            RefreshChoices();
            ApplyFocus();
        }
    }
}
