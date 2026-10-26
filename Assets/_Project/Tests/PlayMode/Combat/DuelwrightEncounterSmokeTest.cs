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
    /// Runs the real OSR roster (Warrior, Ranger and Thief against goblins and an ogre — not the legacy Test Hero/Slime fixture)
    /// through a full autopilot encounter — exercises the d10 initiative scheduler, attack rolls against DEF, monster morale and
    /// fleeing, and melee/ranged gating, end to end.
    /// (The class keeps its old name so the test's history stays attached.)
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
            Assert.IsNotNull(encounter, "Expected the sample skirmish EncounterDefinition to be resolvable from Resources.");

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
