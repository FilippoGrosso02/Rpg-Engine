using System;
using System.Collections.Generic;
using Game.Combat.Contracts;
using Game.Data.Registry;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// Lists the acting combatant's Craft skills (everything but its Basic Attack), grayed
    /// out when unaffordable. entryTemplate lives disabled in the prefab and is cloned once
    /// per skill on Show(). Confirming a skill and backing out are reported through separate
    /// callbacks — previously both routed through the same Hide()/onClosed path, which made
    /// picking a skill immediately reopen the command menu right on top of targeting mode.
    /// </summary>
    public class CraftSubmenu : MonoBehaviour
    {
        [SerializeField] private GameObject submenuRoot;
        [SerializeField] private RectTransform listParent;
        [SerializeField] private Button entryTemplate;
        [SerializeField] private Button backButton;

        private IPlayerCommandInput _input;
        private Action<string> _onConfirmed;
        private Action _onCancelled;
        private readonly List<GameObject> _spawnedEntries = new List<GameObject>();

        public void Bind(IPlayerCommandInput input, Action<string> onConfirmed, Action onCancelled)
        {
            _input = input;
            _onConfirmed = onConfirmed;
            _onCancelled = onCancelled;

            if (backButton != null)
            {
                backButton.onClick.AddListener(HandleBackClicked);
            }

            Hide();
        }

        public void Unbind()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(HandleBackClicked);
            }

            ClearEntries();
            _input = null;
            _onConfirmed = null;
            _onCancelled = null;
        }

        public void Show(List<string> skillIds, int currentAp)
        {
            ClearEntries();

            foreach (var skillId in skillIds)
            {
                var skill = DatabaseRegistry.GetSkill(skillId);
                if (skill == null)
                {
                    continue;
                }

                var entry = CreateEntry(skill.id, skill.displayName, skill.apCost, currentAp);
                if (entry != null)
                {
                    _spawnedEntries.Add(entry);
                }
            }

            if (submenuRoot != null)
            {
                submenuRoot.SetActive(true);
            }
        }

        /// <summary>Hides with no callback — used when resetting UI state for a new turn.</summary>
        public void Hide()
        {
            if (submenuRoot != null)
            {
                submenuRoot.SetActive(false);
            }
        }

        private void HandleBackClicked()
        {
            Hide();
            _onCancelled?.Invoke();
        }

        private GameObject CreateEntry(string skillId, string displayName, int apCost, int currentAp)
        {
            if (entryTemplate == null || listParent == null)
            {
                return null;
            }

            var entry = Instantiate(entryTemplate, listParent);
            entry.gameObject.SetActive(true);

            var label = entry.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = $"{displayName}  {apCost}/{currentAp} AP";
            }

            entry.interactable = currentAp >= apCost;

            entry.onClick.AddListener(() =>
            {
                _input?.SubmitCommand(PlayerCommand.Craft, skillId);
                Hide();
                _onConfirmed?.Invoke(displayName);
            });

            return entry.gameObject;
        }

        private void ClearEntries()
        {
            foreach (var entry in _spawnedEntries)
            {
                if (entry != null)
                {
                    Destroy(entry);
                }
            }

            _spawnedEntries.Clear();
        }
    }
}
