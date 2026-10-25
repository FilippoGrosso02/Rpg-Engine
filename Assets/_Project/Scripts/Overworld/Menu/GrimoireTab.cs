using System.Collections.Generic;
using Game.Combat.UI.UGUI;
using Game.Data.Registry;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld.Menu
{
    /// <summary>The rule book and the monsters the party has beaten. A and D flip between the two sections.</summary>
    public sealed class GrimoireTab : MenuTab
    {
        const int BodyChars = 44;

        enum Section
        {
            Rules,
            Monsters
        }

        MenuList _list;
        PixelText _sections;
        PixelText _title;
        PixelText _body;
        readonly List<string> _monsterIds = new List<string>();
        Section _section = Section.Rules;

        public override string Title => "GRIMOIRE";

        protected override void Build(RectTransform root)
        {
            _sections = UiFactory.Text(root, "Sections", "", 0, 0, 424, 8, PixelText.Align.Left, UiFactory.Gold);
            _list = new MenuList(root, 0, 14, 128, 12);
            _title = UiFactory.Text(root, "Title", "", 140, 14, 284, 8, PixelText.Align.Left, UiFactory.Gold);
            _body = UiFactory.Text(root, "Body", "", 140, 28, 284, 160, PixelText.Align.Left, UiFactory.Cream);
        }

        public override void Refresh()
        {
            _sections.text = _section == Section.Rules ? "< RULES >    MONSTERS" : "  RULES    < MONSTERS >";

            var labels = new List<string>();
            _monsterIds.Clear();
            if (_section == Section.Rules)
            {
                foreach (var topic in MenuText.Rules) labels.Add(topic.title);
            }
            else
            {
                foreach (var id in Bestiary.KnownIds)
                {
                    var enemy = DatabaseRegistry.GetEnemy(id);
                    if (enemy == null) continue;

                    _monsterIds.Add(id);
                    labels.Add(enemy.displayName);
                }
            }

            _list.SetItems(labels, keepIndex: true);
            ShowSelected();
        }

        public override void Handle(MenuInput input)
        {
            if (input.moveX != 0)
            {
                _section = _section == Section.Rules ? Section.Monsters : Section.Rules;
                Refresh();
                return;
            }

            if (input.moveY != 0 && _list.Move(input.moveY))
            {
                ShowSelected();
            }
        }

        void ShowSelected()
        {
            if (_section == Section.Rules)
            {
                var topic = MenuText.Rules[_list.Index];
                _title.text = topic.title;
                MenuText.SetBlock(_body, MenuText.Wrap(topic.body, BodyChars));
                return;
            }

            if (_monsterIds.Count == 0)
            {
                _title.text = "";
                MenuText.SetBlock(_body, MenuText.Wrap("NO MONSTERS DEFEATED YET.\n\nWIN A FIGHT AND THE MONSTERS YOU BEAT ARE WRITTEN HERE.", BodyChars));
                return;
            }

            var id = _monsterIds[_list.Index];
            var enemy = DatabaseRegistry.GetEnemy(id);
            _title.text = enemy.displayName.ToUpperInvariant();
            MenuText.SetBlock(_body, MenuText.Describe(enemy, Bestiary.GetKills(id)));
        }
    }
}
