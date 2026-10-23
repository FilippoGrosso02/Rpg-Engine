using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// The "Escape from combat?" modal. A 160x64 panel at (160, 103) with a red "FLEE?" ribbon, the question, and two key hints —
    /// ENTER FLEE and ESC STAY. The scene behind is dimmed with a #150D0B overlay at about 60% alpha. Purely visual: the model owns
    /// the rules (Enter-only confirm, 0.25 s grace, cancel with Tab / Esc / right click).
    /// </summary>
    public class FleePromptView : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        public void Build(BattleUiSprites sprites)
        {
            root = gameObject;

            var dim = UiFactory.Box(transform, "Dim", new Color(UiFactory.Dim.r, UiFactory.Dim.g, UiFactory.Dim.b, 0.6f), 0, 0, UiFactory.ScreenWidth, UiFactory.ScreenHeight);
            dim.raycastTarget = false;

            UiFactory.Image(transform, "Panel", sprites.Get("panel_dark"), 160, 103, 160, 64, Image.Type.Sliced);
            UiFactory.Image(transform, "Header", sprites.Get("ribbon_red"), 208, 95, 64, 16, Image.Type.Sliced);
            UiFactory.Text(transform, "HeaderText", "FLEE?", 208, 99, 64, 8, PixelText.Align.Center, UiFactory.Cream);
            UiFactory.Text(transform, "Question", "ESCAPE FROM COMBAT?", 160, 119, 160, 8, PixelText.Align.Center, UiFactory.Cream);

            Hint(sprites, "Flee", "ENTER FLEE", 172, 141, UiFactory.Heal);
            Hint(sprites, "Stay", "ESC STAY", 242, 141, UiFactory.Cream);

            Hide();
        }

        /// <summary>A 66x16 key hint plate: the plain framed plate sprite with the key and its action written on it.</summary>
        private void Hint(BattleUiSprites sprites, string name, string text, float x, float y, Color color)
        {
            UiFactory.Image(transform, name + "Plate", sprites.Get("frame_dark_small"), x, y, 66, 16, Image.Type.Sliced);
            UiFactory.Text(transform, name + "Label", text, x, y + 4, 66, 8, PixelText.Align.Center, color);
        }

        public void Show()
        {
            root.SetActive(true);
        }

        public void Hide()
        {
            root.SetActive(false);
        }

        public bool IsShowing => root != null && root.activeSelf;
    }
}
