using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Generates a small modular cave kit on a 4m grid: procedural meshes saved as assets plus prefabs, and a
    /// 3x3 "CaveKit_Preview" room assembled from them. Re-runnable: existing meshes are overwritten in place
    /// (GUIDs and prefab references survive), prefabs are re-saved.
    /// Conventions: walls are 4w x 4h x 1d, pivot bottom-centre, noisy face on +Z (the room side) - rotate Y
    /// 0/90/180/270 to face any direction. Floor/ceiling tiles are 4x4 with the pivot on the walkable/inner face.
    /// Pieces taper their displacement to zero at the edges so neighbours meet without cracks.
    /// Run via: Tools/Cave/Build Modular Kit, or -executeMethod Game.EditorTools.CaveKitBuilder.Run
    /// </summary>
    public static class CaveKitBuilder
    {
        private const string MeshFolder = "Assets/_Project/Art/Models/Cave";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Cave";
        private const string MaterialFolder = "Assets/_Project/Materials/Cave/";

        private const float Grid = 4f;
        private const float Height = 4f;
        private const float WallThickness = 1f;

        // Face bits for MeshBuilder.AddBox: +X, -X, +Y, -Y, +Z, -Z.
        private const int PX = 1, NX = 2, PY = 4, NY = 8, PZ = 16, NZ = 32;

        [MenuItem("Tools/Cave/Build Modular Kit")]
        public static void Run()
        {
            EnsureFolder(MeshFolder);
            EnsureFolder(PrefabFolder);

            var rock = LoadMaterial("cave_wall_brown");
            var ground = LoadMaterial("cave_ground_grey");
            var ceiling = LoadMaterial("cave_wall_blue");

            var floor = SavePiece("Cave_Floor", FloorMesh(), ground, true);
            var ceil = SavePiece("Cave_Ceiling", CeilingMesh(), ceiling, true);
            SavePiece("Cave_Roof", RoofMesh(), rock, true);
            var wall = SavePiece("Cave_Wall", WallMesh(), rock, true);
            var post = SavePiece("Cave_WallCorner", CornerPostMesh(), rock, true);
            var door = SavePiece("Cave_WallDoorway", DoorwayMesh(), rock, false); // mesh collider: a box would seal the opening
            var pillar = SavePiece("Cave_Pillar", PillarMesh(), rock, false);
            var stalagmite = SavePiece("Cave_Stalagmite", StalagmiteMesh(), rock, false);
            var stalactite = SavePiece("Cave_Stalactite", StalactiteMesh(), rock, false);
            var cluster = SavePiece("Cave_StalagmiteCluster", ClusterMesh(), rock, false);
            var boulder = SavePiece("Cave_Boulder", BoulderMesh(), rock, false);

            BuildPreview(floor, ceil, wall, post, door, pillar, cluster, boulder, stalactite);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CaveKitBuilder: kit built in " + PrefabFolder);
        }

        // ---------------------------------------------------------------- pieces

        private static Mesh FloorMesh()
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0, -0.25f, 0), new Vector3(Grid, 0.5f, Grid), PY, 8, 0.12f, 11);
            return b.ToMesh("Cave_Floor");
        }

        private static Mesh CeilingMesh()
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0, 0.25f, 0), new Vector3(Grid, 0.5f, Grid), NY, 8, 0.2f, 23);
            return b.ToMesh("Cave_Ceiling");
        }

        // Thick rock slab sitting on top of the walls (pivot on its underside at wall height). Rough on both
        // faces so it reads as natural rock from inside and from a top-down camera.
        private static Mesh RoofMesh()
        {
            var b = new MeshBuilder();
            var centre = new Vector3(0, 0.5f, 0);
            var size = new Vector3(Grid, 1f, Grid);
            b.AddBox(centre, size, NY, 8, 0.2f, 31);
            b.AddBox(centre, size, PY, 8, 0.3f, 37);
            b.AddBox(centre, size, PX | NX | PZ | NZ, 2, 0f, 41);
            return b.ToMesh("Cave_Roof");
        }

        private static Mesh WallMesh()
        {
            var b = new MeshBuilder();
            AddWallBlock(b, new Vector3(0, Height / 2f, 0), new Vector3(Grid, Height, WallThickness), 31);
            return b.ToMesh("Cave_Wall");
        }

        private static Mesh CornerPostMesh()
        {
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0, Height / 2f, 0), new Vector3(WallThickness, Height, WallThickness),
                PX | NX | PZ | NZ, 8, 0.12f, 47);
            return b.ToMesh("Cave_WallCorner");
        }

        private static Mesh DoorwayMesh()
        {
            var b = new MeshBuilder();
            const float opening = 1.5f;                       // clear width
            const float lintel = 1.25f;                       // height of the beam above the opening
            float side = (Grid - opening) / 2f;
            float sideX = opening / 2f + side / 2f;
            AddWallBlock(b, new Vector3(-sideX, Height / 2f, 0), new Vector3(side, Height, WallThickness), 53);
            AddWallBlock(b, new Vector3(sideX, Height / 2f, 0), new Vector3(side, Height, WallThickness), 59);
            AddWallBlock(b, new Vector3(0, Height - lintel / 2f, 0), new Vector3(opening, lintel, WallThickness), 61);
            return b.ToMesh("Cave_WallDoorway");
        }

        private static void AddWallBlock(MeshBuilder b, Vector3 centre, Vector3 size, int seed)
        {
            b.AddBox(centre, size, PZ, 8, 0.25f, seed);       // room side
            b.AddBox(centre, size, NZ, 4, 0.08f, seed + 1);   // back side
            b.AddBox(centre, size, PX | NX, 6, 0.1f, seed + 2); // ends / doorway jambs
        }

        private static Mesh PillarMesh()
        {
            var b = new MeshBuilder();
            // Hourglass column: wide at floor and ceiling, narrow in the middle.
            b.AddLathe(Vector3.zero, 12, 10, Height, t =>
            {
                float flare = Mathf.Pow(Mathf.Abs(2f * t - 1f), 2f);
                return 0.55f + 0.35f * flare;
            }, 0.1f, 71, true, true);
            return b.ToMesh("Cave_Pillar");
        }

        private static Mesh StalagmiteMesh()
        {
            var b = new MeshBuilder();
            b.AddLathe(Vector3.zero, 10, 8, 2.2f, t => 0.7f * Mathf.Pow(1f - t, 0.85f), 0.1f, 83, true, false);
            return b.ToMesh("Cave_Stalagmite");
        }

        private static Mesh StalactiteMesh()
        {
            var b = new MeshBuilder();
            // Pivot at the ceiling attachment; grows downward.
            b.AddLathe(Vector3.zero, 10, 8, -2.2f, t => 0.7f * Mathf.Pow(1f - t, 0.85f), 0.1f, 89, false, true);
            return b.ToMesh("Cave_Stalactite");
        }

        private static Mesh ClusterMesh()
        {
            var b = new MeshBuilder();
            b.AddLathe(new Vector3(0, 0, 0), 10, 8, 2.4f, t => 0.6f * Mathf.Pow(1f - t, 0.85f), 0.1f, 97, true, false);
            b.AddLathe(new Vector3(0.75f, 0, 0.3f), 9, 7, 1.4f, t => 0.42f * Mathf.Pow(1f - t, 0.85f), 0.1f, 101, true, false);
            b.AddLathe(new Vector3(-0.5f, 0, 0.7f), 9, 7, 1.0f, t => 0.35f * Mathf.Pow(1f - t, 0.85f), 0.1f, 103, true, false);
            return b.ToMesh("Cave_StalagmiteCluster");
        }

        private static Mesh BoulderMesh()
        {
            var b = new MeshBuilder();
            b.AddLathe(Vector3.zero, 14, 10, 1.6f, t => 1.05f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.12f, 1f, t)), 0.22f, 107, true, true);
            return b.ToMesh("Cave_Boulder");
        }

        // ---------------------------------------------------------------- preview room

        private static void BuildPreview(GameObject floor, GameObject ceil, GameObject wall, GameObject post,
            GameObject door, GameObject pillar, GameObject cluster, GameObject boulder, GameObject stalactite)
        {
            var root = new GameObject("CaveKit_Preview");
            const float half = 6f; // 3x3 tiles

            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Place(floor, root, new Vector3(x * Grid, 0, z * Grid), 0);
                    Place(ceil, root, new Vector3(x * Grid, Height, z * Grid), 0);
                }
            }

            for (int i = -1; i <= 1; i++)
            {
                float o = i * Grid;
                Place(i == 0 ? door : wall, root, new Vector3(o, 0, -half), 0);   // south, doorway in the middle
                Place(wall, root, new Vector3(o, 0, half), 180);                    // north
                Place(wall, root, new Vector3(half, 0, o), 270);                    // east
                Place(wall, root, new Vector3(-half, 0, o), 90);                    // west
            }

            foreach (var sx in new[] { -half, half })
            {
                foreach (var sz in new[] { -half, half })
                {
                    Place(post, root, new Vector3(sx, 0, sz), 0);
                }
            }

            Place(pillar, root, new Vector3(0, 0, 0), 0);
            Place(cluster, root, new Vector3(-3.6f, 0, 3.4f), 20);
            Place(boulder, root, new Vector3(3.5f, 0, 3f), 40);
            Place(stalactite, root, new Vector3(3.2f, Height, -2.5f), 0);
            Place(stalactite, root, new Vector3(-2.4f, Height, -1.2f), 70);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/CaveKit_Preview.prefab");
            Object.DestroyImmediate(root);
        }

        private static void Place(GameObject prefab, GameObject parent, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        // ---------------------------------------------------------------- asset helpers

        private static GameObject SavePiece(string name, Mesh mesh, Material material, bool boxCollider)
        {
            string meshPath = MeshFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            EditorUtility.SetDirty(mesh);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (boxCollider)
            {
                var col = go.AddComponent<BoxCollider>();
                col.center = mesh.bounds.center;
                col.size = mesh.bounds.size;
            }
            else
            {
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        // ---------------------------------------------------------------- mesh generation

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _verts = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _tris = new List<int>();

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_verts);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }

            /// <summary>
            /// Adds the faces of a box selected by <paramref name="faces"/>. Each face is a subdivided grid whose
            /// vertices are pushed along the face normal by noise, tapering to zero within 0.5m of the face edge.
            /// </summary>
            public void AddBox(Vector3 centre, Vector3 size, int faces, int subdiv, float amp, int seed)
            {
                for (int bit = 0; bit < 6; bit++)
                {
                    if ((faces & (1 << bit)) == 0)
                    {
                        continue;
                    }

                    int axis = bit / 2;
                    float sign = bit % 2 == 0 ? 1f : -1f;
                    int uAxis = (axis + 1) % 3;
                    int vAxis = (axis + 2) % 3;
                    var normal = Axis(axis) * sign;
                    float su = size[uAxis], sv = size[vAxis];
                    int n = Mathf.Max(1, subdiv);
                    int baseIndex = _verts.Count;

                    for (int j = 0; j <= n; j++)
                    {
                        for (int i = 0; i <= n; i++)
                        {
                            float fu = i / (float)n, fv = j / (float)n;
                            var p = centre + normal * (size[axis] * 0.5f)
                                + Axis(uAxis) * ((fu - 0.5f) * su) + Axis(vAxis) * ((fv - 0.5f) * sv);

                            float edge = Mathf.Min(Mathf.Min(fu * su, (1 - fu) * su), Mathf.Min(fv * sv, (1 - fv) * sv));
                            float taper = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 0.5f));
                            // Sample noise in a face-local frame so adjacent pieces of the same kit stay consistent.
                            p += normal * (amp * taper * Noise(new Vector3(fu * su, fv * sv, bit * 5.3f), seed));

                            _verts.Add(p);
                            _uvs.Add(new Vector2(fu * su / Grid, fv * sv / Grid));
                        }
                    }

                    for (int j = 0; j < n; j++)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            int a = baseIndex + j * (n + 1) + i;
                            AddTri(a, a + n + 1, a + n + 2, normal);
                            AddTri(a, a + n + 2, a + 1, normal);
                        }
                    }
                }
            }

            /// <summary>
            /// Adds a solid of revolution around +Y from <paramref name="origin"/>. <paramref name="height"/> may be
            /// negative to hang downward. Optional flat caps close the bottom/top; a zero-radius end needs none.
            /// </summary>
            public void AddLathe(Vector3 origin, int sides, int rings, float height,
                System.Func<float, float> radius, float amp, int seed, bool capBase, bool capTop)
            {
                int baseIndex = _verts.Count;
                for (int r = 0; r <= rings; r++)
                {
                    float t = r / (float)rings;
                    float rad = radius(t);
                    for (int s = 0; s <= sides; s++)
                    {
                        float ang = s / (float)sides * Mathf.PI * 2f;
                        // Wrap noise sampling on the circle so the seam matches.
                        float wobble = 1f + amp * Noise(new Vector3(Mathf.Cos(ang) * 1.3f, t * 3f, Mathf.Sin(ang) * 1.3f), seed);
                        var p = origin + new Vector3(Mathf.Cos(ang) * rad * wobble, t * height, Mathf.Sin(ang) * rad * wobble);
                        _verts.Add(p);
                        _uvs.Add(new Vector2(s / (float)sides * Mathf.PI * 2f * Mathf.Max(rad, 0.3f) / Grid, Mathf.Abs(height) * t / Grid));
                    }
                }

                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < sides; s++)
                    {
                        int a = baseIndex + r * (sides + 1) + s;
                        int b = a + 1, c = a + sides + 1, d = c + 1;
                        // Outward = away from axis; use the first-vertex radial as the reference direction.
                        var radial = new Vector3(_verts[a].x - origin.x, 0, _verts[a].z - origin.z);
                        AddTri(a, c, d, radial);
                        AddTri(a, d, b, radial);
                    }
                }

                if (capBase)
                {
                    AddCap(origin, sides, radius(0f), seed, amp, Vector3.down * Mathf.Sign(height));
                }
                if (capTop)
                {
                    AddCap(origin + Vector3.up * height, sides, radius(1f), seed, amp, Vector3.up * Mathf.Sign(height));
                }
            }

            private void AddCap(Vector3 centre, int sides, float rad, int seed, float amp, Vector3 normal)
            {
                if (rad < 0.02f)
                {
                    return;
                }
                int c = _verts.Count;
                _verts.Add(centre);
                _uvs.Add(new Vector2(0.5f, 0.5f));
                for (int s = 0; s <= sides; s++)
                {
                    float ang = s / (float)sides * Mathf.PI * 2f;
                    float wobble = 1f + amp * Noise(new Vector3(Mathf.Cos(ang) * 1.3f, 0f, Mathf.Sin(ang) * 1.3f), seed);
                    _verts.Add(centre + new Vector3(Mathf.Cos(ang) * rad * wobble, 0, Mathf.Sin(ang) * rad * wobble));
                    _uvs.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
                }
                for (int s = 0; s < sides; s++)
                {
                    AddTri(c, c + 1 + s, c + 2 + s, normal);
                }
            }

            // Adds a triangle wound so its geometric normal agrees with the intended outward direction.
            private void AddTri(int a, int b, int c, Vector3 outward)
            {
                var n = Vector3.Cross(_verts[b] - _verts[a], _verts[c] - _verts[a]);
                if (Vector3.Dot(n, outward) < 0f)
                {
                    int tmp = b;
                    b = c;
                    c = tmp;
                }
                _tris.Add(a);
                _tris.Add(b);
                _tris.Add(c);
            }

            private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
        }

        // Two-octave Perlin, roughly in [-0.5, 0.5].
        private static float Noise(Vector3 p, int seed)
        {
            float a = Mathf.PerlinNoise(p.x * 0.8f + seed * 1.37f, p.y * 0.8f + p.z * 0.6f + seed * 0.71f);
            float b = Mathf.PerlinNoise(p.x * 2.1f + seed * 0.43f, p.y * 2.1f + p.z * 1.7f + seed * 2.9f);
            return (a * 0.7f + b * 0.3f) - 0.5f;
        }
    }
}
