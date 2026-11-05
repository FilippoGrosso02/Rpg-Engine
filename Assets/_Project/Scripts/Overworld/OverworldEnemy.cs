using System;
using System.Collections.Generic;
using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Overworld
{
    /// <summary>
    /// Visible overworld enemy: patrols waypoints, chases the player on sight, returns to patrol when it loses them,
    /// and raises <see cref="PlayerContacted"/> when it touches the player. Starting the battle is the director's job.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class OverworldEnemy : MonoBehaviour
    {
        public enum State { Patrol, Chase, Return, Ambush }

        /// <summary>Raised once when this enemy reaches the player and <see cref="contactEnabled"/> is true.</summary>
        public static event Action<OverworldEnemy> PlayerContacted;

        /// <summary>Raised once when an undetected ambush connects (melee contact or a shot) inside its surprise window: the enemies have the advantage.</summary>
        public static event Action<OverworldEnemy> AmbushTouched;

        [Header("Identity")]
        [Tooltip("Stable unique id within the dungeon; used later to remember defeated enemies.")]
        public string enemyId;
        public EncounterDefinition encounter;
        [Tooltip("Show every enemy of the encounter on the overworld, patrolling behind this one (this enemy stands for the first). Off: only this model is visible.")]
        public bool showGroupMembers = true;
        [Tooltip("Winning this fight ends the demo (final boss).")]
        public bool endsDemo;

        [Header("Behaviour")]
        [Tooltip("On: attacks on sight (chases the party and fights on contact). Off: sees the party but only fights when the party attacks it.")]
        public bool aggressive = true;

        [Header("Ambush")]
        [Tooltip("Lies hidden until the party comes near. Each approach the party member with the highest MIND checks for it; failing lets it spring an ambush.")]
        public bool ambusher;
        [Tooltip("The ambush triggers when any party member comes this close.")]
        public float ambushRange = 5f;
        [Tooltip("Difficulty of the MIND check that spots the ambush (d10 + MIND against this; 7 is a coin flip at MIND 7).")]
        public int ambushDetectDifficulty = 7;
        [Tooltip("After springing, the party has this many real seconds to avoid being touched. Touched = the fight starts with the enemies surprising the party.")]
        public float surpriseWindowSeconds = 10f;
        [Tooltip("When the window ends without a touch the enemy patrols for this long, then hides again.")]
        public float rehideSeconds = 8f;
        [Tooltip("Shoots at the party during the surprise window as well as running at them. A hit counts as a touch.")]
        public bool rangedAmbush;
        public float shotRange = 12f;
        public float shotInterval = 1.5f;

        [Header("Patrol")]
        public Transform[] patrolPoints;
        public float patrolSpeed = 2f;
        public float waitAtPoint = 1f;

        [Header("Detection")]
        public float viewRadius = 6f;
        [Range(10f, 360f)] public float viewAngle = 110f;
        public float eyeHeight = 1f;
        public float loseSightTime = 3f;
        [Tooltip("Within this distance the enemy notices the player even from behind (still needs line of sight).")]
        public float closeSenseRadius = 2f;
        [Tooltip("While the leader is Hidden, enemies only notice them this close (line of sight needed).")]
        public float hiddenSenseRadius = 1.2f;

        [Header("Chase / contact")]
        public float chaseSpeed = 4.2f;
        public float contactRadius = 1.1f;
        public bool contactEnabled = true;

        [Header("Debug visuals")]
        public Renderer stateRenderer;
        public Color patrolColor = new Color(0.85f, 0.2f, 0.2f);
        public Color chaseColor = new Color(1f, 0.6f, 0.1f);

        public State CurrentState { get; private set; } = State.Patrol;

        /// <summary>True while this enemy is hunting the player. Patrolling or walking back to its route means it has not noticed them.</summary>
        public bool HasNoticedPlayer => CurrentState == State.Chase || (!aggressive && _canSee);

        /// <summary>True while an undetected ambusher is hidden and waiting: it cannot be seen or attacked.</summary>
        public bool IsLyingInWait => CurrentState == State.Ambush;

        /// <summary>
        /// Distance from <paramref name="from"/> to the nearest model of this group (the enemy itself or one of its trailing members)
        /// that lies within <paramref name="coneDegrees"/> of <paramref name="facing"/>. <paramref name="inCone"/> is false when none does.
        /// </summary>
        /// <summary>The positions of every model in this group: the enemy itself and its trailing members.</summary>
        public IEnumerable<Vector3> ModelPositions()
        {
            yield return transform.position;
            var group = GetComponent<OverworldEnemyGroup>();
            if (group != null) foreach (var member in group.Members) yield return member.transform.position;
        }

        /// <summary>The model of this group that stands closest to <paramref name="from"/>.</summary>
        public Vector3 NearestModelPosition(Vector3 from)
        {
            var best = transform.position;
            float bestDistance = float.MaxValue;
            foreach (var position in ModelPositions())
            {
                var delta = position - from;
                delta.y = 0f;
                if (delta.sqrMagnitude >= bestDistance) continue;
                bestDistance = delta.sqrMagnitude;
                best = position;
            }

            return best;
        }

        public float NearestDistanceTo(Vector3 from, Vector3 facing, float coneDegrees, out bool inCone)
        {
            float best = float.MaxValue;
            var group = GetComponent<OverworldEnemyGroup>();

            bool Consider(Vector3 position)
            {
                var delta = position - from;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.01f && facing.sqrMagnitude > 0.0001f && Vector3.Angle(facing, delta) > coneDegrees * 0.5f) return false;
                best = Mathf.Min(best, delta.magnitude);
                return true;
            }

            inCone = Consider(transform.position);
            if (group != null) foreach (var member in group.Members) inCone |= Consider(member.transform.position);
            return best;
        }

        NavMeshAgent _agent;
        Transform _player;
        Vector3 _home;
        int _patrolIndex;
        float _waitTimer;
        float _timeSinceSeen;
        bool _contactRaised;
        bool _canSee;
        bool _armed;            // an undetected ambusher (stays armed after a failed ambush)
        bool _inWindow;         // the surprise window is open
        float _windowEnds;
        float _rehideAt;        // > 0: patrolling until this time, then hides again
        bool _rolled;           // the detection check for this approach has been made
        float _nextShot;
        PartyFollowers _party;
        readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        readonly List<Collider> _hiddenColliders = new List<Collider>();
        MaterialPropertyBlock _block;

        void Awake()
        {
            if (DefeatedEnemies.Contains(enemyId))
            {
                Destroy(gameObject);
                return;
            }
            _agent = GetComponent<NavMeshAgent>();
            _home = transform.position;
        }

        /// <summary>Stops (or resumes) this enemy's brain and movement. Used while a fight plays out on the overworld.</summary>
        public void Freeze(bool frozen)
        {
            if (_agent == null) return; // destroyed in Awake (already defeated)
            enabled = !frozen; // OnEnable clears the stale path when unfreezing
            var group = GetComponent<OverworldEnemyGroup>();
            if (group != null) group.Freeze(frozen);
            if (!_agent.isOnNavMesh) return;
            _agent.isStopped = frozen;
            if (frozen) _agent.velocity = Vector3.zero;
        }

        /// <summary>The group's other members only exist on the map when fights are played out there; with the arena one model stands for the whole group.</summary>
        static bool FightsInPlace()
        {
            var director = FindAnyObjectByType<OverworldDirector>();
            return director != null && director.UsesInPlaceCombat;
        }

        /// <summary>Drop any chase and walk back to the patrol route.</summary>
        public void ForgetPlayer()
        {
            _inWindow = false;
            if (CurrentState == State.Chase) SetState(State.Return);
        }

        /// <summary>Warp back to the starting position and restart the patrol from the first waypoint.</summary>
        public void ResetToStart()
        {
            if (_agent == null) return;
            _agent.Warp(_home);
            _patrolIndex = 0;
            _waitTimer = 0f;
            _inWindow = false;
            _rehideAt = 0f;
            SetState(State.Patrol);
            var group = GetComponent<OverworldEnemyGroup>();
            if (group != null) group.WarpToSlots();
            SetVisible(true);
            _armed = ambusher;
            if (_armed) EnterAmbush();
        }

        void Start()
        {
            if (_agent == null) return; // destroyed in Awake (already defeated)
            var playerGo = GameObject.FindWithTag("Player");
            if (playerGo != null) _player = playerGo.transform;
            SetState(State.Patrol);

            if (showGroupMembers && encounter != null && encounter.enemyIds.Count > 1 && FightsInPlace())
                gameObject.AddComponent<OverworldEnemyGroup>().Build(this);

            _armed = ambusher;
            if (_armed) EnterAmbush();
        }

        void OnEnable()
        {
            // The overworld root is toggled on/off around battles; resume cleanly.
            _contactRaised = false;
            if (_agent != null && _agent.isOnNavMesh) _agent.ResetPath();
        }

        void Update()
        {
            if (_player == null || !_agent.isOnNavMesh) return;

            if (CurrentState == State.Ambush)
            {
                TickAmbush();
                return;
            }

            bool canSee = CanSeePlayer();
            _canSee = canSee;
            if (canSee && PartyRules.Hidden)
            {
                PartyRules.EndHidden();
                InteractionSystem.ShowMessage("SPOTTED! The leader is no longer hidden.");
            }

            if (_inWindow && Time.time >= _windowEnds) EndWindow();

            switch (CurrentState)
            {
                case State.Patrol:
                case State.Return:
                    if (_rehideAt > 0f && Time.time >= _rehideAt && !canSee)
                    {
                        EnterAmbush();
                        return;
                    }

                    // Having just failed an ambush it patrols, and does not chase, until it has hidden again.
                    if (canSee && aggressive && _rehideAt <= 0f) { SetState(State.Chase); break; }
                    TickPatrol();
                    break;
                case State.Chase:
                    if (canSee) _timeSinceSeen = 0f; else _timeSinceSeen += Time.deltaTime;
                    if (_inWindow) _timeSinceSeen = 0f; // an ambush does not give up the chase
                    if (_timeSinceSeen >= loseSightTime) { SetState(State.Return); break; }
                    _agent.SetDestination(_player.position);
                    if (_inWindow) TickShooting();
                    break;
            }

            CheckContact();
        }

        // ------------------------------------------------------------------ ambush

        PartyFollowers Party => _party != null ? _party : _party = FindAnyObjectByType<PartyFollowers>();

        IEnumerable<Vector3> PartyPositions()
        {
            if (Party != null) return Party.MemberPositions();
            return new[] { _player.position };
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        bool PartyWithin(Vector3 point, float radius)
        {
            foreach (var position in PartyPositions()) if (Flat(position, point) <= radius) return true;
            return false;
        }

        /// <summary>Hides in place (models and colliders off) and waits for the party to come close.</summary>
        void EnterAmbush()
        {
            _rehideAt = 0f;
            _inWindow = false;
            _rolled = false;
            _canSee = false;
            SetVisible(false);
            SetState(State.Ambush);
        }

        void TickAmbush()
        {
            if (!PartyWithin(transform.position, ambushRange))
            {
                _rolled = false;
                return;
            }

            if (_rolled) return;
            _rolled = true;

            // The party member with the highest MIND checks. Being Hidden changes nothing here: the ambush comes first.
            if (PartyStealthController.RollMindCheck(ambushDetectDifficulty, out var who)) Reveal(who);
            else Spring();
        }

        void Reveal(string who)
        {
            _armed = false;
            SetVisible(true);
            SetState(State.Patrol);
            InteractionSystem.ShowMessage($"{who} senses an ambush! The enemy is revealed.");
        }

        void Spring()
        {
            SetVisible(true);
            PartyRules.EndHidden();
            _inWindow = true;
            _windowEnds = Time.time + surpriseWindowSeconds;
            _contactRaised = false;
            SetState(State.Chase);
            if (rangedAmbush) _agent.stoppingDistance = shotRange * 0.6f;
            InteractionSystem.ShowMessage("AMBUSH! Do not get touched!");
        }

        void EndWindow()
        {
            _inWindow = false;
            _rehideAt = Time.time + rehideSeconds;
            SetState(State.Return);
            InteractionSystem.ShowMessage("The ambush came to nothing.");
        }

        void TickShooting()
        {
            if (!rangedAmbush || Time.time < _nextShot) return;

            Vector3 target = _player.position;
            float nearest = float.MaxValue;
            foreach (var position in PartyPositions())
            {
                float distance = Flat(position, transform.position);
                if (distance >= nearest) continue;
                nearest = distance;
                target = position;
            }

            if (nearest > shotRange) return;

            var from = transform.position + Vector3.up * 1.3f;
            var to = target + Vector3.up * 1f;
            if (Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(_player) && !hit.transform.IsChildOf(transform) && hit.distance < nearest - 1f) return;

            _nextShot = Time.time + shotInterval;
            OverworldArrow.Fire(from, to, 18f, () =>
            {
                // The arrow lands where the target stood: anyone still there is hit.
                if (this == null || !_inWindow || _contactRaised || !PartyWithin(to, 1f)) return;
                _contactRaised = true;
                AmbushTouched?.Invoke(this);
            });
        }

        /// <summary>Shows or hides the whole group (models and colliders): an ambusher is invisible and intangible while it waits.</summary>
        void SetVisible(bool visible)
        {
            if (visible)
            {
                foreach (var r in _hiddenRenderers) if (r != null) r.enabled = true;
                foreach (var c in _hiddenColliders) if (c != null) c.enabled = true;
                _hiddenRenderers.Clear();
                _hiddenColliders.Clear();
                return;
            }

            void Hide(GameObject root)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    r.enabled = false;
                    _hiddenRenderers.Add(r);
                }

                foreach (var c in root.GetComponentsInChildren<Collider>())
                {
                    if (!c.enabled) continue;
                    c.enabled = false;
                    _hiddenColliders.Add(c);
                }
            }

            Hide(gameObject);
            var group = GetComponent<OverworldEnemyGroup>();
            if (group != null) foreach (var member in group.Members) Hide(member.gameObject);
        }

        void TickPatrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                // No route: stand at home.
                if (CurrentState == State.Return && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
                    SetState(State.Patrol);
                return;
            }

            if (_agent.pathPending) return;
            if (_agent.remainingDistance > _agent.stoppingDistance + 0.1f) return;

            // Arrived at a waypoint (or finished returning): wait, then head to the next one.
            if (CurrentState == State.Return) SetState(State.Patrol);
            _waitTimer += Time.deltaTime;
            if (_waitTimer < waitAtPoint) return;
            _waitTimer = 0f;
            _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
            _agent.SetDestination(patrolPoints[_patrolIndex].position);
        }

        bool CanSeePlayer()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 target = _player.position + Vector3.up * eyeHeight;
            Vector3 toPlayer = target - eye;
            float dist = toPlayer.magnitude;
            float radius = PartyRules.Hidden ? Mathf.Min(hiddenSenseRadius, viewRadius) : viewRadius;
            if (dist > radius) return false;

            // While already chasing, drop the cone requirement so the enemy doesn't lose you by turning.
            if (CurrentState != State.Chase && dist > closeSenseRadius)
            {
                Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
                if (flat.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) return false;
            }

            // Line of sight: the first thing the ray hits must be the player.
            if (Physics.Raycast(eye, toPlayer / dist, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
                return hit.transform.IsChildOf(_player) || hit.transform == _player;
            return true;
        }

        void CheckContact()
        {
            if (!contactEnabled || _contactRaised) return;
            if (_rehideAt > 0f) return; // a failed ambush: no fight until it has hidden again

            if (_inWindow)
            {
                // Inside the surprise window anyone touched, the leader or a follower, hands the enemies the advantage.
                if (!PartyWithin(transform.position, contactRadius)) return;
                _contactRaised = true;
                AmbushTouched?.Invoke(this);
                return;
            }

            if (!aggressive) return; // only the party's attack starts a fight with this one
            Vector3 d = _player.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > contactRadius * contactRadius) return;
            _contactRaised = true;
            PlayerContacted?.Invoke(this);
        }

        void SetState(State s)
        {
            CurrentState = s;
            _timeSinceSeen = 0f;
            if (!_agent.isOnNavMesh) return;

            switch (s)
            {
                case State.Patrol:
                    _agent.speed = patrolSpeed;
                    _agent.stoppingDistance = 0.1f;
                    if (patrolPoints != null && patrolPoints.Length > 0)
                        _agent.SetDestination(patrolPoints[_patrolIndex].position);
                    break;
                case State.Chase:
                    _agent.speed = chaseSpeed;
                    _agent.stoppingDistance = 0f;
                    break;
                case State.Ambush:
                    _agent.ResetPath();
                    break;
                case State.Return:
                    _agent.speed = patrolSpeed;
                    _agent.stoppingDistance = 0.1f;
                    _agent.SetDestination(patrolPoints != null && patrolPoints.Length > 0
                        ? patrolPoints[_patrolIndex].position
                        : _home);
                    break;
            }
            ApplyStateColor();
        }

        void ApplyStateColor()
        {
            if (stateRenderer == null) return;
            _block ??= new MaterialPropertyBlock();
            stateRenderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", CurrentState == State.Chase ? chaseColor : patrolColor);
            stateRenderer.SetPropertyBlock(_block);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            Vector3 p = transform.position + Vector3.up * 0.1f;
            Gizmos.DrawWireSphere(p, viewRadius);
            Gizmos.color = Color.yellow;
            Vector3 left = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
            Vector3 right = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
            Gizmos.DrawLine(p, p + left * viewRadius);
            Gizmos.DrawLine(p, p + right * viewRadius);

            if (patrolPoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;
                Gizmos.DrawSphere(patrolPoints[i].position, 0.2f);
                var next = patrolPoints[(i + 1) % patrolPoints.Length];
                if (next != null) Gizmos.DrawLine(patrolPoints[i].position, next.position);
            }
        }
    }
}
