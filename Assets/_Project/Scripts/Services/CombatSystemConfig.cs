using System;
using Game.Audio;
using Game.Combat.Contracts;
using Game.Combat.UI.UGUI;
using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// The swap point between callers and whichever IEncounterRunner implementation is
    /// currently active, and (this phase) the one place allowed to wire that implementation
    /// up to a UI layer — both sides only ever see this class, never each other.
    /// Swapping combat systems later means pointing encounterRunnerPrefab at a different
    /// prefab; the runner is only ever addressed through IEncounterRunner/ICombatEvents/
    /// IPlayerCommandInput, never a concrete type.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatSystemConfig", menuName = "Config/Combat System Config")]
    public class CombatSystemConfig : ScriptableObject
    {
        [SerializeField] private GameObject encounterRunnerPrefab;
        [SerializeField] private GameObject hudPrefab;

        [Tooltip("On: encounters play out on the overworld around the player and the enemy group (needs a runner that supports it). Off: the previous behaviour, a separate battle arena scene.")]
        [SerializeField] private bool fightInOverworld = true;

        /// <summary>Whether the overworld should hand fights to <see cref="StartEncounterInPlace"/> instead of loading an arena.</summary>
        public bool FightInOverworld => fightInOverworld && SupportsInPlace;

        private bool SupportsInPlace => encounterRunnerPrefab != null && encounterRunnerPrefab.GetComponent<IInPlaceEncounterRunner>() != null;

        /// <summary>
        /// Guards against a second StartEncounter call (e.g. F5 pressed twice, or any other
        /// double-trigger) while one is already running. Without this, a second runner+HUD
        /// pair gets instantiated on top of the first — two overlapping arenas, two
        /// TargetingInput/EventSystem/Camera("MainCamera") instances fighting over the same
        /// clicks — which reads as "raycasting doesn't work" when it's really two encounters
        /// running at once. Deliberately a GameObject reference, not a bool: this is a
        /// ScriptableObject asset, which does NOT get reset by stopping/starting Play Mode
        /// the way scene objects do — a bool left true by an encounter interrupted mid-fight
        /// (e.g. Play Mode stopped manually) would stay stuck true forever afterward. A
        /// destroyed UnityEngine.Object reference compares equal to null automatically, so
        /// this check self-heals the moment the old scene tears down, without extra cleanup.
        /// </summary>
        private GameObject _activeRunnerInstance;

        public void StartEncounter(EncounterDefinition encounter, Action<EncounterResult> onComplete, EncounterAdvantage advantage = EncounterAdvantage.None)
        {
            Launch(encounter, null, onComplete, advantage);
        }

        /// <summary>
        /// Runs the encounter on the loaded level around <paramref name="context"/>. Falls back to the arena flow when the configured
        /// runner cannot fight in place.
        /// </summary>
        public void StartEncounterInPlace(EncounterDefinition encounter, InPlaceEncounterContext context, Action<EncounterResult> onComplete)
        {
            Launch(encounter, context, onComplete);
        }

        private void Launch(EncounterDefinition encounter, InPlaceEncounterContext context, Action<EncounterResult> onComplete,
            EncounterAdvantage advantage = EncounterAdvantage.None)
        {
            if (_activeRunnerInstance != null)
            {
                Debug.LogWarning("[CombatSystemConfig] StartEncounter called while an encounter is already running — ignoring.");
                return;
            }

            if (encounterRunnerPrefab == null)
            {
                Debug.LogError("[CombatSystemConfig] No encounterRunnerPrefab assigned.");
                onComplete?.Invoke(new EncounterResult { victory = false });
                return;
            }

            var runnerInstance = UnityEngine.Object.Instantiate(encounterRunnerPrefab);
            var runner = runnerInstance.GetComponent<IEncounterRunner>();

            if (runner == null)
            {
                Debug.LogError("[CombatSystemConfig] encounterRunnerPrefab has no IEncounterRunner component.");
                UnityEngine.Object.Destroy(runnerInstance);
                onComplete?.Invoke(new EncounterResult { victory = false });
                return;
            }

            _activeRunnerInstance = runnerInstance;

            CombatAudioBridge.Attach(runnerInstance.GetComponent<ICombatEvents>());

            var hudInstance = BindHudIfConfigured(runnerInstance, runner);

            Action<EncounterResult> finish = result =>
            {
                _activeRunnerInstance = null;
                onComplete?.Invoke(result);
                UnityEngine.Object.Destroy(runnerInstance);
                if (hudInstance != null)
                {
                    UnityEngine.Object.Destroy(hudInstance);
                }
            };

            if (runner is ISurpriseEncounterRunner surpriseRunner)
            {
                surpriseRunner.Advantage = advantage;
            }

            if (context != null && runner is IInPlaceEncounterRunner inPlaceRunner)
            {
                inPlaceRunner.StartEncounterInPlace(encounter, context, finish);
            }
            else
            {
                if (context != null)
                {
                    Debug.LogWarning("[CombatSystemConfig] The runner cannot fight in place — falling back to the arena scene.");
                }

                runner.StartEncounter(encounter, finish);
            }
        }

        private GameObject BindHudIfConfigured(GameObject runnerInstance, IEncounterRunner runner)
        {
            if (hudPrefab == null)
            {
                return null;
            }

            var events = runnerInstance.GetComponent<ICombatEvents>();
            var input = runnerInstance.GetComponent<IPlayerCommandInput>();

            if (events == null || input == null)
            {
                Debug.LogWarning("[CombatSystemConfig] hudPrefab is assigned but the runner doesn't implement ICombatEvents/IPlayerCommandInput — starting without a HUD.");
                return null;
            }

            var hudInstance = UnityEngine.Object.Instantiate(hudPrefab);
            var hudRoot = hudInstance.GetComponent<BattleHudRoot>();

            if (hudRoot == null)
            {
                Debug.LogWarning("[CombatSystemConfig] hudPrefab has no BattleHudRoot component — starting without a HUD.");
                UnityEngine.Object.Destroy(hudInstance);
                return null;
            }

            hudRoot.Bind(events, input);

            // Only gate teardown on the result panel when a HUD is actually present —
            // otherwise (e.g. the PlayMode smoke test's bare EncounterRunner) nothing would
            // ever call SubmitCommand(ContinueAfterResult) and the encounter would hang.
            runner.WaitForResultAcknowledgementBeforeUnload = true;

            return hudInstance;
        }
    }
}
