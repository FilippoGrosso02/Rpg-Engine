using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// The aim reticle. The cursor is locked, so the reticle is the screen centre — exactly where the combat runner casts its aiming
    /// ray. It turns red while that ray is on an enemy, and is hidden whenever the cursor is free (the result screen) or a modal is open.
    /// </summary>
    public class ReticleView : MonoBehaviour
    {
        private static readonly Color OverEnemy = new Color(1f, 0.55f, 0.5f, 1f);

        [SerializeField] private Image image;

        private bool _overEnemy;
        private bool _dimmed;

        public void Build(BattleUiSprites sprites)
        {
            // 29x29, centred on the 480x270 screen.
            image = UiFactory.Image(transform, "Reticle", sprites.Get("target_reticle"), 0, 0, 29, 29);
            UiFactory.Place(image.rectTransform, Mathf.Round((UiFactory.ScreenWidth - 29f) * 0.5f), Mathf.Round((UiFactory.ScreenHeight - 29f) * 0.5f), 29, 29);
        }

        public void SetOverEnemy(bool overEnemy)
        {
            _overEnemy = overEnemy;
            Refresh();
        }

        /// <summary>Dims the reticle under a modal (the flee prompt).</summary>
        public void SetDimmed(bool dimmed)
        {
            _dimmed = dimmed;
            Refresh();
        }

        private void Update()
        {
            bool shouldShow = Cursor.lockState == CursorLockMode.Locked;
            if (image.enabled != shouldShow)
            {
                image.enabled = shouldShow;
            }
        }

        private void Refresh()
        {
            var tint = _overEnemy ? OverEnemy : Color.white;
            tint.a = _dimmed ? 0.35f : 1f;
            image.color = tint;
        }
    }
}
