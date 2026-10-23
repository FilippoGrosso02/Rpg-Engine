using System.Collections.Generic;
using Game.Combat.TrailsBattle3D.Runtime;
using UnityEditor;
using UnityEditor.AI;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the walls, columns, cover and raised platforms (with ramps) of BattleArena_01 under an
    /// "ArenaDressing" root, then re-bakes the NavMesh. Re-runnable: the old root is deleted first.
    /// Walkable raised surfaces get BattleArenaFloor so TargetingInput treats clicks on them as "move here";
    /// walls and columns deliberately do not.
    /// Run via: Tools/Combat/Dress Battle Arena, or -executeMethod Game.EditorTools.BattleArenaDressing.Run
    /// </summary>
    public static class BattleArenaDressing
    {
        private const string ScenePath = "Assets/_Project/Scenes/Battle/BattleArena_01.unity";
        private const string MaterialFolder = "Assets/_Project/Materials/Cave/";
        private const string RootName = "ArenaDressing";

        // Arena floor is 30x30 centered on the origin; perimeter walls sit on the edge (inner face at +-14).
        private const float Edge = 14.5f;
        private const float WallHeight = 4f;

        private static Material _wall;
        private static Material _ground;
        private static Mesh _wedge;

        [MenuItem("Tools/Combat/Dress Battle Arena")]
        public static void Run()
        {
            string previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            _wall = LoadMaterial("cave_wall_blue");
            _ground = LoadMaterial("cave_ground_brown"); // raised ground; brown vs the grey arena floor so high points read clearly
            _wedge = CreateWedgeMesh();

            var old = GameObject.Find(RootName);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var root = new GameObject(RootName);

            // Perimeter.
            var walls = Group(root, "Perimeter");
            Box(walls, "Wall_N", new Vector3(0f, WallHeight / 2f, Edge), new Vector3(30f, WallHeight, 1f), _wall);
            Box(walls, "Wall_S", new Vector3(0f, WallHeight / 2f, -Edge), new Vector3(30f, WallHeight, 1f), _wall);
            Box(walls, "Wall_E", new Vector3(Edge, WallHeight / 2f, 0f), new Vector3(1f, WallHeight, 28f), _wall);
            Box(walls, "Wall_W", new Vector3(-Edge, WallHeight / 2f, 0f), new Vector3(1f, WallHeight, 28f), _wall);

            // Columns and a low cover block. Kept short so they don't hide combatants from the camera.
            var cover = Group(root, "Cover");
            foreach (var p in new[]
            {
                new Vector2(-7.5f, -0.5f), new Vector2(7.5f, 0.5f),
                new Vector2(-3.5f, 0f), new Vector2(3.5f, 0f),
                new Vector2(-8f, 9f), new Vector2(8f, -9f),
            })
            {
                Column(cover, "Column", p, 0.7f, 3.4f);
            }
            Box(cover, "CoverBlock_Center", new Vector3(0f, 0.8f, 0.5f), new Vector3(3.2f, 1.6f, 0.8f), _wall);

            // High points. Each is a platform plus a ramp; both are walkable and clickable.
            var high = Group(root, "HighGround");

            // North dais on the enemy side, ramp rising toward +z.
            Platform(high, "Dais_N", new Vector3(0f, 1f, 12f), new Vector3(10f, 2f, 4f));
            Ramp(high, "Ramp_Dais_N", new Vector3(0f, 0f, 8f), 0f, width: 3f, rise: 2f, run: 4f);

            // North-west platform, ramp from the south.
            Platform(high, "Platform_NW", new Vector3(-12f, 0.75f, 6.5f), new Vector3(4f, 1.5f, 7f));
            Ramp(high, "Ramp_NW", new Vector3(-12f, 0f, 1.25f), 0f, width: 3f, rise: 1.5f, run: 3.5f);

            // South-east platform (point-mirror of NW), ramp from the north.
            Platform(high, "Platform_SE", new Vector3(12f, 0.75f, -6.5f), new Vector3(4f, 1.5f, 7f));
            Ramp(high, "Ramp_SE", new Vector3(12f, 0f, -1.25f), 180f, width: 3f, rise: 1.5f, run: 3.5f);

            // The third-person camera collides with this layer only (not with combatants).
            int geometryLayer = ThirdPersonBattleSetup.EnsureLayer(ThirdPersonBattleSetup.GeometryLayerName);
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                t.gameObject.layer = geometryLayer;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath); // NavMesh data is written next to a saved scene.
            NavMeshBuilder.BuildNavMesh();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            if (!string.IsNullOrEmpty(previousScene) && previousScene != ScenePath)
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }

            Debug.Log("[BattleArenaDressing] Arena dressed and NavMesh re-baked.");
        }

        private static GameObject Group(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go;
        }

        private static GameObject Box(GameObject parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static void Column(GameObject parent, string name, Vector2 xz, float radius, float height)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // unit cylinder: radius 0.5, height 2
            go.name = $"{name}_{xz.x:0.#}_{xz.y:0.#}";
            go.transform.SetParent(parent.transform, false);
            go.transform.position = new Vector3(xz.x, height / 2f, xz.y);
            go.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = _wall;
        }

        private static void Platform(GameObject parent, string name, Vector3 center, Vector3 size)
        {
            var go = Box(parent, name, center, size, _ground);
            go.AddComponent<BattleArenaFloor>();
        }

        /// <summary>A wedge whose high edge is on local +z; rotate about Y to aim it. Base sits at position.y.</summary>
        private static void Ramp(GameObject parent, string name, Vector3 baseCenter, float yaw, float width, float rise, float run)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = baseCenter;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(width, rise, run);
            go.AddComponent<MeshFilter>().sharedMesh = _wedge;
            go.AddComponent<MeshRenderer>().sharedMaterial = _ground;
            go.AddComponent<MeshCollider>().sharedMesh = _wedge;
            go.AddComponent<BattleArenaFloor>();
        }

        /// <summary>Unit wedge: x and z in [-0.5, 0.5], height 0 at z=-0.5 rising to 1 at z=+0.5.</summary>
        private static Mesh CreateWedgeMesh()
        {
            var a = new Vector3(-0.5f, 0f, -0.5f);
            var b = new Vector3(0.5f, 0f, -0.5f);
            var c = new Vector3(0.5f, 0f, 0.5f);
            var d = new Vector3(-0.5f, 0f, 0.5f);
            var e = new Vector3(-0.5f, 1f, 0.5f);
            var f = new Vector3(0.5f, 1f, 0.5f);

            var faces = new List<Vector3[]>
            {
                new[] { a, b, c, d },   // bottom
                new[] { a, b, f, e },   // slope
                new[] { d, c, f, e },   // back
                new[] { a, d, e },      // -x side
                new[] { b, c, f },      // +x side
            };

            var centroid = new Vector3(0f, 0.33f, 0f);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            foreach (var face in faces)
            {
                int start = verts.Count;
                foreach (var v in face)
                {
                    verts.Add(v);
                    uvs.Add(new Vector2(v.x + 0.5f, v.z + 0.5f));
                }

                for (int i = 1; i < face.Length - 1; i++)
                {
                    var normal = Vector3.Cross(face[i] - face[0], face[i + 1] - face[0]);
                    bool outward = Vector3.Dot(normal, face[0] - centroid) > 0f;
                    tris.Add(start);
                    tris.Add(outward ? start + i : start + i + 1);
                    tris.Add(outward ? start + i + 1 : start + i);
                }
            }

            var mesh = new Mesh { name = "ArenaRampWedge" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material LoadMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + name + ".mat");
            if (material == null)
            {
                throw new System.InvalidOperationException("Missing material: " + MaterialFolder + name + ".mat");
            }
            return material;
        }
    }
}
