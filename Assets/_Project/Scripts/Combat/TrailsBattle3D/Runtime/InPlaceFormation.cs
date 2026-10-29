using System;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Lays out both sides of an on-map fight around the two points where the overworld player and enemy group stand.
    /// Pure maths plus an injectable "walkable" clamp, so it can be tested without a NavMesh.
    /// </summary>
    public static class InPlaceFormation
    {
        /// <summary>Contact starts a fight with the groups nearly touching; they are pushed apart to at least this distance.</summary>
        public const float MinGroupGap = 4.5f;

        private const float RowDepth = 2.2f;

        // (right, forward) relative to the group's anchor. Party: forward points at the enemy, so negative is "behind the leader".
        private static readonly Vector2[] PartySlots =
        {
            new Vector2(0f, 0f), new Vector2(-1.6f, -1f), new Vector2(1.6f, -1f), new Vector2(-0.9f, -RowDepth), new Vector2(0.9f, -RowDepth)
        };

        // Enemies fan out away from the party, so positive forward is "further back in their formation".
        private static readonly Vector2[] EnemySlots =
        {
            new Vector2(0f, 0f), new Vector2(1.6f, 1f), new Vector2(-1.6f, 1f), new Vector2(0.9f, RowDepth), new Vector2(-0.9f, RowDepth)
        };

        public class Layout
        {
            public Vector3[] party;
            public Vector3[] enemies;

            /// <summary>Direction the party faces (towards the enemy group). Enemies face the opposite way.</summary>
            public Vector3 forward;
        }

        /// <summary>
        /// <paramref name="walkable"/> takes (from, wanted) and returns the nearest reachable point towards wanted; it must return
        /// a point on walkable ground. <see cref="ClampToNavMesh"/> is the real one.
        /// </summary>
        public static Layout Compute(Vector3 partyPosition, Vector3 enemyPosition, Vector3 fallbackForward, int partyCount, int enemyCount,
            Func<Vector3, Vector3, Vector3> walkable)
        {
            var flat = new Vector3(enemyPosition.x - partyPosition.x, 0f, enemyPosition.z - partyPosition.z);
            float distance = flat.magnitude;
            var forward = distance > 0.01f ? flat / distance : Flatten(fallbackForward);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            var right = Vector3.Cross(Vector3.up, forward);

            // Make room: back the party off first, then give whatever it could not back off to the enemies.
            var partyAnchor = partyPosition;
            var enemyAnchor = enemyPosition;
            if (distance < MinGroupGap)
            {
                float missing = MinGroupGap - distance;
                partyAnchor = walkable(partyPosition, partyPosition - forward * (missing * 0.5f));
                float moved = Vector3.Distance(partyPosition, partyAnchor);
                float remaining = Mathf.Max(0f, missing - moved);
                enemyAnchor = walkable(enemyPosition, enemyPosition + forward * remaining);
            }

            var layout = new Layout
            {
                forward = forward,
                party = new Vector3[partyCount],
                enemies = new Vector3[enemyCount]
            };

            for (int i = 0; i < partyCount; i++)
            {
                layout.party[i] = Slot(partyAnchor, right, forward, PartySlots, i, -1f, walkable);
            }

            for (int i = 0; i < enemyCount; i++)
            {
                layout.enemies[i] = Slot(enemyAnchor, right, forward, EnemySlots, i, 1f, walkable);
            }

            return layout;
        }

        private static Vector3 Slot(Vector3 anchor, Vector3 right, Vector3 forward, Vector2[] table, int index, float rowDirection,
            Func<Vector3, Vector3, Vector3> walkable)
        {
            var offset = table[index % table.Length];
            float extraRows = (index / table.Length) * RowDepth * rowDirection;
            var wanted = anchor + right * offset.x + forward * (offset.y + extraRows);
            return index == 0 ? anchor : walkable(anchor, wanted);
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? v : v.normalized;
        }

        /// <summary>Walks from <paramref name="from"/> towards <paramref name="wanted"/> over the NavMesh and stops at its edge (walls, drops).</summary>
        public static Vector3 ClampToNavMesh(Vector3 from, Vector3 wanted)
        {
            if (!NavMesh.SamplePosition(from, out var start, 2f, NavMesh.AllAreas))
            {
                return from;
            }

            if (NavMesh.Raycast(start.position, wanted, out var edge, NavMesh.AllAreas))
            {
                wanted = edge.position;
            }

            return NavMesh.SamplePosition(wanted, out var snapped, 1.5f, NavMesh.AllAreas) ? snapped.position : start.position;
        }
    }
}
