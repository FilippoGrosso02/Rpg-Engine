using Game.Audio;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>WASD / stick movement relative to the camera's yaw. Disabled automatically when the overworld root is deactivated.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("Camera whose yaw defines 'forward'. Falls back to Camera.main.")]
        public Transform cameraTransform;
        public float moveSpeed = 5f;
        public float turnSpeed = 720f;
        public float gravity = -20f;
        [Tooltip("Seconds between footstep sounds while walking.")]
        public float footstepInterval = 0.42f;

        CharacterController _controller;
        float _verticalVelocity;
        float _nextFootstep;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            var cam = cameraTransform != null ? cameraTransform : (Camera.main != null ? Camera.main.transform : null);
            float yaw = cam != null ? cam.eulerAngles.y : 0f;

            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Vector3 move = GetCameraRelativeMove(input, yaw);

            if (move.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            _verticalVelocity = _controller.isGrounded ? -1f : _verticalVelocity + gravity * Time.deltaTime;
            Vector3 velocity = move * moveSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            if (move.sqrMagnitude > 0.0001f && _controller.isGrounded && Time.time >= _nextFootstep)
            {
                GameAudio.Play(AudioCue.Footstep, transform.position);
                _nextFootstep = Time.time + footstepInterval;
            }
        }

        /// <summary>Moves the player instantly (CharacterController ignores direct transform writes while enabled).</summary>
        public void Teleport(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _verticalVelocity = 0f;
        }

        /// <summary>Converts 2D input (x = right, y = forward) into a world-space direction, rotated by the camera yaw. Length is at most 1.</summary>
        public static Vector3 GetCameraRelativeMove(Vector2 input, float cameraYawDegrees)
        {
            var local = new Vector3(input.x, 0f, input.y);
            if (local.sqrMagnitude > 1f) local.Normalize();
            return Quaternion.Euler(0f, cameraYawDegrees, 0f) * local;
        }
    }
}
