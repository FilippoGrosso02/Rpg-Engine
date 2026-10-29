using System.Collections.Generic;
using Game.Data.Registry;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// Makes an encounter's other enemies visible on the overworld: the <see cref="OverworldEnemy"/> itself is the group's leader (it
    /// stands for the encounter's first enemy) and one model per further enemy trails it on the same patrol. When the fight starts,
    /// each combatant spawns where its model stands. Added by OverworldEnemy; removed with it.
    /// </summary>
    public class OverworldEnemyGroup : MonoBehaviour
    {
        // x = right, y = behind the leader.
        static readonly Vector2[] Slots =
        {
            new Vector2(-1.3f, 1.3f), new Vector2(1.3f, 1.3f), new Vector2(0f, 2.6f), new Vector2(-1.3f, 3.9f), new Vector2(1.3f, 3.9f)
        };

        // Index i holds the model for encounter enemy i + 1; null where that enemy has no model.
        readonly List<GroupFollower> _members = new List<GroupFollower>();

        public IEnumerable<GroupFollower> Members
        {
            get { foreach (var member in _members) if (member != null) yield return member; }
        }

        public void Build(OverworldEnemy leader)
        {
            var agent = leader.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var ids = leader.encounter.enemyIds;
            for (int i = 1; i < ids.Count; i++)
            {
                var definition = DatabaseRegistry.GetEnemy(ids[i]);
                GroupFollower member = null;
                if (definition != null)
                {
                    var slot = Slots[(i - 1) % Slots.Length] + Vector2.up * ((i - 1) / Slots.Length) * 1.3f;
                    member = GroupFollower.Spawn($"{leader.name}_Member{i}", definition.battleModelPrefab, definition.animationSet, leader.transform, slot);
                    if (member != null)
                    {
                        member.leaderAgent = agent;
                        member.baseSpeed = leader.patrolSpeed;
                        member.warpDistance = 10f;
                    }
                }

                _members.Add(member);
            }
        }

        /// <summary>Positions aligned with the encounter's enemy list: the leader first, null where an enemy has no model.</summary>
        public Vector3?[] CollectPositions(int enemyCount)
        {
            var positions = new Vector3?[enemyCount];
            if (enemyCount > 0) positions[0] = transform.position;
            for (int i = 1; i < enemyCount && i - 1 < _members.Count; i++)
            {
                if (_members[i - 1] != null) positions[i] = _members[i - 1].transform.position;
            }

            return positions;
        }

        public void Freeze(bool frozen)
        {
            foreach (var member in Members) member.Freeze(frozen);
        }

        public void WarpToSlots()
        {
            foreach (var member in Members) member.WarpToSlot();
        }

        void OnDestroy()
        {
            foreach (var member in _members) if (member != null) Destroy(member.gameObject);
            _members.Clear();
        }
    }
}
