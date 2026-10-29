using System.Collections.Generic;
using Game.Combat.UI.UGUI;
using UnityEngine;

namespace Game.Overworld.Menu
{
    /// <summary>Save game and settings are placeholders for now; Quit asks first.</summary>
    public sealed class SystemTab : MenuTab
    {
        MenuList _list;
        PixelText _message;
        bool _confirmingQuit;
        bool _hasFollowers; // the party follow switch only exists when the party is on the map (fights play out there)

        public override string Title => "SYSTEM";

        protected override void Build(RectTransform root)
        {
            _list = new MenuList(root, 0, 0, 150, 4);
            _message = UiFactory.Text(root, "Message", "", 0, 55, 424, 7, PixelText.Align.Left, UiFactory.Cream);
        }

        public override void Reset()
        {
            _confirmingQuit = false;
            if (_message != null) _message.text = "";
        }

        public override void Refresh()
        {
            _hasFollowers = Object.FindAnyObjectByType<PartyFollowers>() != null;
            var items = new List<string>();
            if (_hasFollowers) items.Add(PartyFollowers.Following ? "PARTY FOLLOW: ON" : "PARTY FOLLOW: OFF");
            items.AddRange(new[] { "SAVE GAME", "SETTINGS", "QUIT" });
            _list.SetItems(items, keepIndex: true);
            _list.Focused = !_confirmingQuit;
            _list.Redraw();
        }

        public override void Handle(MenuInput input)
        {
            if (_confirmingQuit)
            {
                if (input.confirm) Quit();
                return;
            }

            if (input.moveY != 0 && _list.Move(input.moveY))
            {
                _message.text = "";
            }

            if (!input.confirm) return;

            int choice = _list.Index - (_hasFollowers ? 1 : 0); // -1 is the party follow switch
            switch (choice)
            {
                case -1:
                    PartyFollowers.Following = !PartyFollowers.Following;
                    MenuText.SetBlock(_message, PartyFollowers.Following
                        ? "THE PARTY FOLLOWS YOU AGAIN."
                        : "THE PARTY HOLDS ITS GROUND,\nBUT STAYS WITHIN FIGHTING RANGE.");
                    Refresh();
                    break;
                case 0:
                    MenuText.SetBlock(_message, "SAVING IS NOT AVAILABLE YET.");
                    break;
                case 1:
                    MenuText.SetBlock(_message, "SETTINGS ARE NOT AVAILABLE YET.");
                    break;
                default:
                    _confirmingQuit = true;
                    MenuText.SetBlock(_message, "QUIT THE GAME?\nENTER = YES    ESC = NO");
                    _list.Focused = false;
                    _list.Redraw();
                    break;
            }
        }

        public override bool HandleBack()
        {
            if (!_confirmingQuit) return false;

            _confirmingQuit = false;
            _message.text = "";
            _list.Focused = true;
            _list.Redraw();
            return true;
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
