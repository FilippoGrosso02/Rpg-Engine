using Game.Common;
using Game.Data.Definitions;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Overworld
{
    /// <summary>Plays idle / run on an overworld enemy's model, blended by its NavMeshAgent speed.</summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(CombatantAnimator))]
    public class OverworldEnemyAnimator : MonoBehaviour
    {
        public AnimationSetDefinition animationSet;
        [Tooltip("Agent speed that counts as a full run for the blend.")]
        public float fullRunSpeed = 3f;

        NavMeshAgent _agent;
        CombatantAnimator _animator;
        float _blend;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<CombatantAnimator>();
        }

        void Start() => _animator.Initialize(animationSet);

        void Update()
        {
            float target = Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(0.01f, fullRunSpeed));
            _blend = Mathf.MoveTowards(_blend, target, 8f * Time.deltaTime);
            _animator.SetSpeed(_blend);
        }
    }
}
