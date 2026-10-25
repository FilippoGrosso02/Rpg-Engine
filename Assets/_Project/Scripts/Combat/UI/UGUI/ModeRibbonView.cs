using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>The 64x16 ribbon above the command bar: gold "YOUR TURN" / "SPELLS" / "ITEMS", red "ENEMY TURN".</summary>
    public class ModeRibbonView : MonoBehaviour
    {
        [SerializeField] private BattleUiSprites sprites;
        [SerializeField] private Image ribbon;
        [SerializeField] private PixelText label;

        public void Build(BattleUiSprites spriteSource)
        {
            sprites = spriteSource;
            ribbon = GetComponent<Image>();
            if (ribbon == null)
            {
                ribbon = gameObject.AddComponent<Image>();
            }

            ribbon.raycastTarget = false;
            label = UiFactory.Text(transform, "Label", "", 0, 4, 64, 8, PixelText.Align.Center, UiFactory.Cream);
        }

        public static string TextFor(MenuState state)
        {
            switch (state)
            {
                case MenuState.Root: return "";
                case MenuState.Spells: return "SPELLS";
                case MenuState.Items: return "ITEMS";
                case MenuState.FleeConfirm: return "FLEE?";
                default: return "ENEMY TURN";
            }
        }

        /// <param name="lockedByPlayerAction">The menu is locked because the player's own action is resolving, so it isn't the enemy's turn yet.</param>
        public void SetState(MenuState state, bool lockedByPlayerAction = false)
        {
            bool enemy = state == MenuState.Locked && !lockedByPlayerAction;
            ribbon.sprite = sprites.Get(enemy ? "ribbon_red" : "ribbon_gold");
            label.text = state == MenuState.Locked && lockedByPlayerAction ? "" : TextFor(state);
            // Nothing to say in the plain root menu (no "YOUR TURN" banner): hide the ribbon entirely.
            ribbon.enabled = label.text.Length > 0;
        }
    }
}
