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
    }
}
