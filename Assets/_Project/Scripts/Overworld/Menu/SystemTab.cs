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

        public override string Title => "SYSTEM";

        protected override void Build(RectTransform root)
        {
            _list = new MenuList(root, 0, 0, 150, 3);
            _message = UiFactory.Text(root, "Message", "", 0, 44, 424, 7, PixelText.Align.Left, UiFactory.Cream);
        }

        public override void Reset()
        {
            _confirmingQuit = false;
            if (_message != null) _message.text = "";
        }

        public override void Refresh()
        {
            _list.SetItems(new List<string> { "SAVE GAME", "SETTINGS", "QUIT" }, keepIndex: true);
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

            switch (_list.Index)
            {
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
