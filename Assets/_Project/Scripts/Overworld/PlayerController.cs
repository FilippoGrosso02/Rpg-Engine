using Game.Audio;
using UnityEngine;
using UnityEngine.AI;

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
        [Tooltip("Ignore movement input (e.g. while the party swaps its leader).")]
        public bool movementLocked;

        CharacterController _controller;
        Vector3[] _autoCorners;
        int _autoIndex;
        float _autoStop;
        float _autoDeadline;
        System.Action<bool> _autoDone;
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

            Vector3 move;
            if (_autoCorners != null)
            {
                move = AutoWalkMove();
            }
            else
            {
                var input = movementLocked ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                move = GetCameraRelativeMove(input, yaw);
            }

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

        /// <summary>
        /// Stops input and takes the player's collider out of the world. Used while a fight plays out on the overworld: the party's
        /// combatants stand where the player did, and this capsule would otherwise sit in front of every aiming ray.
        /// </summary>
        public void SetFrozen(bool frozen)
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (frozen && _autoCorners != null) FinishAutoWalk(false);
            enabled = !frozen;
            _controller.enabled = !frozen;
            _verticalVelocity = 0f;
        }

        /// <summary>True while the player is walking itself to a point (<see cref="WalkTo"/>); input is ignored meanwhile.</summary>
        public bool AutoWalking => _autoCorners != null;

        /// <summary>
        /// Walks the player along the NavMesh to within <paramref name="stopDistance"/> of <paramref name="destination"/>, ignoring input.
        /// <paramref name="done"/> gets true on arrival, false when cancelled or timed out. Returns false (and does nothing) if there is no way there.
        /// </summary>
        public bool WalkTo(Vector3 destination, float stopDistance, System.Action<bool> done)
        {
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(transform.position, out var from, 2f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(destination, out var to, 2f, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete
                || path.corners.Length == 0)
            {
                return false;
            }

            _autoCorners = path.corners;
            _autoIndex = 0;
            _autoStop = stopDistance;
            _autoDone = done;
            _autoDeadline = Time.time + 12f;
            return true;
        }

        public void CancelAutoWalk()
        {
            if (_autoCorners != null) FinishAutoWalk(false);
        }

        void FinishAutoWalk(bool arrived)
        {
            var callback = _autoDone;
            _autoCorners = null;
            _autoDone = null;
            callback?.Invoke(arrived);
        }

        /// <summary>The direction to walk this frame along the planned path (zero once it ends).</summary>
        Vector3 AutoWalkMove()
        {
            var position = transform.position;
            var toEnd = _autoCorners[_autoCorners.Length - 1] - position;
            toEnd.y = 0f;
            if (toEnd.magnitude <= _autoStop)
            {
                FinishAutoWalk(true);
                return Vector3.zero;
            }

            if (Time.time > _autoDeadline)
            {
                FinishAutoWalk(false);
                return Vector3.zero;
            }

            while (_autoIndex < _autoCorners.Length - 1)
            {
                var toCorner = _autoCorners[_autoIndex] - position;
                toCorner.y = 0f;
                if (toCorner.magnitude >= 0.3f) break;
                _autoIndex++;
            }

            var direction = _autoCorners[_autoIndex] - position;
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.zero : direction.normalized;
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
