using System.Collections.Generic;
using Game.Data.Registry;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// Shows the party members that are not the leader on the overworld, trailing the player. When a fight starts they are where they
    /// stand, and they always take part. <see cref="Following"/> off (the menu's "party follow" button) makes them hold their
    /// ground instead, but never further from the leader than <see cref="encounterRange"/>, so every fight still finds the whole party.
    /// </summary>
    public class PartyFollowers : MonoBehaviour
    {
        /// <summary>Menu switch. Session-only, like the rest of the overworld state for now.</summary>
        public static bool Following { get; set; } = true;

        [Tooltip("Furthest a party member may be from the leader while the party is told to stay put.")]
        public float encounterRange = 6f;

        [Tooltip("Distance behind the leader of the first follower.")]
        public float firstFollowerDistance = 2.6f;

        [Tooltip("Extra distance behind the one in front, per follower.")]
        public float followerSpacing = 2f;

        [Tooltip("How far a follower may drift sideways / back and forth from its place in the line while the leader walks.")]
        public Vector2 wander = new Vector2(0.9f, 0.7f);

        readonly Dictionary<string, GroupFollower> _followers = new Dictionary<string, GroupFollower>();
        PlayerController _player;

        [Tooltip("A leader switch that has not finished after this long (the walker is stuck) happens anyway.")]
        public float switchTimeout = 6f;

        [Tooltip("During a leader switch the old leader must be this far from the player before the next hero may arrive.")]
        public float clearDistance = 1.8f;

        /// <summary>Every model on the overworld that stands in for a non-leader (including a leader who is stepping back).</summary>
        public IEnumerable<GroupFollower> Followers
        {
            get
            {
                foreach (var follower in _followers.Values) yield return follower;
                if (_steppingBack != null) yield return _steppingBack;
            }
        }

        /// <summary>True from pressing the switch until the next hero has taken the lead.</summary>
        public bool SwitchInProgress { get; private set; }

        string _oldLeaderId;
        GroupFollower _steppingBack;
        GroupFollower _walker;

        public void Init(PlayerController player)
        {
            _player = player;
            Sync();
        }

        void OnEnable() => PartyRoster.Changed += Sync;

        void OnDisable() => PartyRoster.Changed -= Sync;

        /// <summary>
        /// Switching the leader is a hand-over on foot: the current leader steps out of the player and walks back to the end of the line
        /// (the player cannot move meanwhile), and only once they are clear does the next hero walk up and take the lead, at which point
        /// the player's model changes. Returns false when there is nobody to hand over to, so the caller can switch at once.
        /// </summary>
        public bool RequestSwitch()
        {
            if (SwitchInProgress) return true;
            if (_player == null || PartyRoster.Count < 2) return false;
            if (!_followers.TryGetValue(PartyRoster.Members[1], out var walker) || walker == null) return false;

            var oldLeader = DatabaseRegistry.GetActor(PartyRoster.Members[0]);
            if (oldLeader == null) return false;

            // The old leader appears at the player's spot and heads for the end of the line (walking even if the party is told to hold).
            var stepping = GroupFollower.Spawn($"Follower_{oldLeader.id}", oldLeader.battleModelPrefab, oldLeader.animationSet, _player.transform, Vector2.zero);
            if (stepping == null) return false;
            Configure(stepping);
            stepping.follow = () => true;
            stepping.slot = LineSlot(PartyRoster.Count - 2);

            _oldLeaderId = oldLeader.id;
            _steppingBack = stepping;
            _walker = walker;
            SwitchInProgress = true;
            _player.movementLocked = true;
            _player.GetComponent<PlayerModelSwitcher>()?.SetModelVisible(false);
            StartCoroutine(RunSwitch(walker));
            return true;
        }

        System.Collections.IEnumerator RunSwitch(GroupFollower walker)
        {
            float waited = 0f;

            // 1. The old leader gets out of the way.
            while (SwitchInProgress && _steppingBack != null && waited < switchTimeout * 0.5f
                   && FlatDistance(_steppingBack.transform.position, _player.transform.position) < clearDistance)
            {
                if (_steppingBack.enabled) waited += Time.deltaTime; // not while the world is frozen for a fight
                yield return null;
            }

            if (!SwitchInProgress) yield break;

            // 2. The next hero walks up and takes the lead. Their model is made ready now, so the swap is only an activation.
            _player.GetComponent<PlayerModelSwitcher>()?.Preload(PartyRoster.Members[1]);
            walker.goTo = _player.transform;
            walker.arrived = CompleteSwitch;
            while (SwitchInProgress && waited < switchTimeout)
            {
                if (walker != null && walker.enabled) waited += Time.deltaTime;
                yield return null;
            }

            if (SwitchInProgress) CompleteSwitch();
        }

        void CompleteSwitch()
        {
            if (!SwitchInProgress) return;
            SwitchInProgress = false;

            var stepping = _steppingBack;
            _steppingBack = null;
            if (stepping != null && PartyRoster.Count >= 2)
            {
                stepping.follow = () => Following;
                _followers[_oldLeaderId] = stepping; // already in place: Sync only has to update its slot
            }
            else if (stepping != null)
            {
                Destroy(stepping.gameObject);
            }

            _oldLeaderId = null;

            // Hand the walker's pose over to the player so the swap is not visible: same facing, same stride.
            var walker = _walker;
            _walker = null;
            var switcher = _player != null ? _player.GetComponent<PlayerModelSwitcher>() : null;
            if (walker != null && _player != null)
            {
                _player.transform.rotation = Quaternion.Euler(0f, walker.transform.eulerAngles.y, 0f);
                if (switcher != null) switcher.SetBlend(walker.RunBlend);
            }

            if (PartyRoster.Count >= 2) PartyRoster.CycleLeader(); // raises Changed: Sync below and the player's model both follow
            EndHandOver();
        }

        /// <summary>The roster changed some other way mid-hand-over: the plan no longer applies, so the old leader simply stays leader.</summary>
        void AbortSwitch()
        {
            SwitchInProgress = false;
            if (_steppingBack != null) Destroy(_steppingBack.gameObject);
            _steppingBack = null;
            _walker = null;
            _oldLeaderId = null;
            foreach (var f in _followers.Values) if (f != null) { f.goTo = null; f.arrived = null; }
            if (_player != null) _player.GetComponent<PlayerModelSwitcher>()?.ClearPreload();
            EndHandOver();
        }

        void EndHandOver()
        {
            if (_player == null) return;
            _player.movementLocked = false;
            _player.GetComponent<PlayerModelSwitcher>()?.SetModelVisible(true);
        }

        void OnDestroy()
        {
            foreach (var follower in _followers.Values) if (follower != null) Destroy(follower.gameObject);
            if (_steppingBack != null) Destroy(_steppingBack.gameObject);
            _followers.Clear();
        }

        /// <summary>A single file behind the leader: x = right (none), y = behind.</summary>
        Vector2 LineSlot(int index) => new Vector2(0f, firstFollowerDistance + index * followerSpacing);

        void Configure(GroupFollower follower)
        {
            follower.baseSpeed = _player.moveSpeed;
            follower.wander = wander;
            follower.follow = () => Following;
            follower.leashRange = encounterRange;
            follower.warpDistance = encounterRange * 2.5f;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>Brings the models in line with the roster: members who left (or became the leader) disappear, new ones appear at the leader.</summary>
        void Sync()
        {
            if (_player == null) return;

            if (SwitchInProgress) AbortSwitch();

            var wanted = new List<string>();
            for (int i = 1; i < PartyRoster.Count; i++) wanted.Add(PartyRoster.Members[i]);

            var gone = new List<string>();
            foreach (var id in _followers.Keys) if (!wanted.Contains(id)) gone.Add(id);
            foreach (var id in gone)
            {
                if (_followers[id] != null) Destroy(_followers[id].gameObject);
                _followers.Remove(id);
            }

            for (int i = 0; i < wanted.Count; i++)
            {
                var slot = LineSlot(i);
                if (_followers.TryGetValue(wanted[i], out var existing))
                {
                    if (existing != null) existing.slot = slot;
                    continue;
                }

                var actor = DatabaseRegistry.GetActor(wanted[i]);
                if (actor == null) continue;

                var follower = GroupFollower.Spawn($"Follower_{wanted[i]}", actor.battleModelPrefab, actor.animationSet, _player.transform, slot);
                if (follower == null) continue;

                Configure(follower);
                _followers[wanted[i]] = follower;
            }
        }

        /// <summary>Positions in roster order (leader first), null where a member has no model (so the fight places them itself).</summary>
        public Vector3?[] CollectPositions()
        {
            var positions = new Vector3?[PartyRoster.Count];
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == 0) positions[i] = _steppingBack != null ? _steppingBack.transform.position : _player != null ? _player.transform.position : (Vector3?)null;
                else if (_followers.TryGetValue(PartyRoster.Members[i], out var follower) && follower != null) positions[i] = follower.transform.position;
            }

            return positions;
        }
    }
}
