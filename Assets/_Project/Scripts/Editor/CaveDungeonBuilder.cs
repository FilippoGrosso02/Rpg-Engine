using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Lays out a sample dungeon from the CaveKitBuilder pieces: rooms and 1-wide corridors are carved into a
    /// cell grid, then floors, walls (on every open/solid edge), corner posts, doorway frames (corridor meets
    /// room), roofs and seeded decoration are placed. Saves Prefabs/Cave/CaveDungeon_Sample.prefab and a
    /// Scenes/CaveDungeon_Sample.unity containing it. Roofs sit under a "Roofs" child so they can be toggled off.
    /// Run via: Tools/Cave/Build Sample Dungeon (builds the kit first), or -executeMethod Game.EditorTools.CaveDungeonBuilder.Run
    /// </summary>
    public static class CaveDungeonBuilder
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/Cave/";
        private const string ScenePath = "Assets/_Project/Scenes/CaveDungeon_Sample.unity";
        internal const float Cell = 4f;
        private const float Height = 4f;

        private const int Solid = 0, Room = 1, Corridor = 2;
        private const int W = 26, H = 14;

        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dz = { 0, 0, 1, -1 };

        private static int[,] _grid;

        [MenuItem("Tools/Cave/Build Sample Dungeon")]
        public static void Run()
        {
            BuildPrefab();
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CaveDungeonBuilder: built " + ScenePath);
        }

        /// <summary>Rebuilds the kit and the CaveDungeon_Sample prefab only (also used by the playable Dungeon_03 builder).</summary>
        public static void BuildPrefab()
        {
            CaveKitBuilder.Run();

            var floor = Load("Cave_Floor");
            var roof = Load("Cave_Roof");
            var wall = Load("Cave_Wall");
            var post = Load("Cave_WallCorner");
            var door = Load("Cave_WallDoorway");
            var pillar = Load("Cave_Pillar");
            var cluster = Load("Cave_StalagmiteCluster");
            var boulder = Load("Cave_Boulder");
            var stalactite = Load("Cave_Stalactite");
            var stalagmite = Load("Cave_Stalagmite");

            Carve();

            var root = new GameObject("CaveDungeon_Sample");
            var floors = Group(root, "Floors");
            var roofs = Group(root, "Roofs");
            var walls = Group(root, "Walls");
            var posts = Group(root, "Posts");
            var decor = Group(root, "Decor");
            var lights = Group(root, "Lights");
            var rng = new System.Random(1234);

            for (int x = 0; x < W; x++)
            {
                for (int z = 0; z < H; z++)
                {
                    int kind = _grid[x, z];
                    var c = CellCentre(x, z);

                    if (kind != Solid)
                    {
                        Place(floor, floors, c, 0);
                    }
                    if (kind != Solid || TouchesOpen(x, z))
                    {
                        Place(roof, roofs, c + Vector3.up * Height, 0);
                    }
                    if (kind == Solid)
                    {
                        continue;
                    }

                    // Walls and doorway frames on this cell's edges.
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + Dx[d], nz = z + Dz[d];
                        var edge = c + new Vector3(Dx[d], 0, Dz[d]) * (Cell / 2f);
                        if (Get(nx, nz) == Solid)
                        {
                            // Face into this open cell: direction -d.
                            Place(wall, walls, edge, Yaw(-Dx[d], -Dz[d]));
                        }
                        else if (kind == Corridor && Get(nx, nz) == Room)
                        {
                            // Frame the mouth of the corridor, facing into the room.
                            Place(door, walls, edge, Yaw(Dx[d], Dz[d]));
                        }
                    }

                    if (kind == Room)
                    {
                        Decorate(x, z, c, rng, decor, cluster, boulder, stalactite, stalagmite);
                    }
                }
            }

            // Corner posts: any grid vertex touched by a wall edge.
            for (int x = -1; x < W; x++)
            {
                for (int z = -1; z < H; z++)
                {
                    if (VertexHasWall(x, z))
                    {
                        Place(post, posts, new Vector3((x + 0.5f) * Cell, 0, (z + 0.5f) * Cell), 0);
                    }
                }
            }

            // Hand-placed pillars and lights per room.
            foreach (var p in new[] { new Vector2Int(12, 2), new Vector2Int(15, 2), new Vector2Int(12, 4), new Vector2Int(15, 4),
                                      new Vector2Int(22, 2), new Vector2Int(22, 4) })
            {
                Place(pillar, decor, CellCentre(p.x, p.y), rng.Next(0, 4) * 90f);
            }
            AddLight(lights, CellCentre(3, 2) + Vector3.up * 2.5f, new Color(1f, 0.75f, 0.45f), 12f);
            AddLight(lights, CellCentre(13, 3) + Vector3.up * 2.5f, new Color(1f, 0.75f, 0.45f), 18f);
            AddLight(lights, CellCentre(14, 10) + Vector3.up * 2.5f, new Color(0.55f, 0.75f, 1f), 16f);
            AddLight(lights, CellCentre(3, 9) + Vector3.up * 2.5f, new Color(1f, 0.85f, 0.4f), 12f);
            AddLight(lights, CellCentre(22, 3) + Vector3.up * 2.5f, new Color(1f, 0.35f, 0.25f), 16f);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "CaveDungeon_Sample.prefab");
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- layout

        private static void Carve()
        {
            _grid = new int[W, H];
            Fill(1, 1, 5, 3, Room);        // A: start chamber
            Fill(6, 2, 9, 2, Corridor);    // A -> B
            Fill(10, 1, 17, 5, Room);      // B: pillared hall
            Fill(18, 3, 19, 3, Corridor);  // B -> D
            Fill(20, 1, 24, 5, Room);      // D: boss cavern
            Fill(13, 6, 13, 8, Corridor);  // B -> C
            Fill(10, 9, 17, 12, Room);     // C: lower hall
            Fill(3, 4, 3, 6, Corridor);    // A -> E
            Fill(1, 7, 6, 11, Room);       // E: treasure chamber
            Fill(7, 9, 9, 9, Corridor);    // E -> C
        }

        private static void Fill(int x0, int z0, int x1, int z1, int kind)
        {
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    _grid[x, z] = kind;
                }
            }
        }

        private static int Get(int x, int z) => x < 0 || z < 0 || x >= W || z >= H ? Solid : _grid[x, z];

        private static bool TouchesOpen(int x, int z)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oz = -1; oz <= 1; oz++)
                {
                    if (Get(x + ox, z + oz) != Solid)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // The four cells around vertex (x,z) are (x..x+1, z..z+1); a wall edge runs between two of them.
        private static bool VertexHasWall(int x, int z)
        {
            bool a = Get(x, z) != Solid, b = Get(x + 1, z) != Solid, c = Get(x, z + 1) != Solid, d = Get(x + 1, z + 1) != Solid;
            int open = (a ? 1 : 0) + (b ? 1 : 0) + (c ? 1 : 0) + (d ? 1 : 0);
            return open > 0 && open < 4;
        }

        private static void Decorate(int x, int z, Vector3 c, System.Random rng, GameObject parent,
            GameObject cluster, GameObject boulder, GameObject stalactite, GameObject stalagmite)
        {
            // Keep pillar cells clear; push rocks toward a wall so the room centre stays walkable.
            double roll = rng.NextDouble();
            Vector3 toWall = Vector3.zero;
            for (int d = 0; d < 4; d++)
            {
                if (Get(x + Dx[d], z + Dz[d]) == Corridor)
                {
                    // Cells at a corridor mouth stay clear (a rock in front of a doorway frame would seal it); only the roof gets a stalactite.
                    if (rng.NextDouble() < 0.3)
                    {
                        Place(stalactite, parent, c + Vector3.up * Height + new Vector3(1f, 0, 1f), (float)rng.NextDouble() * 360f);
                    }
                    return;
                }

                if (Get(x + Dx[d], z + Dz[d]) == Solid)
                {
                    toWall += new Vector3(Dx[d], 0, Dz[d]);
                }
            }

            if (toWall != Vector3.zero && roll < 0.35)
            {
                var spot = c + toWall.normalized * 1.1f + new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * 0.8f;
                Place(roll < 0.2 ? cluster : boulder, parent, spot, (float)rng.NextDouble() * 360f);
            }
            else if (roll > 0.85)
            {
                Place(stalagmite, parent, c + new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * 2f,
                    (float)rng.NextDouble() * 360f);
            }

            if (rng.NextDouble() < 0.3)
            {
                Place(stalactite, parent, c + new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * 2f
                    + Vector3.up * Height, (float)rng.NextDouble() * 360f);
            }
        }

        private static Vector3 CellCentre(int x, int z) => new Vector3(x * Cell, 0, z * Cell);

        // Y rotation that turns the wall's +Z (room side) toward (dx, dz).
        private static float Yaw(int dx, int dz) => dx > 0 ? 90f : dx < 0 ? 270f : dz > 0 ? 0f : 180f;

        // ---------------------------------------------------------------- scene

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "CaveDungeon_Sample.prefab");
            PrefabUtility.InstantiatePrefab(prefab, scene);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>().backgroundColor = Color.black;
            camGo.transform.position = new Vector3(48f, 70f, -20f);
            camGo.transform.rotation = Quaternion.Euler(62f, 0f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.22f, 0.3f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject Load(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
            if (go == null)
            {
                throw new System.InvalidOperationException("Missing prefab: " + PrefabFolder + name + ".prefab");
            }
            return go;
        }

        private static GameObject Group(GameObject root, string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(root.transform, false);
            return g;
        }

        private static void Place(GameObject prefab, GameObject parent, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        private static void AddLight(GameObject parent, Vector3 pos, Color color, float range)
        {
            var go = new GameObject("RoomLight");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = 3f;
        }
    }
}
