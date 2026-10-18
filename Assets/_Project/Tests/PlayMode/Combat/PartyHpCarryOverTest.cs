using System.Collections;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using Game.Data.Registry;
using Game.Data.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Combat.Tests.PlayMode
{
    public class PartyHpCarryOverTest
    {
        private const float TimeoutSeconds = 30f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DatabaseRegistry.Initialize();
            PartyState.HealAll();
            yield return null;
        }

        [TearDown]
        public void TearDown() => PartyState.HealAll();

        [UnityTest]
        public IEnumerator WonFight_StartsFromSavedHp_AndSavesHpAfterwards()
        {
            var encounter = Resources.Load<EncounterDefinition>("Data/Encounters/EncounterSampleSkirmish");
            Assert.IsNotNull(encounter);
            foreach (var id in encounter.partyActorIds) PartyState.SetHp(id, 3);

            var runnerObject = new GameObject("TestEncounterRunner");
            var runner = runnerObject.AddComponent<EncounterRunner>();
            runner.autoPilotAllSides = true;

            int highestAllyHpAtTurnStart = 0;
            runner.OnTurnStarted += c =>
            {
                if (c.isPlayerSide) highestAllyHpAtTurnStart = Mathf.Max(highestAllyHpAtTurnStart, c.currentHp);
            };

            EncounterResult result = null;
            runner.StartEncounter(encounter, r => result = r);

            float elapsed = 0f;
            while (result == null && elapsed < TimeoutSeconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            Object.Destroy(runnerObject);

            Assert.IsNotNull(result, "Encounter did not finish in time.");
            Assert.IsTrue(result.victory, "The sample matchup is expected to be a win.");
            Assert.LessOrEqual(highestAllyHpAtTurnStart, 3, "Allies should have started the fight at their saved 3 HP, not full HP.");
            foreach (var id in encounter.partyActorIds)
            {
                Assert.IsTrue(PartyState.TryGetHp(id, out int hp), $"{id} should have HP saved after a win.");
                Assert.GreaterOrEqual(hp, 1);
                Assert.LessOrEqual(hp, 3, "HP should not have risen above what the fight started with (no healing in this matchup).");
            }
        }
    }
}
