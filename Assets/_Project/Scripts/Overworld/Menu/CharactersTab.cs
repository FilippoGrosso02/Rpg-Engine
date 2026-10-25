using System.Collections.Generic;
using Game.Combat.UI.UGUI;
using Game.Data.Registry;
using Game.Data.Rules;
using Game.Data.State;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Overworld.Menu
{
    /// <summary>The party: pick a member on the left, read their sheet on the right.</summary>
    public sealed class CharactersTab : MenuTab
    {
        const float DetailX = 124f;
        const float BarWidth = 140f;

        MenuList _list;
        PixelText _name;
        PixelText _subtitle;
        PixelText _hpLabel;
        Image _hpBack;
        Image _hpFill;
        PixelText _body;
        PixelText _empty;
        readonly List<string> _ids = new List<string>();

        public override string Title => "CHARACTERS";

        protected override void Build(RectTransform root)
        {
            _list = new MenuList(root, 0, 0, 116, 12);

            _name = UiFactory.Text(root, "Name", "", DetailX, 0, 296, 8, PixelText.Align.Left, UiFactory.Gold);
            _subtitle = UiFactory.Text(root, "Subtitle", "", DetailX, 11, 296, 8, PixelText.Align.Left, UiFactory.Cream);

            _hpBack = UiFactory.Box(root, "HpBack", UiFactory.Dim, DetailX, 26, BarWidth, 7);
            _hpFill = UiFactory.Box(root, "HpFill", UiFactory.Heal, DetailX + 1, 27, BarWidth - 2, 5);
            _hpLabel = UiFactory.Text(root, "HpLabel", "", DetailX + BarWidth + 6, 26, 100, 8, PixelText.Align.Left, UiFactory.Cream);

            _body = UiFactory.Text(root, "Body", "", DetailX, 42, 296, 150, PixelText.Align.Left, UiFactory.Cream);
            _empty = UiFactory.Text(root, "Empty", "NO PARTY MEMBERS.", DetailX, 0, 296, 8, PixelText.Align.Left, UiFactory.Cream);
        }

        public override void Refresh()
        {
            _ids.Clear();
            var labels = new List<string>();
            foreach (var id in PartyRoster.Members)
            {
                var actor = DatabaseRegistry.GetActor(id);
                if (actor == null) continue;

                _ids.Add(id);
                labels.Add(actor.displayName);
            }

            _list.SetItems(labels, keepIndex: true);
            ShowSelected();
        }

        public override void Handle(MenuInput input)
        {
            if (input.moveY != 0 && _list.Move(input.moveY))
            {
                ShowSelected();
            }
        }

        void ShowSelected()
        {
            var actor = _ids.Count > 0 ? DatabaseRegistry.GetActor(_ids[_list.Index]) : null;
            var hero = HeroSheet.Build(actor);

            _empty.gameObject.SetActive(hero == null);
            foreach (var graphic in new Graphic[] { _name, _subtitle, _hpLabel, _hpBack, _hpFill, _body })
            {
                graphic.gameObject.SetActive(hero != null);
            }

            if (hero == null) return;

            _name.text = hero.displayName.ToUpperInvariant();
            _subtitle.text = (hero.className + " LV " + hero.level + (string.IsNullOrEmpty(hero.roleLabel) ? "" : " - " + hero.roleLabel)).ToUpperInvariant();
            _hpLabel.text = "HP " + hero.currentHp + "/" + hero.maxHp;

            var fillRect = _hpFill.rectTransform;
            fillRect.sizeDelta = new Vector2(Mathf.Round((BarWidth - 2) * Mathf.Clamp01(hero.currentHp / (float)hero.maxHp)), fillRect.sizeDelta.y);
            MenuText.SetBlock(_body, MenuText.Describe(hero));
        }
    }
}
