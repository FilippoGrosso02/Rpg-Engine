using System.Collections;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using Game.Combat.UI.UGUI;
using Game.Data.Registry;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Combat.Tests.PlayMode
{
    /// <summary>
    /// Asserts the HUD's event wiring, not button presses: binding the real BattleHUD
    /// prefab to a headless (autopilot) encounter should still update ActingUnitCard and
    /// show EncounterResultPanel purely from ICombatEvents, with nothing clicking a menu.
    /// </summary>
    public class HudBindingSmokeTest
    {
        private const float TimeoutSeconds = 30f;
        private const string BattleHudPrefabPath = "Assets/_Project/Prefabs/Combat/UI/BattleHUD.prefab";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DatabaseRegistry.Initialize();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartEncounter_WithHudBound_UpdatesPanelsAndShowsResultOnEnd()
        {
#if UNITY_EDITOR
            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleHudPrefabPath);
#else
            GameObject hudPrefab = null;
            Assert.Ignore("Loading the BattleHUD prefab by path requires the Editor (AssetDatabase); this test doesn't run outside the Editor.");
#endif
            Assert.IsNotNull(hudPrefab, $"Expected the BattleHUD prefab at {BattleHudPrefabPath}.");

            var encounter = Resources.Load<EncounterDefinition>("Data/Encounters/EncounterSampleSkirmish");
            Assert.IsNotNull(encounter, "Expected the sample EncounterDefinition to be resolvable from Resources.");

            var runnerObject = new GameObject("TestEncounterRunner");
            var runner = runnerObject.AddComponent<EncounterRunner>();
            runner.autoPilotAllSides = true; // headless — nothing simulates menu clicks

            var hudObject = Object.Instantiate(hudPrefab);
            var hud = hudObject.GetComponent<BattleHudRoot>();
            Assert.IsNotNull(hud, "Expected the BattleHUD prefab to carry a BattleHudRoot component.");

            hud.Bind(runner, runner);

            // Duelwright's Basic Attack rolls a d6, so outcomes here aren't deterministic
            // turn-by-turn (unlike the old flat power-based formula). Rather than assume any
            // particular fight length, this test only relies on what's actually guaranteed:
            // every ally gets an OnTurnStarted at least once, which is what ActingUnitCard
            // reacts to. ActingUnitCard only ever shows whoever is *currently* acting (not a
            // per-combatant history), so the displayed HP is captured synchronously inside
            // this same handler — BattleHudRoot's own OnTurnStarted subscription (from
            // hud.Bind, above) already ran by the time this one fires, since delegates invoke
            // in subscription order.
            CombatantRuntime observedAlly = null;
            int observedActualHp = -1;
            int observedDisplayedHp = -2;
            runner.OnTurnStarted += combatant =>
            {
                if (observedAlly == null && combatant.isPlayerSide)
                {
                    observedAlly = combatant;
                    // Captured here, not re-read from observedAlly.currentHp later — that field
                    // keeps mutating for the rest of the encounter, so a later read would no
                    // longer reflect what ActingUnitCard displayed at this exact moment.
                    observedActualHp = combatant.currentHp;
                    observedDisplayedHp = hud.ActingUnitCard.DisplayedHp;
                }
            };

            bool resultPanelSeenVisible = false;
            runner.OnEncounterEnded += _ => resultPanelSeenVisible = hud.IsResultPanelVisible;

            EncounterResult result = null;
            bool completed = false;
            runner.StartEncounter(encounter, r =>
            {
                result = r;
                completed = true;
            });

            float elapsed = 0f;
            while (!completed && elapsed < TimeoutSeconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            Assert.IsTrue(completed, "Encounter did not resolve within the timeout.");
            Assert.IsNotNull(result);

            Assert.IsNotNull(observedAlly, "Expected at least one party member to have started a turn.");
            Assert.AreEqual(
                observedActualHp,
                observedDisplayedHp,
                "Expected ActingUnitCard's displayed HP to match the combatant's real HP at the moment its turn started.");

            Assert.IsTrue(resultPanelSeenVisible, "Expected EncounterResultPanel to be visible when OnEncounterEnded fired.");

            Object.Destroy(runnerObject);
            Object.Destroy(hudObject);
        }
    }
}
