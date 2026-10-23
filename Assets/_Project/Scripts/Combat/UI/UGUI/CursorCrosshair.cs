using UnityEngine;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// Centre-screen aim marker. Shown only while the cursor is locked (third-person look); when the
    /// cursor is free (Alt held / Tab toggled) the real mouse pointer is the aim point instead.
    /// </summary>
    public class CursorCrosshair : MonoBehaviour
    {
        [SerializeField] private GameObject visual;

        private void Update()
        {
            if (visual != null)
            {
                bool show = Cursor.lockState == CursorLockMode.Locked;
                if (visual.activeSelf != show)
                {
                    visual.SetActive(show);
                }
            }
        }
    }
}
