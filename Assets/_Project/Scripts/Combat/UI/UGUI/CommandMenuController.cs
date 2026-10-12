using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Data.Registry;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// Craft / Guard menu, visible only while a player-side combatant's turn is active.
    /// There's no Attack button — clicking any enemy directly performs a Basic Attack (see
    /// EncounterRunner.ResolvePlayerTurn); this menu only covers the actions that need an
    /// explicit choice before targeting (or, for Guard, no targeting at all). Only talks to
    /// IPlayerCommandInput — never touches the concrete runner.
    /// </summary>
    public class CommandMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private Button craftButton;
        [SerializeField] private Button guardButton;
        [SerializeField] private CraftSubmenu craftSubmenu;
        [SerializeField] private TargetingPrompt targetingPrompt;

        private IPlayerCommandInput _input;
        private CombatantRuntime _actingCombatant;

        public void Bind(IPlayerCommandInput input)
        {
            _input = input;

            if (craftButton != null)
            {
                craftButton.onClick.AddListener(HandleCraftClicked);
            }

            if (guardButton != null)
            {
                guardButton.onClick.AddListener(HandleGuardClicked);
            }

            if (craftSubmenu != null)
            {
                craftSubmenu.Bind(input, HandleCraftConfirmed, HandleCraftCancelled);
            }

            if (targetingPrompt != null)
            {
                targetingPrompt.Bind(input);
            }

            SetActingCombatant(null);
        }

        public void Unbind()
        {
            if (craftButton != null)
            {
                craftButton.onClick.RemoveListener(HandleCraftClicked);
            }

            if (guardButton != null)
            {
                guardButton.onClick.RemoveListener(HandleGuardClicked);
            }

            if (craftSubmenu != null)
            {
                craftSubmenu.Unbind();
            }

            if (targetingPrompt != null)
            {
                targetingPrompt.Unbind();
            }

            _input = null;
            _actingCombatant = null;
            SetMenuVisible(false);
        }

        public void SetActingCombatant(CombatantRuntime combatant)
        {
            _actingCombatant = combatant;

            if (craftSubmenu != null)
            {
                craftSubmenu.Hide();
            }

            if (targetingPrompt != null)
            {
                targetingPrompt.Hide();
            }

            SetMenuVisible(combatant != null);

            if (combatant != null && craftButton != null)
            {
                craftButton.interactable = GetCraftSkillIds(combatant)
                    .Select(DatabaseRegistry.GetSkill)
                    .Any(skill => skill != null && combatant.currentAp >= skill.apCost);
            }
        }

        private void HandleCraftClicked()
        {
            if (_actingCombatant == null || craftSubmenu == null)
            {
                return;
            }

            craftSubmenu.Show(GetCraftSkillIds(_actingCombatant), _actingCombatant.currentAp);
            SetMenuVisible(false);
        }

        private void HandleGuardClicked()
        {
            _input?.SubmitCommand(PlayerCommand.Guard);
            SetMenuVisible(false);
        }

        private void HandleCraftConfirmed(string skillDisplayName)
        {
            targetingPrompt?.Show(skillDisplayName);
        }

        private void HandleCraftCancelled()
        {
            // Re-show the root menu if the player backs out without picking a craft.
            SetMenuVisible(_actingCombatant != null);
        }

        private static List<string> GetCraftSkillIds(CombatantRuntime combatant)
        {
            // Convention: the first known skill is the actor's Basic Attack (matches
            // EncounterRunner.GetFirstUsableSkill / the AI's behavior) — everything after
            // that is a selectable Craft. SkillDefinition has no explicit "is basic attack"
            // flag to key off instead.
            return combatant.skillIds.Skip(1).ToList();
        }

        private void SetMenuVisible(bool visible)
        {
            if (menuRoot != null)
            {
                menuRoot.SetActive(visible);
            }
        }
    }
}
