using System.Collections;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using Game.Data.Registry;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Combat.Tests.PlayMode
{
    public class EncounterSmokeTest
    {
        private const float TimeoutSeconds = 30f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DatabaseRegistry.Initialize();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartEncounter_SampleSkirmish_ResolvesToAResultWithoutHangingOrThrowing()
        {
            var encounter = Resources.Load<EncounterDefinition>("Data/Encounters/EncounterSampleSkirmish");
            Assert.IsNotNull(encounter, "Expected the sample EncounterDefinition to be resolvable from Resources.");

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

            Assert.IsTrue(completed, "Encounter did not resolve within the timeout — possible infinite loop.");
            Assert.IsNotNull(result, "Expected a non-null EncounterResult.");
        }
    }
}
