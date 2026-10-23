using Game.Combat.TrailsBattle3D.Runtime;
using Game.Combat.UI.UGUI;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// One-time (re-runnable) setup for the third-person battle camera: a "BattleGeometry" physics layer (so the
    /// camera collides with walls but not with combatants), a CinemachineBrain on the arena camera, the
    /// BattleCameraRig object, and the crosshair in the battle HUD.
    /// Run via: Tools/Combat/Setup Third-Person Battle Camera
    /// </summary>
    public static class ThirdPersonBattleSetup
    {
        public const string GeometryLayerName = "BattleGeometry";

        private const string ScenePath = "Assets/_Project/Scenes/Battle/BattleArena_01.unity";
        private const string HudPath = "Assets/_Project/Prefabs/Combat/TrailsBattle3D/UI/BattleHUD.prefab";
        private const string RigName = "BattleCameraRig";
        private const string CrosshairName = "Crosshair";

        [MenuItem("Tools/Combat/Setup Third-Person Battle Camera")]
        public static void Run()
        {
            int layer = EnsureLayer(GeometryLayerName);

            string previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            AssignGeometryLayer(layer);
            SetupRig(layer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            if (!string.IsNullOrEmpty(previousScene) && previousScene != ScenePath)
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }

            SetupCrosshair();
            AssetDatabase.SaveAssets();
            Debug.Log("[ThirdPersonBattleSetup] Done.");
        }

        public static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
            {
                return existing;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }

            throw new System.InvalidOperationException("No free user layer slot for " + layerName);
        }

        /// <summary>The arena floor and everything under ArenaDressing belong to the layer the camera collides with.</summary>
        private static void AssignGeometryLayer(int layer)
        {
            var floor = GameObject.Find("ArenaFloor");
            if (floor != null)
            {
                floor.layer = layer;
            }

            var dressing = GameObject.Find("ArenaDressing");
            if (dressing != null)
            {
                foreach (var t in dressing.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = layer;
                }
            }
        }

        private static void SetupRig(int geometryLayer)
        {
            var cameraObject = GameObject.Find("Battle Camera");
            if (cameraObject.GetComponent<CinemachineBrain>() == null)
            {
                cameraObject.AddComponent<CinemachineBrain>();
            }

            var old = GameObject.Find(RigName);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var rigObject = new GameObject(RigName);
            rigObject.transform.position = new Vector3(0f, 2f, -5f);

            var vcam = rigObject.AddComponent<CinemachineCamera>();
            vcam.Lens.FieldOfView = 55f;

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
            deoccluder.CollideAgainst = 1 << geometryLayer;
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
        }

        private static void SetupCrosshair()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                Transform canvas = null;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "OverlayCanvas")
                    {
                        canvas = t;
                        break;
                    }
                }

                if (canvas == null)
                {
                    throw new System.InvalidOperationException("OverlayCanvas not found in BattleHUD");
                }

                var existing = canvas.Find(CrosshairName);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing.gameObject);
                }

                var crosshair = new GameObject(CrosshairName, typeof(RectTransform));
                crosshair.transform.SetParent(canvas, false);
                var rect = (RectTransform)crosshair.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(36f, 36f);

                var visual = new GameObject("Visual", typeof(RectTransform));
                visual.transform.SetParent(crosshair.transform, false);
                var visualRect = (RectTransform)visual.transform;
                visualRect.anchorMin = Vector2.zero;
                visualRect.anchorMax = Vector2.one;
                visualRect.offsetMin = visualRect.offsetMax = Vector2.zero;

                AddImage(visual.transform, "Ring", "Battle/target_ring", Vector2.zero, new Vector2(36f, 36f));
                AddImage(visual.transform, "Dot", "Decor/deco_stud", Vector2.zero, new Vector2(8f, 8f));

                var component = crosshair.AddComponent<CursorCrosshair>();
                var serialized = new SerializedObject(component);
                serialized.FindProperty("visual").objectReferenceValue = visual;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddImage(Transform parent, string name, string sprite, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/" + sprite + ".png");
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }
}
