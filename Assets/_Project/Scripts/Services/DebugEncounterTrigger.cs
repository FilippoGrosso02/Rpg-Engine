using Game.Combat.Contracts;
using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// Stands in for the field-side encounter trigger, which is a separate, later piece of
    /// work (see plan's "not part of this module" section). Press the hotkey to start the
    /// sample encounter directly through CombatSystemConfig.
    /// </summary>
    public class DebugEncounterTrigger : MonoBehaviour
    {
        public CombatSystemConfig combatSystemConfig;
        public EncounterDefinition sampleEncounter;
        public KeyCode hotkey = KeyCode.F5;

        private void Update()
        {
            if (Input.GetKeyDown(hotkey))
            {
                TriggerSampleEncounter();
            }
        }

        public void TriggerSampleEncounter()
        {
            if (combatSystemConfig == null || sampleEncounter == null)
            {
                Debug.LogWarning("[DebugEncounterTrigger] Missing combatSystemConfig or sampleEncounter reference.");
                return;
            }

            combatSystemConfig.StartEncounter(sampleEncounter, result =>
            {
                Debug.Log($"[DebugEncounterTrigger] Encounter finished — victory={result.victory}, xp={result.xpAwarded}, loot=[{string.Join(", ", result.lootItemIds)}]");
            });
        }
    }
}
