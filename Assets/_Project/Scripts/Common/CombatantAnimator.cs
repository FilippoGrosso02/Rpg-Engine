using Game.Data.Definitions;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game.Common
{
    /// <summary>
    /// Plays arbitrary AnimationClips on a combatant's model through a PlayableGraph, so no
    /// Animator Controller is needed and any skill/weapon clip can be dropped in from data.
    /// Layout: a locomotion mixer (idle/run, blended by <see cref="SetSpeed"/>) feeds a top
    /// mixer together with a one-shot slot used for attacks, hits and the defeat pose.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatantAnimator : MonoBehaviour
    {
        private const float BlendTime = 0.12f;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _top;
        private AnimationMixerPlayable _locomotion;
        private AnimationClipPlayable _oneShot;
        private AnimationSetDefinition _set;

        private bool _hasOneShot;
        private bool _hold;
        private float _oneShotLength;
        private float _oneShotWeight;
        private float _speed01;
        private int _comboIndex;

        public AnimationSetDefinition Set => _set;

        /// <summary>True while an attack/hit clip is playing (a held defeat pose does not count).</summary>
        public bool IsActionPlaying => _hasOneShot && !_hold;

        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

        private void OnDisable()
        {
            if (_graph.IsValid())
            {
                _graph.Stop();
            }
        }

        private void OnEnable()
        {
            if (_graph.IsValid())
            {
                _graph.Play();
            }
        }

        public void Initialize(AnimationSetDefinition set)
        {
            _set = set;
            if (set == null)
            {
                return;
            }

            var animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }

            // Combat movement is driven by the runner, never by baked root motion.
            animator.applyRootMotion = false;

            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            _graph = PlayableGraph.Create($"CombatantAnimator_{name}");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(_graph, "Animation", animator);

            _top = AnimationMixerPlayable.Create(_graph, 2);
            _locomotion = AnimationMixerPlayable.Create(_graph, 2);
            _graph.Connect(_locomotion, 0, _top, 0);
            _top.SetInputWeight(0, 1f);
            _top.SetInputWeight(1, 0f);

            ConnectLoop(set.idle, 0);
            ConnectLoop(set.run, 1);
            ApplyLocomotionWeights();

            output.SetSourcePlayable(_top);
            _graph.Play();
        }

        /// <summary>0 = idle, 1 = full run. Cheap to call every frame.</summary>
        public void SetSpeed(float speed01)
        {
            _speed01 = Mathf.Clamp01(speed01);
            ApplyLocomotionWeights();
        }

        /// <summary>Plays a one-shot action. Returns its real-time duration (0 if nothing played).</summary>
        public float Play(AnimationAction action)
        {
            if (action == null || !action.HasClip)
            {
                return 0f;
            }

            return PlayOneShot(action.clip, action.speed, false);
        }

        public float PlayGuard() => PlayOneShot(_set != null ? _set.guard : null, 1f, true);

        /// <summary>Plays a random hit reaction; returns its duration (0 if the set has none).</summary>
        public float PlayHit()
        {
            if (_set == null || _set.hits == null || _set.hits.Count == 0)
            {
                return 0f;
            }

            return PlayOneShot(_set.hits[Random.Range(0, _set.hits.Count)], 1f, false);
        }

        /// <summary>Plays the defeat clip and holds its last frame. Returns its duration.</summary>
        public float PlayDefeated() => PlayOneShot(_set != null ? _set.defeated : null, 1f, true);

        /// <summary>Drops any one-shot (including a held pose) and returns to locomotion.</summary>
        public void ClearAction()
        {
            _hold = false;
            if (_hasOneShot)
            {
                _top.DisconnectInput(1);
                _oneShot.Destroy();
                _hasOneShot = false;
                _top.SetInputWeight(0, 1f);
            }
        }

        /// <summary>Next basic attack from the list, cycling in order so repeated attacks form a combo.</summary>
        public AnimationAction NextBasicAttack(System.Collections.Generic.IList<AnimationAction> weaponClips)
        {
            var list = weaponClips != null && weaponClips.Count > 0
                ? weaponClips
                : (_set != null ? _set.basicAttacks : null);
            if (list == null || list.Count == 0)
            {
                return null;
            }

            var action = list[_comboIndex % list.Count];
            _comboIndex++;
            return action;
        }

        private float PlayOneShot(AnimationClip clip, float speed, bool hold)
        {
            if (clip == null || !_graph.IsValid())
            {
                return 0f;
            }

            if (_hasOneShot)
            {
                _top.DisconnectInput(1);
                _oneShot.Destroy();
            }

            speed = Mathf.Max(0.01f, speed);
            _oneShot = AnimationClipPlayable.Create(_graph, clip);
            _oneShot.SetSpeed(speed);
            _oneShot.SetApplyFootIK(false);
            _graph.Connect(_oneShot, 0, _top, 1);

            _hasOneShot = true;
            _hold = hold;
            _oneShotLength = clip.length / speed;
            _oneShotWeight = 0f;
            _top.SetInputWeight(1, 0f);
            return _oneShotLength;
        }

        private void ConnectLoop(AnimationClip clip, int slot)
        {
            if (clip == null)
            {
                return;
            }

            var playable = AnimationClipPlayable.Create(_graph, clip);
            _graph.Connect(playable, 0, _locomotion, slot);
        }

        private void ApplyLocomotionWeights()
        {
            if (!_locomotion.IsValid())
            {
                return;
            }

            _locomotion.SetInputWeight(0, 1f - _speed01);
            _locomotion.SetInputWeight(1, _speed01);
        }

        private void Update()
        {
            if (!_graph.IsValid() || !_hasOneShot)
            {
                return;
            }

            var time = (float)_oneShot.GetTime() / Mathf.Max(0.01f, (float)_oneShot.GetSpeed());
            var finished = time >= _oneShotLength - BlendTime;

            if (finished && _hold)
            {
                // Freeze on the last frame at full weight (defeat pose / guard stance).
                _oneShot.SetSpeed(0);
                _oneShotWeight = Mathf.MoveTowards(_oneShotWeight, 1f, Time.deltaTime / BlendTime);
            }
            else
            {
                var target = finished ? 0f : 1f;
                _oneShotWeight = Mathf.MoveTowards(_oneShotWeight, target, Time.deltaTime / BlendTime);
                if (finished && _oneShotWeight <= 0f)
                {
                    _top.DisconnectInput(1);
                    _oneShot.Destroy();
                    _hasOneShot = false;
                }
            }

            _top.SetInputWeight(1, _oneShotWeight);
            _top.SetInputWeight(0, 1f - _oneShotWeight);
        }
    }
}
