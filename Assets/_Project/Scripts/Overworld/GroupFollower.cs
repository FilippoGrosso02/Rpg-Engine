using System;
using Game.Common;
using Game.Data.Definitions;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Overworld
{
    /// <summary>
    /// A character model that trails a leader on the overworld: a party member behind the player, or an enemy behind its group's
    /// leader. Its position when a fight starts is where its combatant spawns.
    /// While <see cref="follow"/> says false it holds its ground, but never lets the distance to the leader exceed
    /// <see cref="leashRange"/> (it walks back inside the range), and it warps to its slot if it ends up far beyond it.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(CombatantAnimator))]
    public class GroupFollower : MonoBehaviour
    {
        public Transform leader;

        [Tooltip("Optional: the leader's agent. Its speed is then used as the pace to keep up with.")]
        public NavMeshAgent leaderAgent;

        [Tooltip("Pace to keep up with when there is no leader agent (the player's walk speed).")]
        public float baseSpeed = 5f;

        [Tooltip("Where to stand relative to the leader's heading: x = right, y = behind.")]
        public Vector2 slot;

        [Tooltip("Furthest this follower may be from the leader while holding its ground.")]
        public float leashRange = 6f;

        [Tooltip("Beyond this distance from the leader the follower warps to its slot instead of walking.")]
        public float warpDistance = 15f;

        [Tooltip("How far the slot drifts sideways (x) and back/forth (y) around its nominal place, so a column does not look ruled. 0 = exact slot.")]
        public Vector2 wander = Vector2.zero;

        [Tooltip("Drift progress per metre the leader walks. The drift only changes while the leader moves, so followers settle when it stops.")]
        public float wanderPerMetre = 0.18f;

        /// <summary>While set, the follower ignores its slot and leash and hurries to this transform; <see cref="arrived"/> fires once it is there.</summary>
        [HideInInspector] public Transform goTo;

        [HideInInspector] public float arriveDistance = 0.15f;

        /// <summary>How much of a run the model is playing right now (0 idle, 1 full run).</summary>
        public float RunBlend => _blend;

        [HideInInspector] public Action arrived;

        /// <summary>Asked every frame: true = trail the leader, false = hold position (within the leash).</summary>
        public Func<bool> follow = () => true;

        NavMeshAgent _agent;
        CombatantAnimator _animator;
        float _blend;
        float _noiseTime;
        float _noiseSeed;
        Vector3 _lastLeaderPosition;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<CombatantAnimator>();
            _noiseSeed = UnityEngine.Random.Range(0f, 100f);
        }

        void Start()
        {
            if (leader != null) _lastLeaderPosition = leader.position;
        }

        void Update()
        {
            if (leader == null || !_agent.isOnNavMesh) return;

            _noiseTime += FlatDistance(leader.position, _lastLeaderPosition) * wanderPerMetre;
            _lastLeaderPosition = leader.position;

            float pace = leaderAgent != null ? leaderAgent.speed : baseSpeed;
            float leaderDistance = FlatDistance(transform.position, leader.position);
            var wanted = SlotPoint();

            _agent.stoppingDistance = goTo != null ? 0f : 0.15f;

            if (goTo != null)
            {
                // Hurry while far, ease off over the last metres so it glides to a stop on the spot instead of braking short of it.
                float remaining = FlatDistance(transform.position, goTo.position);
                _agent.speed = Mathf.Lerp(pace * 0.5f, pace * 1.5f, Mathf.Clamp01(remaining / 2.5f));
                _agent.SetDestination(goTo.position);
                bool there = remaining <= arriveDistance
                             || (!_agent.pathPending && _agent.remainingDistance <= 0.05f && remaining < 1f); // destination snapped to the mesh edge
                if (there)
                {
                    var callback = arrived;
                    goTo = null;
                    arrived = null;
                    callback?.Invoke();
                }
            }
            else if (leaderDistance > warpDistance)
            {
                _agent.Warp(wanted);
            }
            else if (follow == null || follow())
            {
                float away = FlatDistance(transform.position, wanted);
                if (away > 0.4f)
                {
                    // Hurry when far behind, match the leader's pace when close, so it settles instead of oscillating.
                    _agent.speed = pace * (1f + Mathf.Clamp01((away - 0.6f) / 3f) * 0.6f);
                    _agent.SetDestination(wanted);
                }
                else if (_agent.hasPath)
                {
                    _agent.ResetPath();
                }
            }
            else if (leaderDistance > leashRange)
            {
                var outward = transform.position - leader.position;
                outward.y = 0f;
                var target = leader.position + outward.normalized * Mathf.Max(0f, leashRange - 1f);
                _agent.speed = pace * 1.3f;
                _agent.SetDestination(target);
            }
            else if (_agent.hasPath)
            {
                _agent.ResetPath();
            }

            float run = Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(0.01f, pace));
            _blend = Mathf.MoveTowards(_blend, run, 8f * Time.deltaTime);
            _animator.SetSpeed(_blend);
        }

        /// <summary>Stops (or resumes) walking. The model is expected to be hidden by the caller while frozen.</summary>
        public void Freeze(bool frozen)
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            enabled = !frozen;
            if (!_agent.isOnNavMesh) return;
            _agent.isStopped = frozen;
            if (frozen) _agent.velocity = Vector3.zero;
        }

        /// <summary>Instantly moves onto the NavMesh point nearest <paramref name="position"/>.</summary>
        public void WarpTo(Vector3 position)
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (NavMesh.SamplePosition(position, out var hit, 2f, NavMesh.AllAreas)) _agent.Warp(hit.position);
        }

        /// <summary>Instantly moves to this follower's slot behind the leader (used when the leader was moved or reset).</summary>
        public void WarpToSlot()
        {
            if (leader != null && _agent != null && _agent.isOnNavMesh) _agent.Warp(SlotPoint());
        }

        Vector3 SlotPoint()
        {
            var drift = new Vector2(
                (Mathf.PerlinNoise(_noiseSeed, _noiseTime) - 0.5f) * 2f * wander.x,
                (Mathf.PerlinNoise(_noiseSeed + 50f, _noiseTime) - 0.5f) * 2f * wander.y);
            return SlotPoint(leader, slot + drift);
        }

        /// <summary>The slot's world position, snapped to the NavMesh (the leader's own position when there is none nearby).</summary>
        public static Vector3 SlotPoint(Transform leader, Vector2 slot)
        {
            var offset = Quaternion.Euler(0f, leader.eulerAngles.y, 0f) * new Vector3(slot.x, 0f, -slot.y);
            var point = leader.position + offset;
            return NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas) ? hit.position : leader.position;
        }

        /// <summary>Builds a follower: an agent root with the character's model as a child. Null if there is no walkable ground at the slot.</summary>
        public static GroupFollower Spawn(string objectName, GameObject modelPrefab, AnimationSetDefinition animationSet, Transform leader, Vector2 slot)
        {
            if (modelPrefab == null || leader == null) return null;

            var point = SlotPoint(leader, slot);
            if (!NavMesh.SamplePosition(point, out var onMesh, 2f, NavMesh.AllAreas)) return null;

            var root = new GameObject(objectName);
            root.transform.SetPositionAndRotation(onMesh.position, Quaternion.Euler(0f, leader.eulerAngles.y, 0f));

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.acceleration = 14f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.15f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var animator = root.AddComponent<CombatantAnimator>();
            var model = Instantiate(modelPrefab, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // A follower is only a stand-in: it must not block enemy sight lines or an aiming ray.
            foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;

            animator.Initialize(animationSet);

            var follower = root.AddComponent<GroupFollower>();
            follower.leader = leader;
            follower.slot = slot;
            return follower;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
