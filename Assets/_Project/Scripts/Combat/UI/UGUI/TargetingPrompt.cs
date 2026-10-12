using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// Shown once the player has committed to Attack/Craft and must now click a target in
    /// the arena. Previously that state had no on-screen feedback and no way out besides
    /// guessing to click an enemy — this makes it explicit and gives a Cancel button back to
    /// the command menu.
    /// </summary>
    public class TargetingPrompt : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text promptLabel;
        [SerializeField] private Button cancelButton;

        private IPlayerCommandInput _input;

        public void Bind(IPlayerCommandInput input)
        {
            _input = input;

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(HandleCancelClicked);
            }

            Hide();
        }

        public void Unbind()
        {
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(HandleCancelClicked);
            }

            _input = null;
        }

        public void Show(string actionDisplayName)
        {
            if (promptLabel != null)
            {
                promptLabel.text = $"Select a target for {actionDisplayName}";
            }

            if (root != null)
            {
                root.SetActive(true);
            }
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void HandleCancelClicked()
        {
            Hide();
            _input?.SubmitCommand(PlayerCommand.CancelCommand);
        }
    }
}
