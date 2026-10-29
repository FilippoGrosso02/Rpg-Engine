using System.Collections.Generic;
using Game.Common;
using Game.Data.Definitions;
using Game.Data.Registry;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// Shows the first party member's model on the overworld Player and animates it (idle / run from the
    /// controller's speed). Q cycles the party leader (<see cref="PartyRoster.CycleLeader"/>), which swaps the model;
    /// changes made elsewhere (e.g. the F2 roster panel) are picked up too.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(CombatantAnimator))]
    public class PlayerModelSwitcher : MonoBehaviour
    {
        public KeyCode cycleKey = KeyCode.Q;
        [Tooltip("Speed that counts as a full run for the animation blend. Defaults to the PlayerController's moveSpeed.")]
        public float fullRunSpeed;

        CharacterController _controller;
        CombatantAnimator _animator;
        PlayerController _player;
        string _currentId;
        float _blend;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponent<CombatantAnimator>();
            _player = GetComponent<PlayerController>();
        }

        void OnEnable()
        {
            PartyRoster.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => PartyRoster.Changed -= Refresh;

        void Start() => Refresh();

        void Update()
        {
            if (Input.GetKeyDown(cycleKey))
            {
                // With followers the next hero has to walk up first; alone (or without models) the switch is immediate.
                var followers = FindAnyObjectByType<PartyFollowers>();
                if (followers == null || !followers.RequestSwitch()) PartyRoster.CycleLeader();
            }

            float full = fullRunSpeed > 0f ? fullRunSpeed : (_player != null ? _player.moveSpeed : 5f);
            var v = _controller.velocity;
            float target = Mathf.Clamp01(new Vector2(v.x, v.z).magnitude / Mathf.Max(0.01f, full));
            _blend = Mathf.MoveTowards(_blend, target, 8f * Time.deltaTime);
            _animator.SetSpeed(_blend);
        }

        GameObject _preloaded;
        string _preloadedId;

        /// <summary>
        /// Creates a hero's model ahead of a leader switch (hidden), so the swap itself is a cheap activation instead of an
        /// instantiate on the very frame the new leader takes over.
        /// </summary>
        public void Preload(string actorId)
        {
            ClearPreload();
            var actor = DatabaseRegistry.GetActor(actorId);
            if (actor == null || actor.battleModelPrefab == null) return;

            _preloaded = Instantiate(actor.battleModelPrefab, transform);
            _preloaded.name = "Model (next)";
            _preloaded.transform.localPosition = Vector3.zero;
            _preloaded.transform.localRotation = Quaternion.identity;
            _preloaded.SetActive(false);
            _preloadedId = actorId;
        }

        public void ClearPreload()
        {
            if (_preloaded != null) Destroy(_preloaded);
            _preloaded = null;
            _preloadedId = null;
        }

        /// <summary>Continues a run animation that was already playing (a hero who walked up to take the lead).</summary>
        public void SetBlend(float blend) => _blend = Mathf.Clamp01(blend);

        /// <summary>
        /// Plays the leader's basic attack, the same clip a fight would use (the equipped weapon's list first, then the character's
        /// own set, cycling through combos). Returns how long to wait before the blow has landed, or 0 if there is nothing to play.
        /// </summary>
        public float PlaySwing(float followThrough = 0.5f)
        {
            if (_animator == null || string.IsNullOrEmpty(_currentId)) return 0f;

            var actor = DatabaseRegistry.GetActor(_currentId);
            List<AnimationAction> weaponClips = null;
            var weaponId = actor != null ? Loadout.GetWeaponId(actor) : null;
            var weapon = string.IsNullOrEmpty(weaponId) ? null : DatabaseRegistry.GetWeapon(weaponId);
            if (weapon != null && weapon.basicAttackAnimations != null && weapon.basicAttackAnimations.Count > 0)
                weaponClips = weapon.basicAttackAnimations;

            var action = _animator.NextBasicAttack(weaponClips);
            if (action == null || _animator.Play(action) <= 0f) return 0f;

            // Past the moment of impact, most of the way through the follow-through.
            return Mathf.Lerp(action.ImpactTime, action.Duration, Mathf.Clamp01(followThrough));
        }

        /// <summary>Hides or shows the current model while the leader is mid-hand-over (the old leader walks off as a separate model).</summary>
        public void SetModelVisible(bool visible)
        {
            var model = transform.Find("Model");
            if (model != null) model.gameObject.SetActive(visible);
        }

        void Refresh()
        {
            if (_animator == null || PartyRoster.Count == 0)
            {
                return;
            }

            var leaderId = PartyRoster.Members[0];
            if (leaderId == _currentId)
            {
                return;
            }

            var actor = DatabaseRegistry.GetActor(leaderId);
            if (actor == null || actor.battleModelPrefab == null)
            {
                return;
            }

            var old = transform.Find("Model");
            if (old != null)
            {
                old.name = "Model (old)";
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }

            GameObject model;
            if (_preloaded != null && _preloadedId == leaderId)
            {
                model = _preloaded;
                model.SetActive(true);
                _preloaded = null;
                _preloadedId = null;
            }
            else
            {
                model = Instantiate(actor.battleModelPrefab, transform);
            }

            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            _currentId = leaderId;
            _animator.Initialize(actor.animationSet);
            _animator.SetSpeed(_blend);
        }
    }
}
