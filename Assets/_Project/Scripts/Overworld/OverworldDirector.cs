using System.Collections;
using Game.Combat.Contracts;
using Game.Services;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// Hands the player off to combat when an enemy touches them. The whole overworld lives under <see cref="overworldRoot"/>;
    /// deactivating it is the single switch that disables the camera, listener, input, enemy AI and rendering for the battle.
    /// Keep this component OUTSIDE the root so it stays active during the fight.
    /// </summary>
    public class OverworldDirector : MonoBehaviour
    {
        public CombatSystemConfig combatSystemConfig;
        public GameObject overworldRoot;
        public PlayerController player;
        public Transform respawnPoint;
        [Tooltip("Seconds after returning from a battle during which enemies cannot start another one.")]
        public float graceSeconds = 1.5f;

        /// <summary>Test seam: when set, used instead of <see cref="combatSystemConfig"/> to start a battle.</summary>
        public System.Action<EncounterDefinition, System.Action<EncounterResult>> encounterStarter;

        public bool InBattle { get; private set; }

        OverworldEnemy _activeEnemy;

        void OnEnable() => OverworldEnemy.PlayerContacted += BeginEncounter;
        void OnDisable() => OverworldEnemy.PlayerContacted -= BeginEncounter;

        public void BeginEncounter(OverworldEnemy enemy)
        {
            if (InBattle || enemy == null) return;
            if (enemy.encounter == null)
            {
                Debug.LogWarning($"{enemy.name} has no EncounterDefinition; ignoring contact.", enemy);
                return;
            }
            if (combatSystemConfig == null && encounterStarter == null)
            {
                Debug.LogError("OverworldDirector has no CombatSystemConfig assigned.", this);
                return;
            }

            InBattle = true;
            _activeEnemy = enemy;
            overworldRoot.SetActive(false);
            if (encounterStarter != null) encounterStarter(enemy.encounter, OnEncounterComplete);
            else combatSystemConfig.StartEncounter(enemy.encounter, OnEncounterComplete);
        }

        void OnEncounterComplete(EncounterResult result)
        {
            overworldRoot.SetActive(true);
            var enemies = FindObjectsByType<OverworldEnemy>();

            if (result != null && result.victory)
            {
                if (_activeEnemy != null)
                {
                    DefeatedEnemies.Add(_activeEnemy.enemyId);
                    Destroy(_activeEnemy.gameObject);
                }
                foreach (var e in enemies) if (e != _activeEnemy) e.ForgetPlayer();
            }
            else
            {
                // Placeholder defeat handling: back to the entrance, everything resets.
                if (player != null && respawnPoint != null) player.Teleport(respawnPoint.position);
                var cam = FindAnyObjectByType<TopDownCamera>();
                if (cam != null) cam.Snap();
                foreach (var e in enemies) e.ResetToStart();
            }

            _activeEnemy = null;
            InBattle = false;
            StartCoroutine(GracePeriod(enemies));
        }

        IEnumerator GracePeriod(OverworldEnemy[] enemies)
        {
            foreach (var e in enemies) if (e != null) e.contactEnabled = false;
            yield return new WaitForSeconds(graceSeconds);
            foreach (var e in enemies) if (e != null) e.contactEnabled = true;
        }
    }
}
