using Game.Common;
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
                PartyRoster.CycleLeader();
            }

            float full = fullRunSpeed > 0f ? fullRunSpeed : (_player != null ? _player.moveSpeed : 5f);
            var v = _controller.velocity;
            float target = Mathf.Clamp01(new Vector2(v.x, v.z).magnitude / Mathf.Max(0.01f, full));
            _blend = Mathf.MoveTowards(_blend, target, 8f * Time.deltaTime);
            _animator.SetSpeed(_blend);
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

            var model = Instantiate(actor.battleModelPrefab, transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            _currentId = leaderId;
            _animator.Initialize(actor.animationSet);
            _animator.SetSpeed(_blend);
        }
    }
}
