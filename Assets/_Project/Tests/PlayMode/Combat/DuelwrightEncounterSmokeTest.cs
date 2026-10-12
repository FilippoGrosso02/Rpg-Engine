using System.Collections;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using Game.Data.Registry;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Combat.Tests.PlayMode
{
    /// <summary>
    /// Runs the actual Duelwright roster (not the legacy Test Hero/Slime fixture) through a
    /// full autopilot encounter — exercises the new SPD-ordered scheduler, AP economy, Fear
    /// meter, melee/ranged gating, generic card effects, and every bespoke CardEffects
    /// behavior an AI would reasonably reach, end to end.
    /// </summary>
    public class DuelwrightEncounterSmokeTest
    {
        private const float TimeoutSeconds = 30f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DatabaseRegistry.Initialize();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartEncounter_DuelwrightSkirmish_ResolvesWithoutHangingOrThrowing()
        {
            var encounter = Resources.Load<EncounterDefinition>("Data/Encounters/EncounterDuelwrightSkirmish");
            Assert.IsNotNull(encounter, "Expected the Duelwright sample EncounterDefinition to be resolvable from Resources.");

            var runnerObject = new GameObject("TestEncounterRunner");
            var runner = runnerObject.AddComponent<EncounterRunner>();
            runner.autoPilotAllSides = true;

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

            Object.Destroy(runnerObject);

            Assert.IsTrue(completed, "Duelwright encounter did not resolve within the timeout — possible infinite loop.");
            Assert.IsNotNull(result, "Expected a non-null EncounterResult.");
        }
    }
}
