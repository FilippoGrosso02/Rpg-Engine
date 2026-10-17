using UnityEngine;

namespace Game.Overworld
{
    /// <summary>Fixed-angle follow camera (Trails-in-the-Sky style): constant pitch and yaw, smoothly tracks the target.</summary>
    public class TopDownCamera : MonoBehaviour
    {
        public Transform target;
        [Range(20f, 80f)] public float pitch = 55f;
        public float yaw = 0f;
        public float distance = 11f;
        [Tooltip("Height above the target's pivot that the camera looks at.")]
        public float lookHeight = 1f;
        public float smoothTime = 0.15f;

        Vector3 _velocity;

        void LateUpdate()
        {
            if (target == null) return;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref _velocity, smoothTime);
        }

        /// <summary>Jump straight to the follow position (use after teleports / scene start).</summary>
        public void Snap()
        {
            if (target == null) return;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = DesiredPosition();
            _velocity = Vector3.zero;
        }

        Vector3 DesiredPosition()
        {
            var focus = target.position + Vector3.up * lookHeight;
            return focus - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;
        }

        void OnEnable() => Snap();
    }
}
