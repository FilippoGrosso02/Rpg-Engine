using Game.Overworld;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Re-runnable setup of the close third-person overworld camera in Dungeon_01: a CinemachineBrain on the Overworld Camera
    /// (its old TopDownCamera is disabled) and an OverworldCameraRig under OverworldRoot that follows the player.
    /// Run via: Tools/Overworld/Setup Third-Person Camera
    /// </summary>
    public static class OverworldCameraSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Overworld/Dungeon_01.unity";
        const string RigName = "OverworldCameraRig";

        [MenuItem("Tools/Overworld/Setup Third-Person Camera")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("OverworldRoot");
            var player = root.transform.Find("Player");
            var cameraObject = root.transform.Find("Overworld Camera").gameObject;

            var oldCamera = cameraObject.GetComponent<TopDownCamera>();
            if (oldCamera != null) oldCamera.enabled = false;
            if (cameraObject.GetComponent<CinemachineBrain>() == null) cameraObject.AddComponent<CinemachineBrain>();

            var old = root.transform.Find(RigName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var rigObject = new GameObject(RigName);
            rigObject.transform.SetParent(root.transform, false);
            rigObject.transform.position = player.position + new Vector3(0f, 2f, -3f);

            var vcam = rigObject.AddComponent<CinemachineCamera>();
            vcam.Lens.FieldOfView = 55f;
            vcam.Follow = player;
            vcam.LookAt = player;

            var orbit = rigObject.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = 4.5f;
            orbit.TargetOffset = new Vector3(0f, 1.5f, 0f);
            orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            orbit.TrackerSettings.PositionDamping = new Vector3(0.25f, 0.25f, 0.25f);
            orbit.HorizontalAxis.Wrap = true;
            orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbit.HorizontalAxis.Recentering.Enabled = false;
            orbit.VerticalAxis.Range = new Vector2(-10f, 70f);
            orbit.VerticalAxis.Value = 18f;
            orbit.VerticalAxis.Recentering.Enabled = false;

            var composer = rigObject.AddComponent<CinemachineRotationComposer>();
            composer.TargetOffset = new Vector3(0f, 1.3f, 0f);
            composer.Composition.ScreenPosition = new Vector2(0f, -0.1f);
            composer.Damping = new Vector2(0.1f, 0.1f);

            var deoccluder = rigObject.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = ~0 & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            deoccluder.TransparentLayers = 0;
            deoccluder.MinimumDistanceFromTarget = 0.6f;
            deoccluder.AvoidObstacles.Enabled = true;
            deoccluder.AvoidObstacles.CameraRadius = 0.3f;

            var input = rigObject.AddComponent<CinemachineInputAxisController>();

            var rig = rigObject.AddComponent<OverworldCameraRig>();
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("virtualCamera").objectReferenceValue = vcam;
            serialized.FindProperty("orbit").objectReferenceValue = orbit;
            serialized.FindProperty("lookInput").objectReferenceValue = input;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[OverworldCameraSetup] Done.");
        }
    }
}
