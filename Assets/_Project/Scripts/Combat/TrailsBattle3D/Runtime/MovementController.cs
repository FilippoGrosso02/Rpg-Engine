using UnityEngine;
using UnityEngine.AI;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Clamps a requested move to the unit's moveRange and snaps it onto the arena's baked
    /// NavMesh. No custom collision/clamping math — NavMesh.SamplePosition does both jobs.
    /// </summary>
    public static class MovementController
    {
        public static bool TryGetMovePosition(Vector3 currentPosition, Vector3 desiredPosition, float moveRange, out Vector3 result)
        {
            var offset = desiredPosition - currentPosition;
            var distance = offset.magnitude;
            var clampedTarget = distance > moveRange
                ? currentPosition + offset.normalized * moveRange
                : desiredPosition;

            if (NavMesh.SamplePosition(clampedTarget, out var hit, moveRange + 0.5f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = currentPosition;
            return false;
        }

        /// <summary>
        /// One frame of free (WASD) movement. <paramref name="delta"/> is the wanted displacement this frame;
        /// the result stays inside the move circle of radius <paramref name="moveRange"/> around the turn-start
        /// <paramref name="anchor"/> (sliding along its edge rather than stopping dead), slides along NavMesh
        /// edges (walls, columns, platform drops) and is snapped to the walkable surface so ramps work.
        /// Returns false if there is nowhere to go.
        /// </summary>
        public static bool TryStep(Vector3 current, Vector3 delta, Vector3 anchor, float moveRange, out Vector3 result)
        {
            result = current;
            delta.y = 0f;
            if (delta.sqrMagnitude < 1e-8f)
            {
                return false;
            }

            var target = current + delta;

            var fromAnchor = new Vector3(target.x - anchor.x, 0f, target.z - anchor.z);
            if (fromAnchor.magnitude > moveRange)
            {
                fromAnchor = fromAnchor.normalized * moveRange;
                target = new Vector3(anchor.x + fromAnchor.x, current.y, anchor.z + fromAnchor.z);
            }

            if (NavMesh.Raycast(current, target, out var blocked, NavMesh.AllAreas))
            {
                var remaining = target - blocked.position;
                remaining.y = 0f;
                var normal = blocked.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude > 1e-6f)
                {
                    normal.Normalize();
                    remaining -= Vector3.Dot(remaining, normal) * normal;
                }

                target = blocked.position + remaining;
                if (NavMesh.Raycast(blocked.position, target, out var blockedAgain, NavMesh.AllAreas))
                {
                    target = blockedAgain.position;
                }
            }

            if (!NavMesh.SamplePosition(target, out var snapped, 0.6f, NavMesh.AllAreas))
            {
                return false;
            }

            if ((snapped.position - current).sqrMagnitude < 1e-8f)
            {
                return false;
            }

            result = snapped.position;
            return true;
        }
    }
}
