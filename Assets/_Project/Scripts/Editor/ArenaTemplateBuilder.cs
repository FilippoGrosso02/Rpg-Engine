using System.Collections.Generic;
using System.IO;
using Game.Combat.TrailsBattle3D.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds a few reusable battle arenas (BattleArena_Cave, _Crypt, _Lair) in the look of the overworld dungeon: an irregular walled
    /// floor in the cave materials, columns and low cover, spawn points for five heroes and four enemies, and a baked NavMesh. Each is
    /// a copy of BattleArena_01 (so it keeps the camera, light and rig the runner expects) with the arena itself regenerated.
    /// An encounter picks one through <c>EncounterDefinition.arenaTemplate</c>. Re-runnable: the generated part is replaced each time.
    /// Run via: Tools/Combat/Build Arena Templates
    /// </summary>
    public static class ArenaTemplateBuilder
    {
        const string SourceScene = "Assets/_Project/Scenes/Battle/BattleArena_01.unity";
        const string SceneFolder = "Assets/_Project/Scenes/Battle";
        const string MaterialFolder = "Assets/_Project/Materials/Cave";
        const string RootName = "ArenaGenerated";

        sealed class Template
        {
            public string name;
            public float half;          // half the arena's width; the floor is about 2 x half across
            public int seed;
            public string wall;
            public string ground;
            public bool irregular;      // wavy walls with niches, or a plainer outline with only the corners knocked about
            public int columns;
            public int cover;
        }

        static readonly Template[] Templates =
        {
            new Template { name = "Cave", half = 14f, seed = 11, wall = "cave_wall_brown", ground = "cave_ground_brown", irregular = true, columns = 7, cover = 5 },
            new Template { name = "Crypt", half = 12f, seed = 23, wall = "cave_wall_blue", ground = "cave_ground_grey", irregular = false, columns = 8, cover = 4 },
            new Template { name = "Lair", half = 16f, seed = 37, wall = "cave_wall_blue", ground = "cave_ground_brown", irregular = true, columns = 6, cover = 3 },
        };

        [MenuItem("Tools/Combat/Build Arena Templates")]
        public static void Run()
        {
            string previousScene = EditorSceneManager.GetActiveScene().path;
            foreach (var template in Templates) Build(template);

            if (!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArenaTemplateBuilder] Arena templates built: " + string.Join(", ", System.Array.ConvertAll(Templates, t => t.name)));
        }

        static void Build(Template t)
        {
            string path = $"{SceneFolder}/BattleArena_{t.name}.unity";
            if (!File.Exists(path)) AssetDatabase.CopyAsset(SourceScene, path);

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var old in new[] { "ArenaFloor", "ArenaDressing", RootName })
            {
                var go = GameObject.Find(old);
                if (go != null) Object.DestroyImmediate(go);
            }

            var wall = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{t.wall}.mat");
            var ground = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{t.ground}.mat");
            var root = new GameObject(RootName);
            var rng = new System.Random(t.seed);
            float h = t.half;

            // --- Outline: clockwise from the north-west corner.
            var nominal = new[] { new Vector2(-h, h), new Vector2(h, h), new Vector2(h, -h), new Vector2(-h, -h) };
            var corner = new Vector2[4];
            float cornerJitter = t.irregular ? 1.6f : 0.6f;
            for (int i = 0; i < 4; i++) corner[i] = nominal[i] + new Vector2(DemoDungeonSetup.Jitter(rng, cornerJitter), DemoDungeonSetup.Jitter(rng, cornerJitter));

            var ring = new List<Vector2>();
            var chains = new List<List<Vector2>>();
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corner[i], b = corner[(i + 1) % 4];
                Vector2 along = (nominal[(i + 1) % 4] - nominal[i]).normalized;
                Vector2 inward = new Vector2(along.y, -along.x);
                var chain = t.irregular
                    ? DemoDungeonSetup.Wavy(a, b, inward, rng, false, false)
                    : new List<Vector2> { a, b };
                chains.Add(chain);
                ring.AddRange(chain.GetRange(0, chain.Count - 1));
            }

            // --- Floor and walls.
            var floor = new GameObject("ArenaFloor");
            floor.transform.SetParent(root.transform, false);
            var mesh = DemoDungeonSetup.BuildFloorMesh(ring);
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = ground;
            floor.AddComponent<MeshCollider>().sharedMesh = mesh;
            floor.AddComponent<BattleArenaFloor>();

            for (int k = 0; k < chains.Count; k++) DemoDungeonSetup.BuildWallChain(root.transform, $"Wall{k}", chains[k], Vector2.zero, wall, rng);

            // --- Spawn points first, so columns and cover keep clear of them.
            var spawnPoints = Object.FindAnyObjectByType<BattleArenaSpawnPoints>();
            var keepClear = PlaceSpawnPoints(spawnPoints, h);

            AddColumns(root.transform, wall, rng, h, t.columns, keepClear);
            AddCover(root.transform, wall, rng, h, t.cover, keepClear);

            // The third-person camera collides with this layer only (not with combatants).
            int geometryLayer = ThirdPersonBattleSetup.EnsureLayer(ThirdPersonBattleSetup.GeometryLayerName);
            foreach (var tr in root.GetComponentsInChildren<Transform>())
            {
                tr.gameObject.layer = geometryLayer;
                GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);
            }

            // The scene needs a path on disk before baking: the NavMesh asset is written next to it.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);

            AddToBuildSettings(path);
            Report(t, spawnPoints);
        }

        /// <summary>Five hero slots in two rows to the south, four enemy slots to the north. Returns every spawn position.</summary>
        static List<Vector2> PlaceSpawnPoints(BattleArenaSpawnPoints spawn, float half)
        {
            var positions = new List<Vector2>();
            if (spawn == null)
            {
                Debug.LogWarning("[ArenaTemplateBuilder] No BattleArenaSpawnPoints in the scene.");
                return positions;
            }

            float z = half * 0.36f;
            var party = new[] { new Vector2(-4f, -z), new Vector2(0f, -z), new Vector2(4f, -z), new Vector2(-2f, -z - 2.5f), new Vector2(2f, -z - 2.5f) };
            var enemies = new[] { new Vector2(-6f, z), new Vector2(-2f, z), new Vector2(2f, z), new Vector2(6f, z) };

            spawn.partySpawnPoints = Markers(spawn.transform, "Party_", party);
            spawn.enemySpawnPoints = Markers(spawn.transform, "Enemy_", enemies);
            EditorUtility.SetDirty(spawn);

            positions.AddRange(party);
            positions.AddRange(enemies);
            return positions;
        }

        /// <summary>Reuses the existing marker objects, adds more when needed and removes surplus ones.</summary>
        static Transform[] Markers(Transform parent, string prefix, Vector2[] positions)
        {
            var existing = new List<Transform>();
            foreach (Transform child in parent) if (child.name.StartsWith(prefix)) existing.Add(child);

            var result = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                Transform marker;
                if (i < existing.Count) marker = existing[i];
                else
                {
                    marker = new GameObject(prefix + (i + 1)).transform;
                    marker.SetParent(parent, false);
                }

                marker.name = prefix + (i + 1);
                marker.position = new Vector3(positions[i].x, 0f, positions[i].y);
                result[i] = marker;
            }

            for (int i = positions.Length; i < existing.Count; i++) Object.DestroyImmediate(existing[i].gameObject);
            return result;
        }

        static void AddColumns(Transform parent, Material material, System.Random rng, float half, int count, List<Vector2> keepClear)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 12 && placed < count; attempt++)
            {
                // Scattered through the middle band, away from the walls and the starting positions.
                var p = new Vector2(DemoDungeonSetup.Jitter(rng, half * 0.7f), DemoDungeonSetup.Jitter(rng, half * 0.55f));
                if (Clear(p, keepClear, 3.5f)) continue;

                float radius = DemoDungeonSetup.Range(rng, 0.5f, 0.8f);
                float height = DemoDungeonSetup.Range(rng, 3.2f, 3.8f); // short enough not to hide combatants from the camera
                var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                column.name = "Column";
                column.transform.SetParent(parent, false);
                column.transform.position = new Vector3(p.x, height / 2f, p.y);
                column.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
                column.GetComponent<MeshRenderer>().sharedMaterial = material;
                keepClear.Add(p);
                placed++;
            }
        }

        static void AddCover(Transform parent, Material material, System.Random rng, float half, int count, List<Vector2> keepClear)
        {
            int placed = 0;
            for (int attempt = 0; attempt < count * 12 && placed < count; attempt++)
            {
                var p = new Vector2(DemoDungeonSetup.Jitter(rng, half * 0.6f), DemoDungeonSetup.Jitter(rng, half * 0.3f));
                if (Clear(p, keepClear, 3f)) continue;

                var size = new Vector3(DemoDungeonSetup.Range(rng, 2f, 3.4f), DemoDungeonSetup.Range(rng, 0.9f, 1.5f), DemoDungeonSetup.Range(rng, 0.7f, 1.1f));
                var block = DemoDungeonSetup.Box(parent, "Cover", new Vector3(p.x, size.y / 2f, p.y), size, material);
                block.transform.rotation = Quaternion.Euler(0f, DemoDungeonSetup.Range(rng, 0f, 180f), 0f);
                keepClear.Add(p);
                placed++;
            }
        }

        static bool Clear(Vector2 p, List<Vector2> others, float distance)
        {
            foreach (var other in others) if (Vector2.Distance(p, other) < distance) return true;
            return false;
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void Report(Template t, BattleArenaSpawnPoints spawn)
        {
            int onMesh = 0, total = 0;
            if (spawn != null)
            {
                foreach (var point in spawn.partySpawnPoints) { total++; if (NavMesh.SamplePosition(point.position, out _, 1.5f, NavMesh.AllAreas)) onMesh++; }
                foreach (var point in spawn.enemySpawnPoints) { total++; if (NavMesh.SamplePosition(point.position, out _, 1.5f, NavMesh.AllAreas)) onMesh++; }
            }

            Debug.Log($"[ArenaTemplateBuilder] {t.name}: {onMesh}/{total} spawn points are on the NavMesh.");
        }
    }
}
