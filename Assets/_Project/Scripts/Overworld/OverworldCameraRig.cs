using Unity.Cinemachine;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// Close third-person follow camera for the overworld, built on Cinemachine: one CinemachineCamera orbiting the player
    /// (mouse look comes from the CinemachineInputAxisController on the same object). The cursor is locked while exploring;
    /// hold Alt to free it. The cursor is also freed (and mouse look stopped) while time is frozen, i.e. while the pause menu is open. Lives under OverworldRoot, so it is
    /// switched off (and the cursor released) for the duration of a battle.
    /// </summary>
    public class OverworldCameraRig : MonoBehaviour
    {
        [SerializeField] CinemachineCamera virtualCamera;
        [SerializeField] CinemachineOrbitalFollow orbit;
        [SerializeField] CinemachineInputAxisController lookInput;

        [Tooltip("Where the character stands on screen while aiming (0 = centre, +-0.5 = screen edge, x right, y up): a little to the bottom left, like a third-person shooter.")]
        [SerializeField] Vector2 aimScreenPosition = new Vector2(-0.12f, -0.16f);

        [Tooltip("Height above the character's feet that the camera orbits and looks at: the head, or a little above it.")]
        [SerializeField] float lookAtHeight = 1.85f;

        [Tooltip("How quickly the camera slides between exploring and aiming (higher = faster).")]
        [SerializeField] float aimBlendSpeed = 7f;

        CinemachineRotationComposer _composer;
        Vector2 _exploreScreenPosition;
        bool _composerCaptured;
        bool _aiming;
        float _aimAmount; // 0 = exploring, 1 = aiming

        /// <summary>Holding the aim button: the character slides to the bottom left of the screen so the view ahead is clear. Releasing slides it back.</summary>
        public void SetAiming(bool aiming) => _aiming = aiming;

        void OnEnable()
        {
            if (!_composerCaptured && virtualCamera != null && virtualCamera.TryGetComponent(out _composer))
            {
                _exploreScreenPosition = _composer.Composition.ScreenPosition; // the authored, centred framing
                _composerCaptured = true;
            }

            if (orbit != null) orbit.TargetOffset = new Vector3(orbit.TargetOffset.x, lookAtHeight, orbit.TargetOffset.z);
            if (_composer != null) _composer.TargetOffset = new Vector3(_composer.TargetOffset.x, lookAtHeight, _composer.TargetOffset.z);

            _aiming = false;
            _aimAmount = 0f;
            ApplyAimFraming();
            SnapBehind();
        }

        void OnDisable()
        {
            _aiming = false;
            _aimAmount = 0f;
            ApplyAimFraming();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void ApplyAimFraming()
        {
            if (!_composerCaptured || _composer == null) return;
            float t = Mathf.SmoothStep(0f, 1f, _aimAmount);
            _composer.Composition.ScreenPosition = Vector2.Lerp(_exploreScreenPosition, aimScreenPosition, t);
        }

        void Update()
        {
            _aimAmount = Mathf.MoveTowards(_aimAmount, _aiming ? 1f : 0f, aimBlendSpeed * Time.unscaledDeltaTime);
            ApplyAimFraming();

            bool free = Time.timeScale <= 0f || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
            if (lookInput != null && lookInput.enabled == free) lookInput.enabled = !free;
        }

        /// <summary>Stops steering and releases the cursor so another camera (a fight on the overworld) can take over; it keeps its pose.</summary>
        public void Suspend()
        {
            if (lookInput != null) lookInput.enabled = false;
            enabled = false;
        }

        /// <summary>Takes control again and swings back behind the player.</summary>
        public void Resume() => enabled = true;

        /// <summary>Swings the camera behind the followed target and jumps straight to it (use after teleports / scene start).</summary>
        public void SnapBehind()
        {
            if (virtualCamera == null || orbit == null || virtualCamera.Follow == null) return;
            orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, virtualCamera.Follow.eulerAngles.y);
            virtualCamera.PreviousStateIsValid = false;
        }
    }
}
