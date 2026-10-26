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

        void OnEnable() => SnapBehind();

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            bool free = Time.timeScale <= 0f || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
            if (lookInput != null && lookInput.enabled == free) lookInput.enabled = !free;
        }

        /// <summary>Swings the camera behind the followed target and jumps straight to it (use after teleports / scene start).</summary>
        public void SnapBehind()
        {
            if (virtualCamera == null || orbit == null || virtualCamera.Follow == null) return;
            orbit.HorizontalAxis.Value = Mathf.DeltaAngle(0f, virtualCamera.Follow.eulerAngles.y);
            virtualCamera.PreviousStateIsValid = false;
        }
    }
}
