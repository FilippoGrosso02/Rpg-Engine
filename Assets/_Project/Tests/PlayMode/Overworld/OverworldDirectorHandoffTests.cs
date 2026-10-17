using System;
using System.Collections;
using Game.Combat.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Overworld.Tests.PlayMode
{
    /// <summary>
    /// Exercises the overworld -> battle -> overworld handoff in the real Dungeon_01 scene. The battle itself is replaced by a
    /// stub starter (<see cref="OverworldDirector.encounterStarter"/>) so these tests cover suspend/resume and outcome handling only;
    /// the real combat loop has its own suites.
    /// </summary>
    public class OverworldDirectorHandoffTests
    {
        OverworldDirector _director;
        OverworldEnemy _enemy;
        int _startCount;
        Action<EncounterResult> _finishBattle;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DefeatedEnemies.Clear();
            var load = SceneManager.LoadSceneAsync("Dungeon_01", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            yield return null; // let Start() run on everything

            _director = UnityEngine.Object.FindAnyObjectByType<OverworldDirector>();
            Assert.IsNotNull(_director, "Dungeon_01 should contain an OverworldDirector.");

            // Keep the live enemies from touching the player on their own during the test.
            foreach (var e in UnityEngine.Object.FindObjectsByType<OverworldEnemy>())
                e.contactEnabled = false;
            _enemy = UnityEngine.Object.FindObjectsByType<OverworldEnemy>()[0];
            Assert.IsNotNull(_enemy.encounter, "Placed enemies should carry an EncounterDefinition.");

            _startCount = 0;
            _finishBattle = null;
            _director.graceSeconds = 0.05f;
            _director.encounterStarter = (encounter, onComplete) =>
            {
                _startCount++;
                _finishBattle = onComplete;
            };
        }

        [UnityTest]
        public IEnumerator Contact_SuspendsOverworld_UntilBattleEnds()
        {
            _director.BeginEncounter(_enemy);
            yield return null;

            Assert.IsTrue(_director.InBattle);
            Assert.IsFalse(_director.overworldRoot.activeSelf, "Overworld root should be disabled while the battle runs.");
            Assert.AreEqual(1, _startCount);

            _finishBattle(new EncounterResult { victory = true });
            yield return null;

            Assert.IsFalse(_director.InBattle);
            Assert.IsTrue(_director.overworldRoot.activeSelf, "Overworld root should be restored after the battle.");
        }

        [UnityTest]
        public IEnumerator Victory_RemovesEnemyAndRemembersIt()
        {
            string id = _enemy.enemyId;
            var go = _enemy.gameObject;

            _director.BeginEncounter(_enemy);
            _finishBattle(new EncounterResult { victory = true });
            yield return null;

            Assert.IsTrue(DefeatedEnemies.Contains(id));
            Assert.IsTrue(go == null, "The defeated enemy should be destroyed.");
        }

        [UnityTest]
        public IEnumerator Defeat_RespawnsPlayer_AndKeepsEnemy()
        {
            var player = _director.player;
            player.Teleport(new Vector3(0f, 0.1f, 10f));
            string id = _enemy.enemyId;

            _director.BeginEncounter(_enemy);
            _finishBattle(new EncounterResult { victory = false });
            yield return null;

            Vector3 delta = player.transform.position - _director.respawnPoint.position;
            delta.y = 0f;
            Assert.Less(delta.magnitude, 0.1f, "Player should be back at the respawn point.");
            Assert.IsFalse(DefeatedEnemies.Contains(id));
            Assert.IsTrue(_enemy != null, "The enemy should survive a lost battle.");
        }

        [UnityTest]
        public IEnumerator SecondContactWhileInBattle_IsIgnored()
        {
            _director.BeginEncounter(_enemy);
            _director.BeginEncounter(_enemy);
            yield return null;

            Assert.AreEqual(1, _startCount, "Only one battle may run at a time.");
        }

        [UnityTest]
        public IEnumerator AfterBattle_ContactIsDisabledOnlyBrieflyThenRestored()
        {
            _director.BeginEncounter(_enemy);
            _finishBattle(new EncounterResult { victory = false });
            yield return null;

            Assert.IsFalse(_enemy.contactEnabled, "Grace period should block immediate re-engagement.");

            float waited = 0f;
            while (!_enemy.contactEnabled && waited < 2f)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
            Assert.IsTrue(_enemy.contactEnabled, "Contact should be re-enabled once the grace period ends.");
        }

        [TearDown]
        public void TearDown()
        {
            DefeatedEnemies.Clear();
        }
    }
}
