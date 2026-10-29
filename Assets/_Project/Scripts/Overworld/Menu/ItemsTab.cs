using System.Collections.Generic;
using Game.Audio;
using Game.Combat.UI.UGUI;
using Game.Data.Registry;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld.Menu
{
    /// <summary>What the party carries and what each item does. Enter uses a healing item on the most wounded hero.</summary>
    public sealed class ItemsTab : MenuTab
    {
        const int DescriptionChars = 48;

        MenuList _list;
        PixelText _name;
        PixelText _description;
        PixelText _empty;
        PixelText _message;
        readonly List<string> _ids = new List<string>();

        public override string Title => "ITEMS";

        protected override void Build(RectTransform root)
        {
            _list = new MenuList(root, 0, 0, 150, 12);
            _name = UiFactory.Text(root, "Name", "", 164, 0, 256, 8, PixelText.Align.Left, UiFactory.Gold);
            _description = UiFactory.Text(root, "Description", "", 164, 14, 256, 120, PixelText.Align.Left, UiFactory.Cream);
            _message = UiFactory.Text(root, "Message", "", 164, 150, 256, 8, PixelText.Align.Left, UiFactory.Gold);
            _empty = UiFactory.Text(root, "Empty", "YOU ARE NOT CARRYING ANYTHING.", 0, 0, 300, 8, PixelText.Align.Left, UiFactory.Cream);
        }

        public override void Refresh()
        {
            _ids.Clear();
            var labels = new List<string>();
            foreach (var id in Inventory.ItemIds)
            {
                var item = DatabaseRegistry.GetItem(id);
                _ids.Add(id);
                labels.Add((item != null ? item.displayName : id) + " X" + Inventory.GetItemCount(id));
            }

            _list.SetItems(labels, keepIndex: true);
            _empty.gameObject.SetActive(_ids.Count == 0);
            ShowSelected();
        }

        public override void Handle(MenuInput input)
        {
            if (input.moveY != 0 && _list.Move(input.moveY))
            {
                _message.text = "";
                ShowSelected();
            }

            if (input.confirm && _ids.Count > 0)
            {
                if (PartyCare.UseHealingItem(_ids[_list.Index], out string message)) GameAudio.Play(AudioCue.PotionUse);
                else GameAudio.Play(AudioCue.UiDenied);
                _message.text = message.ToUpperInvariant();
            }
        }

        public override void Reset()
        {
            if (_message != null) _message.text = "";
        }

        void ShowSelected()
        {
            var item = _ids.Count > 0 ? DatabaseRegistry.GetItem(_ids[_list.Index]) : null;
            _name.text = item != null ? item.displayName.ToUpperInvariant() : "";
            MenuText.SetBlock(_description, item != null ? MenuText.Wrap(item.description.ToUpperInvariant(), DescriptionChars) : "");
        }
    }
}
