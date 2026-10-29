using System.Collections;
using Game.Audio;
using Game.Combat.Contracts;
using Game.Data.Definitions;
using Game.Data.State;
using Game.Services;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// Hands the player off to combat when an enemy touches them. Two presentations, picked by CombatSystemConfig.FightInOverworld:
    /// in the arena flow the whole overworld lives under <see cref="overworldRoot"/> and deactivating it is the single switch that
    /// disables the camera, listener, input, enemy AI and rendering for the battle; in the in-place flow the overworld stays loaded
    /// and visible, frozen by <see cref="OverworldFreeze"/>, and the fight happens around the player and the enemy group.
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

        [Tooltip("Party the player starts with (pick heroes from the database). Only applied the first time in a session.")]
        public PartyRosterDefinition startingRoster;

        [Tooltip("In-place fights: seconds the camera takes to blend between the exploration view and the battle view.")]
        public float cameraBlendSeconds = 0.8f;

        public bool InBattle { get; private set; }

        /// <summary>
        /// True when fights play out on the overworld (CombatSystemConfig "Fight In Overworld"). Off means the original presentation:
        /// one character on screen, Q switches it at once, enemies are single models, and fights load the arena.
        /// </summary>
        public bool UsesInPlaceCombat => encounterStarter == null && combatSystemConfig != null && combatSystemConfig.FightInOverworld;

        OverworldEnemy _activeEnemy;
        OverworldFreeze _freeze;
        PartyFollowers _party;
        CinemachineBrain _blendBrain;
        CinemachineBlendDefinition _savedBlend;

        void Awake() => PartyRoster.EnsureInitialized(startingRoster);

        void Start()
        {
            GameAudio.SetMusicState(AudioMusicState.Overworld);

            // The rest of the party walks behind the player; created here (not in the scene) so every dungeon gets it.
            // Only for on-map fights: with the arena the overworld shows the leader alone, as it always did.
            if (player != null && UsesInPlaceCombat)
            {
                _party = GetComponent<PartyFollowers>();
                if (_party == null) _party = gameObject.AddComponent<PartyFollowers>();
                _party.Init(player);
            }

            // Left mouse swings the leader's weapon; hitting an enemy group starts the fight (an ambush if it had not noticed the party).
            if (GetComponent<PlayerAttack>() == null) gameObject.AddComponent<PlayerAttack>();
        }

        void OnEnable() => OverworldEnemy.PlayerContacted += BeginEncounter;
        void OnDisable() => OverworldEnemy.PlayerContacted -= BeginEncounter;

        /// <summary>The player is mid-swing at an enemy: contact from another enemy must not start a different fight meanwhile.</summary>
        public bool AttackPending { get; set; }

        /// <summary>An enemy caught the player: nobody was surprised.</summary>
        public void BeginEncounter(OverworldEnemy enemy)
        {
            if (AttackPending) return;
            BeginEncounter(enemy, EncounterAdvantage.None);
        }

        public void BeginEncounter(OverworldEnemy enemy, EncounterAdvantage advantage)
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
            GameAudio.SetMusicState(AudioMusicState.Battle);
            _activeEnemy = enemy;

            if (UsesInPlaceCombat)
            {
                BeginInPlace(enemy, advantage);
                return;
            }

            overworldRoot.SetActive(false);
            if (encounterStarter != null) encounterStarter(enemy.encounter, OnEncounterComplete);
            else combatSystemConfig.StartEncounter(enemy.encounter, OnEncounterComplete, advantage);
        }

        void BeginInPlace(OverworldEnemy enemy, EncounterAdvantage advantage)
        {
            var playerPosition = player != null ? player.transform.position : enemy.transform.position;
            var context = new InPlaceEncounterContext
            {
                partyPosition = playerPosition,
                enemyPosition = enemy.transform.position,
                fallbackForward = player != null ? player.transform.forward : Vector3.forward,
                introSeconds = cameraBlendSeconds,
                advantage = advantage,
                // Everyone fights where their overworld model stands.
                partyPositions = _party != null ? _party.CollectPositions() : null,
                enemyPositions = enemy.GetComponent<OverworldEnemyGroup>() != null
                    ? enemy.GetComponent<OverworldEnemyGroup>().CollectPositions(enemy.encounter.enemyIds.Count)
                    : null
            };

            _freeze = OverworldFreeze.Apply(player, enemy, FindObjectsByType<OverworldEnemy>(), FindAnyObjectByType<OverworldCameraRig>(), _party);
            OverrideCameraBlend();
            combatSystemConfig.StartEncounterInPlace(enemy.encounter, context, OnEncounterComplete);
        }

        /// <summary>The brain's default blend is a slow 2 s; a fight transition should be quick. Restored once the blend back is over.</summary>
        void OverrideCameraBlend()
        {
            if (_blendBrain != null) return; // already overridden by a fight that has only just ended
            _blendBrain = FindAnyObjectByType<CinemachineBrain>();
            if (_blendBrain == null) return;
            _savedBlend = _blendBrain.DefaultBlend;
            _blendBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, cameraBlendSeconds);
        }

        IEnumerator RestoreCameraBlend()
        {
            yield return new WaitForSeconds(cameraBlendSeconds + 0.25f);
            if (_blendBrain != null) _blendBrain.DefaultBlend = _savedBlend;
            _blendBrain = null;
        }

        void OnEncounterComplete(EncounterResult result)
        {
            GameAudio.SetMusicState(AudioMusicState.Overworld);
            overworldRoot.SetActive(true);

            if (_freeze != null)
            {
                // In-place fight: bring the frozen world back, with the player standing where the party's leader ended up.
                _freeze.Release();
                _freeze = null;
                if (result != null && (result.victory || result.fled) && result.leaderEndPosition.HasValue && player != null)
                    player.Teleport(result.leaderEndPosition.Value);
                if (_blendBrain != null) StartCoroutine(RestoreCameraBlend());
            }

            var enemies = FindObjectsByType<OverworldEnemy>();

            if (result != null && result.victory)
            {
                if (_activeEnemy != null)
                {
                    DefeatedEnemies.Add(_activeEnemy.enemyId);
                    if (_activeEnemy.endsDemo) DemoEndPanel.Show();
                    Destroy(_activeEnemy.gameObject);
                }
                foreach (var e in enemies) if (e != _activeEnemy) e.ForgetPlayer();
            }
            else if (result != null && result.fled)
            {
                // The party escaped: not a win (the enemy stays) and not a wipe (no heal, no teleport). Everyone loses track of the
                // player and contact stays off for the grace period below, so the same enemy cannot instantly re-trigger the fight.
                foreach (var e in enemies) e.ForgetPlayer();
            }
            else
            {
                // Placeholder defeat handling: back to the entrance, everything resets (party fully healed).
                PartyState.HealAll();
                if (player != null && respawnPoint != null) player.Teleport(respawnPoint.position);
                var cam = FindAnyObjectByType<TopDownCamera>();
                if (cam != null && cam.isActiveAndEnabled) cam.Snap();
                var rig = FindAnyObjectByType<OverworldCameraRig>();
                if (rig != null) rig.SnapBehind();
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
