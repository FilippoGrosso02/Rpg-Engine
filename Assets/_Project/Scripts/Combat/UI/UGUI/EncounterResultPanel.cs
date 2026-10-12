using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    public class EncounterResultPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text detailsLabel;
        [SerializeField] private Button continueButton;

        private IPlayerCommandInput _input;

        public bool IsShowing => panelRoot != null && panelRoot.activeSelf;

        public void Bind(IPlayerCommandInput input)
        {
            _input = input;

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(HandleContinueClicked);
            }

            Hide();
        }

        public void Unbind()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(HandleContinueClicked);
            }

            _input = null;
        }

        public void Show(EncounterResult result)
        {
            if (titleLabel != null)
            {
                titleLabel.text = result.victory ? "Victory!" : "Defeat...";
            }

            if (detailsLabel != null)
            {
                var loot = result.lootItemIds.Count > 0 ? string.Join(", ", result.lootItemIds) : "none";
                detailsLabel.text = $"XP: {result.xpAwarded}\nLoot: {loot}";
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void HandleContinueClicked()
        {
            _input?.SubmitCommand(PlayerCommand.ContinueAfterResult);
            Hide();
        }
    }
}
