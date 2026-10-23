using Unity.Cinemachine;
using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Close third-person camera for battles, built on Cinemachine: one CinemachineCamera that orbits the
    /// follow target (mouse look comes from the CinemachineInputAxisController on the same object, which
    /// reads the legacy "Mouse X"/"Mouse Y" axes). The runner points it at whichever ally is acting and
    /// simply leaves it there during enemy turns.
    /// </summary>
    public class BattleCameraRig : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private CinemachineOrbitalFollow orbit;
        [SerializeField] private CinemachineInputAxisController lookInput;

        [Tooltip("Degrees per second when rotating the camera with Q / E.")]
        [SerializeField] private float keyboardRotateSpeed = 110f;

        public Transform Followed => virtualCamera != null ? virtualCamera.Follow : null;

        public void Follow(Transform target)
        {
            if (virtualCamera == null || target == null)
            {
                return;
            }

            virtualCamera.Follow = target;
            virtualCamera.LookAt = target;
        }

        /// <summary>Swings the camera behind <paramref name="from"/> so it looks towards <paramref name="to"/>.</summary>
        public void FaceTowards(Vector3 from, Vector3 to)
        {
            if (orbit == null)
            {
                return;
            }

            var flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            if (flat.sqrMagnitude < 0.0001f)
            {
                return;
            }

            // The follow target sits left/right of screen centre (over-the-shoulder composition), so the camera
            // looks that much to the other side of it, and it sits beside the character-to-destination line,
            // not on it. Solve for the orbit angle whose camera, looking forward, points the crosshair at the
            // destination (a few fixed-point steps converge; the camera moves very little between them).
            float offset = CompositionYawOffset();
            float horizontalRadius = orbit.Radius * Mathf.Cos(orbit.VerticalAxis.Value * Mathf.Deg2Rad);
            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg - offset;

            for (int i = 0; i < 3; i++)
            {
                float radians = yaw * Mathf.Deg2Rad;
                float cameraX = from.x - Mathf.Sin(radians) * horizontalRadius;
                float cameraZ = from.z - Mathf.Cos(radians) * horizontalRadius;
                yaw = Mathf.Atan2(to.x - cameraX, to.z - cameraZ) * Mathf.Rad2Deg - offset;
            }

            orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, yaw);
        }

        private float CompositionYawOffset()
        {
            var composer = virtualCamera != null ? virtualCamera.GetComponent<CinemachineRotationComposer>() : null;
            if (composer == null || Screen.height <= 0)
            {
                return 0f;
            }

            float aspect = Screen.width / (float)Screen.height;
            float halfVertical = virtualCamera.Lens.FieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanHalfHorizontal = Mathf.Tan(halfVertical) * aspect;
            return Mathf.Atan(-2f * composer.Composition.ScreenPosition.x * tanHalfHorizontal) * Mathf.Rad2Deg;
        }

        /// <summary>Mouse look is switched off while the player has the cursor free to click the HUD.</summary>
        public void SetLookEnabled(bool enabled)
        {
            if (lookInput != null && lookInput.enabled != enabled)
            {
                lookInput.enabled = enabled;
            }
        }

        private void Update()
        {
            if (orbit == null)
            {
                return;
            }

            float turn = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            if (turn != 0f)
            {
                orbit.HorizontalAxis.Value += turn * keyboardRotateSpeed * Time.deltaTime;
            }
        }
    }
}
