using Game.Combat.TrailsBattle3D.Runtime;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Re-runnable setup for fighting on the overworld: builds the third-person battle camera as a prefab (the arena scene's rig is
    /// part of that scene and cannot be reused), and hands it to the EncounterRunner prefab. The camera collides with everything a
    /// physics raycast would hit, since the level geometry is not on the arena's "BattleGeometry" layer.
    /// To go back to the arena fights, untick "Fight In Overworld" on the CombatSystemConfig asset.
    /// Run via: Tools/Combat/Setup In-Place Combat
    /// </summary>
    public static class InPlaceCombatSetup
    {
        public const string RigPrefabPath = "Assets/_Project/Prefabs/Combat/TrailsBattle3D/InPlaceBattleCameraRig.prefab";
        public const string RunnerPrefabPath = "Assets/_Project/Prefabs/Combat/TrailsBattle3D/EncounterRunner.prefab";

        /// <summary>Priority of the battle camera; the exploration camera sits at the default of 0, so the brain blends to this one.</summary>
        private const int BattleCameraPriority = 100;

        [MenuItem("Tools/Combat/Setup In-Place Combat")]
        public static void Run()
        {
            var rigPrefab = BuildCameraRigPrefab();
            AssignToRunnerPrefab(rigPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[InPlaceCombatSetup] Camera rig prefab built and assigned to the EncounterRunner prefab.");
        }

        private static BattleCameraRig BuildCameraRigPrefab()
        {
            var rigObject = new GameObject("InPlaceBattleCameraRig");
            rigObject.transform.position = new Vector3(0f, 2f, -5f);

            var vcam = rigObject.AddComponent<CinemachineCamera>();
            vcam.Lens.FieldOfView = 55f;
            vcam.Priority.Enabled = true;
            vcam.Priority.Value = BattleCameraPriority;

            var orbit = rigObject.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = 3.4f;
            orbit.TargetOffset = Vector3.zero;
            orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            orbit.TrackerSettings.PositionDamping = new Vector3(0.35f, 0.35f, 0.35f);
            orbit.HorizontalAxis.Wrap = true;
            orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
            orbit.HorizontalAxis.Recentering.Enabled = false;
            orbit.VerticalAxis.Range = new Vector2(-10f, 70f);
            orbit.VerticalAxis.Value = 20f;
            orbit.VerticalAxis.Recentering.Enabled = false;

            var composer = rigObject.AddComponent<CinemachineRotationComposer>();
            composer.Composition.ScreenPosition = new Vector2(-0.18f, -0.1f);
            composer.Damping = new Vector2(0.1f, 0.1f);

            var deoccluder = rigObject.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = Physics.DefaultRaycastLayers;
            deoccluder.TransparentLayers = 0;
            deoccluder.MinimumDistanceFromTarget = 0.6f;
            deoccluder.AvoidObstacles.Enabled = true;
            deoccluder.AvoidObstacles.CameraRadius = 0.3f;

            var input = rigObject.AddComponent<CinemachineInputAxisController>();

            var rig = rigObject.AddComponent<BattleCameraRig>();
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("virtualCamera").objectReferenceValue = vcam;
            serialized.FindProperty("orbit").objectReferenceValue = orbit;
            serialized.FindProperty("lookInput").objectReferenceValue = input;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(rigObject, RigPrefabPath);
            Object.DestroyImmediate(rigObject);
            return saved.GetComponent<BattleCameraRig>();
        }

        private static void AssignToRunnerPrefab(BattleCameraRig rigPrefab)
        {
            var contents = PrefabUtility.LoadPrefabContents(RunnerPrefabPath);
            try
            {
                var runner = contents.GetComponent<EncounterRunner>();
                var serialized = new SerializedObject(runner);
                serialized.FindProperty("inPlaceCameraRigPrefab").objectReferenceValue = rigPrefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, RunnerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
