using System.Collections.Generic;
using System.IO;
using Game.Combat.Contracts;
using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Data.State;
using Game.Overworld;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the demo dungeon: the items / gear / monsters / encounters it needs (load-or-create, re-runnable) and the
    /// Dungeon_02 greybox scene (copied from Dungeon_01 for the player, camera, director and HUD wiring, then everything dungeon-specific
    /// is regenerated). Nine rooms with a west and an east branch that meet at a locked door before the boss.
    /// Run via: Tools/Overworld/Build Demo Dungeon (Dungeon_02), or -executeMethod Game.EditorTools.DemoDungeonSetup.Run
    /// </summary>
    public static class DemoDungeonSetup
    {
        const string DataRoot = "Assets/_Project/Resources/Data";
        const string SceneFolder = "Assets/_Project/Scenes/Overworld";
        const string SourceScenePath = SceneFolder + "/Dungeon_01.unity";
        const string ScenePath = SceneFolder + "/Dungeon_02.unity";
        const string ModelFolder = "Assets/_Project/Prefabs/Characters";
        const string EnemyAnimPath = "Assets/_Project/Config/Animation/AnimSet_Enemy.asset";
        const string EnemyTemplatePath = "Assets/_Project/Prefabs/Overworld/OverworldEnemy_Template.prefab";

        // Item / gear ids shared between content creation and the scene.
        const string Potion = "item_healing_potion";
        const string IronKey = "item_iron_key";
        const string CryptSigil = "item_crypt_sigil";

        // Room centres on a 20-unit grid; the wall gap and corridors follow Dungeon_01.
        const float DefaultHalfRoom = 8f;
        const float WallHeight = 3f;
        const float WallThickness = 0.5f;
        const float CorridorWidth = 3.6f;

        const string CaveFolder = "Assets/_Project/Materials/Cave";

        enum Dir { N, E, S, W }

        sealed class Room
        {
            public string name;
            public Vector2 center;
            public float half = DefaultHalfRoom;
            public readonly HashSet<Dir> openings = new HashSet<Dir>();
        }

        static readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();

        [MenuItem("Tools/Overworld/Build Demo Dungeon (Dungeon_02)")]
        public static void Run()
        {
            CreateContent();
            BuildScene();
        }

        /// <summary>Regenerates only the Dungeon_02 geometry, props and enemies, leaving the item / monster / encounter assets untouched.</summary>
        [MenuItem("Tools/Overworld/Rebuild Demo Dungeon Scene Only (Dungeon_02)")]
        public static void RunSceneOnly() => BuildScene();

        // ------------------------------------------------------------------ content

        static void CreateContent()
        {
            foreach (var folder in new[] { "Items", "Weapons", "Armor", "Enemies", "Encounters" })
                EnsureFolder($"{DataRoot}/{folder}");

            // Items
            Create<ItemDefinition>("Items", Potion, "Healing Potion", i =>
            {
                i.description = "Heals the most wounded hero by 5 HP.";
                i.itemType = ItemType.Consumable;
                i.healAmount = 5;
                i.stackable = true;
                i.maxStack = 99;
            });
            Create<ItemDefinition>("Items", IronKey, "Iron Key", i =>
            {
                i.description = "Heavy and cold. Taken from the guard post captain; it fits the sealed gate.";
                i.itemType = ItemType.KeyItem;
                i.stackable = false;
                i.maxStack = 1;
            });
            Create<ItemDefinition>("Items", CryptSigil, "Crypt Sigil", i =>
            {
                i.description = "A carved bone sigil from the shrine. The sealed gate recognises it.";
                i.itemType = ItemType.KeyItem;
                i.stackable = false;
                i.maxStack = 1;
            });

            // Gear
            Create<WeaponDefinition>("Weapons", "weapon_longsword", "Longsword", w =>
            {
                w.description = "A well-kept blade from the armory. d8 damage.";
                w.equipSlot = EquipmentSlot.MainHand;
                w.damage = new DiceRoll(1, 8);
                w.attackModifier = 0;
                w.isRanged = false;
                w.isMetal = true;
                w.damageType = DamageType.Slashing;
            });
            Create<WeaponDefinition>("Weapons", "weapon_longbow", "Longbow", w =>
            {
                w.description = "Found in the crypt. d8 damage at range.";
                w.equipSlot = EquipmentSlot.MainHand;
                w.damage = new DiceRoll(1, 8);
                w.attackModifier = 0;
                w.isRanged = true;
                w.isMetal = false;
                w.damageType = DamageType.Piercing;
            });
            Create<WeaponDefinition>("Weapons", "weapon_keen_dagger", "Keen Dagger", w =>
            {
                w.description = "Light and sharp: +3 to hit.";
                w.equipSlot = EquipmentSlot.MainHand;
                w.damage = new DiceRoll(1, 4);
                w.attackModifier = 3;
                w.isRanged = false;
                w.isMetal = true;
                w.damageType = DamageType.Piercing;
            });
            Create<ArmorDefinition>("Armor", "armor_banded_mail", "Banded Mail", a =>
            {
                a.description = "Heavy plates riveted to leather. +5 DEF.";
                a.equipSlot = EquipmentSlot.Body;
                a.acBonus = 5;
            });
            Create<ArmorDefinition>("Armor", "armor_studded_leather", "Studded Leather", a =>
            {
                a.description = "Leather reinforced with studs. +3 DEF.";
                a.equipSlot = EquipmentSlot.Body;
                a.acBonus = 3;
            });

            // Monsters (the Bandit, Archer and Witch from OsrRosterSetup are reused as they are)
            var enemyAnim = AssetDatabase.LoadAssetAtPath<AnimationSetDefinition>(EnemyAnimPath);
            Create<EnemyDefinition>("Enemies", "enemy_skeleton", "Skeleton", e =>
            {
                Monster(e, Model("Skeleton_Battle") ?? Model("Enemy_Bandit"), enemyAnim, "Undead");
                e.hp = 6; e.armorClass = 5; e.attackBonus = 7; e.damage = new DiceRoll(1, 6);
                e.morale = 0; e.dexterity = 6; e.saveBonus = 6;
                e.immuneToCharm = true; e.immuneToFear = true; e.immuneToSleep = true;
            });
            Create<EnemyDefinition>("Enemies", "enemy_bandit_captain", "Bandit Captain", e =>
            {
                Monster(e, Model("Enemy_Bandit"), enemyAnim, "Leader");
                e.hp = 12; e.armorClass = 6; e.attackBonus = 8; e.damage = new DiceRoll(1, 8);
                e.morale = 9; e.dexterity = 8; e.saveBonus = 8;
            });
            Create<EnemyDefinition>("Enemies", "enemy_ogre_warlord", "Ogre Warlord", e =>
            {
                Monster(e, Model("Enemy_Archer"), enemyAnim, "Boss");
                e.hp = 20; e.armorClass = 6; e.attackBonus = 8; e.damage = new DiceRoll(1, 8);
                e.morale = 0; e.dexterity = 7; e.saveBonus = 9;
                e.immuneToFear = true; e.immuneToCharm = true;
            });

            // Encounters (party comes from the PartyRoster; the fixed ids are the fallback with no roster)
            Encounter("EncounterDemoHall", "enemy_goblin", "enemy_goblin");
            Encounter("EncounterDemoArmory", "enemy_goblin", "enemy_skeleton");
            Encounter("EncounterDemoGuardPost", "enemy_bandit_captain", "enemy_skeleton");
            Encounter("EncounterDemoCrypt", "enemy_skeleton", "enemy_skeleton");
            Encounter("EncounterDemoGate", "enemy_slime", "enemy_goblin");
            Encounter("EncounterDemoBoss", "enemy_ogre_warlord", "enemy_goblin");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{name}.prefab");

        static void Monster(EnemyDefinition e, GameObject model, AnimationSetDefinition anim, string role)
        {
            var basic = AssetDatabase.LoadAssetAtPath<SkillDefinition>($"{DataRoot}/Skills/skill_basic_attack.asset");
            e.battleModelPrefab = model;
            e.animationSet = anim;
            e.moveRange = 6;
            e.attackRangeMultiplier = 1;
            e.roleLabel = role;
            e.skillIds = new List<string> { basic.id };
        }

        static void Encounter(string assetName, params string[] enemyIds)
        {
            var path = $"{DataRoot}/Encounters/{assetName}.asset";
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
            if (encounter == null)
            {
                encounter = ScriptableObject.CreateInstance<EncounterDefinition>();
                AssetDatabase.CreateAsset(encounter, path);
            }

            encounter.useFixedParty = false;
            encounter.partyActorIds = new List<string> { "actor_warrior", "actor_ranger", "actor_thief" };
            encounter.enemyIds = new List<string>(enemyIds);
            EditorUtility.SetDirty(encounter);
        }

        static EncounterDefinition LoadEncounter(string assetName) =>
            AssetDatabase.LoadAssetAtPath<EncounterDefinition>($"{DataRoot}/Encounters/{assetName}.asset");

        // ------------------------------------------------------------------ scene

        static void BuildScene()
        {
            if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset(SourceScenePath, ScenePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("OverworldRoot");
            if (root == null) { Debug.LogError("[DemoDungeonSetup] OverworldRoot not found in " + ScenePath); return; }

            EnsureEnemyTemplate(root);

            foreach (var name in new[] { "Dungeon", "Enemies", "Props", "Doors" })
            {
                var old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            var wall = LoadMaterial("Greybox_Wall");
            var floor = LoadMaterial("Greybox_Floor");
            var chestMat = EnsureMaterial("Greybox_Chest", new Color(0.55f, 0.36f, 0.14f), wall);
            var shrineMat = EnsureMaterial("Greybox_Shrine", new Color(0.3f, 0.75f, 0.95f), wall);
            var doorMat = EnsureMaterial("Greybox_Door", new Color(0.4f, 0.25f, 0.12f), wall);
            var trapMat = EnsureMaterial("Greybox_Trap", new Color(0.45f, 0.1f, 0.1f), wall);

            var dungeon = new GameObject("Dungeon").transform;
            dungeon.SetParent(root.transform, false);
            var props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            var doors = new GameObject("Doors").transform;
            doors.SetParent(root.transform, false);
            var enemies = new GameObject("Enemies").transform;
            enemies.SetParent(root.transform, false);

            BuildLayout(dungeon, wall, floor);

            // --- Entrance
            Chest(props, chestMat, "chest_entrance", new Vector3(-4f, 0f, -4f), items: new[] { Loot(Potion, 1) });

            // --- Hall: first fight, fork west / east
            Enemy(enemies, "hall_patrol", "EncounterDemoHall", new Vector3(0f, 0f, 21f), new Vector3(-3f, 0f, 21f), new Vector3(3f, 0f, 21f));

            // --- West branch: Armory (gear, one guard), Guard Post (tough fight, iron key)
            Enemy(enemies, "armory_guard", "EncounterDemoArmory", new Vector3(-20f, 0f, 22f), new Vector3(-24f, 0f, 22f), new Vector3(-16f, 0f, 22f));
            Chest(props, chestMat, "chest_armory_a", new Vector3(-24f, 0f, 16f), weapons: new[] { "weapon_longsword" });
            Chest(props, chestMat, "chest_armory_b", new Vector3(-16f, 0f, 16f), armor: new[] { "armor_banded_mail" });
            Enemy(enemies, "guardpost_captain", "EncounterDemoGuardPost", new Vector3(-20f, 0f, 42f), new Vector3(-24f, 0f, 42f), new Vector3(-16f, 0f, 42f)).patrolColor = EliteGem;
            Chest(props, chestMat, "chest_guardpost", new Vector3(-24f, 0f, 36f),
                items: new[] { Loot(IronKey, 1), Loot(Potion, 1) }, armor: new[] { "armor_studded_leather" });

            // --- East branch: Crypt (trap, skeletons, bow + potions), Shrine (rest point, crypt sigil)
            Enemy(enemies, "crypt_skeletons", "EncounterDemoCrypt", new Vector3(20f, 0f, 22f), new Vector3(16f, 0f, 22f), new Vector3(24f, 0f, 22f));
            Chest(props, chestMat, "chest_crypt", new Vector3(24f, 0f, 16f), weapons: new[] { "weapon_longbow" }, items: new[] { Loot(Potion, 2) });
            Trap(props, trapMat, "crypt_floor_trap", new Vector3(20f, 0f, 17f), damage: 2);
            RestShrine(props, shrineMat, "shrine_east", new Vector3(20f, 0f, 42f), uses: 1);
            Chest(props, chestMat, "chest_shrine", new Vector3(24f, 0f, 36f),
                items: new[] { Loot(CryptSigil, 1) }, weapons: new[] { "weapon_keen_dagger" });

            // --- Gate hall: the two branches meet here; sealed door leads north
            Enemy(enemies, "gate_patrol", "EncounterDemoGate", new Vector3(0f, 0f, 41f), new Vector3(-3f, 0f, 41f), new Vector3(3f, 0f, 41f));
            Door(doors, doorMat, "door_gate", new Vector3(0f, 0f, 50f), new[] { IronKey, CryptSigil });

            // --- Antechamber: last supplies and a second shrine before the boss
            Chest(props, chestMat, "chest_antechamber", new Vector3(-4f, 0f, 64f), items: new[] { Loot(Potion, 2) });
            RestShrine(props, shrineMat, "shrine_antechamber", new Vector3(4f, 0f, 64f), uses: 1);

            // --- Boss
            var boss = Enemy(enemies, "boss_warlord", "EncounterDemoBoss", new Vector3(0f, 0f, 85f));
            boss.patrolColor = EliteGem;
            boss.endsDemo = true;
            boss.viewRadius = 9f;
            boss.transform.localScale = Vector3.one * 1.35f;

            AddPillars(dungeon, wall, props, doors, enemies, root.transform.Find("Player"));

            // Bake the NavMesh over the dungeon geometry (doors and props are outside it on purpose).
            var surface = dungeon.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("[DemoDungeonSetup] Dungeon_02 built and saved.");
        }

        // The overworld enemy (model, agent, animator) is saved once as a prefab from Dungeon_01's first enemy, so rebuilds do not need it.
        static void EnsureEnemyTemplate(GameObject root)
        {
            if (File.Exists(EnemyTemplatePath)) return;

            var source = root.transform.Find("Enemies/Enemy_A1");
            if (source == null) { Debug.LogError("[DemoDungeonSetup] No Enemy_A1 to build the enemy template from."); return; }

            EnsureFolder(Path.GetDirectoryName(EnemyTemplatePath).Replace('\\', '/'));
            var copy = Object.Instantiate(source.gameObject);
            var enemy = copy.GetComponent<OverworldEnemy>();
            enemy.patrolPoints = new Transform[0];
            enemy.encounter = null;
            enemy.enemyId = "";
            PrefabUtility.SaveAsPrefabAsset(copy, EnemyTemplatePath);
            Object.DestroyImmediate(copy);
        }

        static void BuildLayout(Transform parent, Material wall, Material floor)
        {
            Rooms.Clear();
            AddRoom("Entrance", 0, 0);
            AddRoom("Hall", 0, 20);
            AddRoom("Armory", -20, 20);
            AddRoom("GuardPost", -20, 40);
            AddRoom("Crypt", 20, 20);
            AddRoom("Shrine", 20, 40);
            AddRoom("GateHall", 0, 40);
            AddRoom("Antechamber", 0, 60);
            AddRoom("BossRoom", 0, 80, half: 9f);

            var links = new[]
            {
                ("Entrance", "Hall"), ("Hall", "Armory"), ("Armory", "GuardPost"), ("GuardPost", "GateHall"),
                ("Hall", "Crypt"), ("Crypt", "Shrine"), ("Shrine", "GateHall"), ("GateHall", "Antechamber"), ("Antechamber", "BossRoom"),
            };

            foreach (var (a, b) in links)
            {
                var from = Rooms[a];
                var to = Rooms[b];
                Vector2 d = to.center - from.center;
                Dir dir = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? Dir.E : Dir.W) : (d.y > 0 ? Dir.N : Dir.S);
                from.openings.Add(dir);
                to.openings.Add(Opposite(dir));
            }

            foreach (var room in Rooms.Values) BuildRoom(parent, room, wall, floor);
            foreach (var (a, b) in links) BuildCorridor(parent, Rooms[a], Rooms[b], wall, floor);
        }

        static void AddRoom(string name, float x, float z, float half = DefaultHalfRoom) =>
            Rooms[name] = new Room { name = name, center = new Vector2(x, z), half = half };

        static Material Cave(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{CaveFolder}/{name}.mat");

        /// <summary>The eastern branch and the boss lair are cold blue-grey stone, everything else warm brown rock.</summary>
        static bool IsCold(Room room) => room.name == "Crypt" || room.name == "Shrine" || room.name == "BossRoom";

        static Material WallFor(Room room, Material fallback) => Cave(IsCold(room) ? "cave_wall_blue" : "cave_wall_brown") ?? fallback;

        static Material FloorFor(Room room, Material fallback) => Cave(IsCold(room) ? "cave_ground_grey" : "cave_ground_brown") ?? fallback;

        static Dir Opposite(Dir d) => d == Dir.N ? Dir.S : d == Dir.S ? Dir.N : d == Dir.E ? Dir.W : Dir.E;

        static int Seed(string text)
        {
            int hash = 17;
            foreach (var ch in text) hash = hash * 31 + ch;
            return hash;
        }

        static float Jitter(System.Random rng, float amplitude) => (float)(rng.NextDouble() * 2.0 - 1.0) * amplitude;

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>
        /// An irregular cave-like room: the four corners wander, every side is a chain of wall pieces pushed in and out (now and then
        /// into a small niche), and the floor is a mesh that follows exactly that outline. Doorways stay on the straight line so the
        /// corridors still meet them.
        /// </summary>
        static void BuildRoom(Transform parent, Room room, Material wallMat, Material floorMat)
        {
            var rng = new System.Random(Seed(room.name));
            float h = room.half;
            wallMat = WallFor(room, wallMat);
            floorMat = FloorFor(room, floorMat);

            // Clockwise from the north-west corner; side i runs from corner i to corner i + 1 (N, E, S, W).
            var nominal = new[] { new Vector2(-h, h), new Vector2(h, h), new Vector2(h, -h), new Vector2(-h, -h) };
            var corner = new Vector2[4];
            for (int i = 0; i < 4; i++) corner[i] = nominal[i] + new Vector2(Jitter(rng, 1.0f), Jitter(rng, 1.0f));

            var ring = new List<Vector2>();
            var chains = new List<List<Vector2>>();
            var sides = new[] { Dir.N, Dir.E, Dir.S, Dir.W };

            for (int i = 0; i < 4; i++)
            {
                Vector2 na = nominal[i], nb = nominal[(i + 1) % 4];
                Vector2 a = corner[i], b = corner[(i + 1) % 4];
                Vector2 along = (nb - na).normalized;
                Vector2 inward = new Vector2(along.y, -along.x);

                if (!room.openings.Contains(sides[i]))
                {
                    var chain = Wavy(a, b, inward, rng, taperFrom: false, taperTo: false);
                    chains.Add(chain);
                    ring.AddRange(chain.GetRange(0, chain.Count - 1));
                    continue;
                }

                // The doorway sits in the middle of the side, on the nominal line.
                Vector2 gapStart = na + along * (h - CorridorWidth / 2f);
                Vector2 gapEnd = na + along * (h + CorridorWidth / 2f);
                var first = Wavy(a, gapStart, inward, rng, taperFrom: false, taperTo: true);
                var second = Wavy(gapEnd, b, inward, rng, taperFrom: true, taperTo: false);
                chains.Add(first);
                chains.Add(second);
                ring.AddRange(first);
                ring.AddRange(second.GetRange(0, second.Count - 1));
            }

            var c = room.center;
            var floorObject = new GameObject(room.name + "_Floor");
            floorObject.transform.SetParent(parent, false);
            floorObject.transform.localPosition = new Vector3(c.x, 0f, c.y);
            var mesh = BuildFloorMesh(ring);
            floorObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            floorObject.AddComponent<MeshRenderer>().sharedMaterial = floorMat;
            floorObject.AddComponent<MeshCollider>().sharedMesh = mesh;

            for (int k = 0; k < chains.Count; k++) BuildWallChain(parent, $"{room.name}_Wall{k}", chains[k], c, wallMat, rng);
        }

        /// <summary>A line from <paramref name="from"/> to <paramref name="to"/> broken into pieces whose joints are pushed in or out; ends can be pinned for doorways.</summary>
        static List<Vector2> Wavy(Vector2 from, Vector2 to, Vector2 inward, System.Random rng, bool taperFrom, bool taperTo)
        {
            float length = Vector2.Distance(from, to);
            int pieces = Mathf.Max(1, Mathf.RoundToInt(length / 2.4f));
            var points = new List<Vector2> { from };
            int nicheLeft = 0;
            float nicheDepth = 0f;

            for (int i = 1; i < pieces; i++)
            {
                Vector2 basePoint = Vector2.Lerp(from, to, i / (float)pieces);

                // Near a doorway the wall straightens out so it meets the corridor cleanly.
                float weight = 1f;
                if (taperFrom) weight = Mathf.Min(weight, Mathf.Clamp01(Vector2.Distance(basePoint, from) / 2.5f));
                if (taperTo) weight = Mathf.Min(weight, Mathf.Clamp01(Vector2.Distance(basePoint, to) / 2.5f));

                float offset;
                if (nicheLeft > 0)
                {
                    offset = nicheDepth;
                    nicheLeft--;
                }
                else if (weight >= 1f && i < pieces - 2 && rng.NextDouble() < 0.2)
                {
                    nicheDepth = -Range(rng, 1.0f, 1.4f); // a recess, away from the room
                    offset = nicheDepth;
                    nicheLeft = 1;
                }
                else
                {
                    offset = Range(rng, -0.5f, 1.0f); // mostly bulging into the room
                }

                points.Add(basePoint + inward * (offset * weight));
            }

            points.Add(to);
            return points;
        }

        static void BuildWallChain(Transform parent, string name, List<Vector2> points, Vector2 center, Material material, System.Random rng)
        {
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                Vector2 delta = b - a;
                float length = delta.magnitude;
                if (length < 0.05f) continue;

                float height = Range(rng, 3.2f, 4.4f);
                Vector2 mid = center + (a + b) / 2f;
                var piece = Box(parent, $"{name}_{i}", new Vector3(mid.x, height / 2f, mid.y),
                    new Vector3(WallThickness, height, length + WallThickness * 0.9f), material);
                piece.transform.localRotation = Quaternion.LookRotation(new Vector3(delta.x, 0f, delta.y));
            }
        }

        /// <summary>A flat top following the outline (a fan from the middle, which suits these roundish rooms) with a short skirt so the collider has some depth.</summary>
        static Mesh BuildFloorMesh(List<Vector2> ring)
        {
            int n = ring.Count;
            // 0 = middle, 1..n = top outline, n+1..2n = top edge of the skirt, 2n+1..3n = bottom of the skirt (kept apart so shading stays flat).
            var vertices = new List<Vector3> { Vector3.zero };
            for (int i = 0; i < n; i++) vertices.Add(new Vector3(ring[i].x, 0f, ring[i].y));
            for (int i = 0; i < n; i++) vertices.Add(new Vector3(ring[i].x, 0f, ring[i].y));
            for (int i = 0; i < n; i++) vertices.Add(new Vector3(ring[i].x, -1f, ring[i].y));

            var triangles = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                triangles.AddRange(new[] { 0, 1 + i, 1 + next }); // top, clockwise seen from above
                int topA = n + 1 + i, topB = n + 1 + next, lowA = 2 * n + 1 + i, lowB = 2 * n + 1 + next;
                triangles.AddRange(new[] { topA, lowB, topB });
                triangles.AddRange(new[] { topA, lowA, lowB });
            }

            var uvs = new List<Vector2>();
            foreach (var v in vertices) uvs.Add(new Vector2(v.x, v.z) * 0.25f);

            var mesh = new Mesh { name = "RoomFloor" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void BuildCorridor(Transform parent, Room a, Room b, Material wall, Material floor)
        {
            Vector2 axis = (b.center - a.center);
            axis = Mathf.Abs(axis.y) > Mathf.Abs(axis.x) ? new Vector2(0f, Mathf.Sign(axis.y)) : new Vector2(Mathf.Sign(axis.x), 0f);
            Vector2 side = new Vector2(-axis.y, axis.x);

            Vector2 start = a.center + axis * a.half; // the two doorways, on the rooms' nominal walls
            Vector2 end = b.center - axis * b.half;
            Vector2 mid = (start + end) / 2f;
            float length = Vector2.Distance(start, end);
            string name = $"Corridor_{a.name}_{b.name}";
            var rng = new System.Random(Seed(name));
            bool cold = IsCold(a) && IsCold(b);
            wall = Cave(cold ? "cave_wall_blue" : "cave_wall_brown") ?? wall;
            floor = Cave(cold ? "cave_ground_grey" : "cave_ground_brown") ?? floor;

            // Floor runs a little into both rooms so there is no seam at the doorway.
            var floorBox = Box(parent, name + "_Floor", new Vector3(mid.x, -0.5f, mid.y), new Vector3(CorridorWidth + WallThickness, 1f, length + 2f), floor);
            floorBox.transform.localRotation = Quaternion.LookRotation(new Vector3(axis.x, 0f, axis.y));

            for (int s = -1; s <= 1; s += 2)
            {
                var pts = new List<Vector2> { start + side * (s * CorridorWidth / 2f) };
                int pieces = Mathf.Max(2, Mathf.RoundToInt(length / 1.5f));
                for (int i = 1; i < pieces; i++)
                {
                    float t = i / (float)pieces;
                    float taper = Mathf.Clamp01(Mathf.Min(t, 1f - t) * length / 1.2f);
                    Vector2 p = Vector2.Lerp(start, end, t) + side * (s * CorridorWidth / 2f);
                    pts.Add(p - side * s * Range(rng, -0.1f, 0.35f) * taper); // pinches in a little, never out
                }

                pts.Add(end + side * (s * CorridorWidth / 2f));
                BuildWallChain(parent, $"{name}_{(s < 0 ? "A" : "B")}", pts, Vector2.zero, wall, rng);
            }
        }

        /// <summary>Stone columns near the room corners, to break up the big floors. They keep clear of everything placed in the dungeon (props, enemies, patrol routes).</summary>
        static void AddPillars(Transform parent, Material material, params Transform[] occupied)
        {
            var points = new List<Vector2>();
            var segments = new List<(Vector2, Vector2)>();
            foreach (var root in occupied)
            {
                if (root == null) continue;
                foreach (var t in root.GetComponentsInChildren<Transform>()) points.Add(new Vector2(t.position.x, t.position.z));

                foreach (var route in root.GetComponentsInChildren<Transform>())
                {
                    if (!route.name.EndsWith("_Route") || route.childCount < 2) continue;
                    for (int i = 0; i < route.childCount; i++)
                    {
                        var p = route.GetChild(i).position;
                        var q = route.GetChild((i + 1) % route.childCount).position;
                        segments.Add((new Vector2(p.x, p.z), new Vector2(q.x, q.z)));
                    }
                }
            }

            foreach (var room in Rooms.Values)
            {
                var rng = new System.Random(Seed(room.name) + 7);
                float reach = room.half - 3.2f;
                int index = 0;
                foreach (var corner in new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                {
                    index++;
                    if (rng.NextDouble() < 0.3) continue;

                    Vector2 p = room.center + corner * reach + new Vector2(Jitter(rng, 0.6f), Jitter(rng, 0.6f));
                    if (IsOccupied(p, points, segments, 2.6f)) continue;

                    float radius = Range(rng, 0.45f, 0.7f);
                    float height = Range(rng, 3.4f, 4.4f);
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pillar.name = $"{room.name}_Pillar{index}";
                    pillar.transform.SetParent(parent, false);
                    pillar.transform.localPosition = new Vector3(p.x, height / 2f, p.y);
                    pillar.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
                    pillar.GetComponent<MeshRenderer>().sharedMaterial = WallFor(room, material);
                }
            }
        }

        static bool IsOccupied(Vector2 p, List<Vector2> points, List<(Vector2, Vector2)> segments, float clearance)
        {
            foreach (var q in points) if (Vector2.Distance(p, q) < clearance) return true;
            foreach (var (a, b) in segments)
            {
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                if (Vector2.Distance(p, a + ab * t) < clearance) return true;
            }

            return false;
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static LootItem Loot(string id, int count) => new LootItem { itemId = id, count = count };

        static void Chest(Transform parent, Material material, string id, Vector3 position,
            LootItem[] items = null, string[] weapons = null, string[] armor = null)
        {
            var go = Box(parent, "Chest_" + id, position + new Vector3(0, 0.35f, 0), new Vector3(1f, 0.7f, 0.65f), material);
            var chest = go.AddComponent<LootContainer>();
            chest.interactionId = id;
            chest.prompt = "Open chest";
            chest.openedRenderer = go.GetComponent<MeshRenderer>();
            if (items != null) chest.items = new List<LootItem>(items);
            if (weapons != null) chest.weaponIds = new List<string>(weapons);
            if (armor != null) chest.armorIds = new List<string>(armor);
        }

        static void RestShrine(Transform parent, Material material, string id, Vector3 position, int uses)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Shrine_" + id;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position + new Vector3(0, 0.6f, 0);
            go.transform.localScale = new Vector3(0.9f, 0.6f, 0.9f);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var rest = go.AddComponent<RestPoint>();
            rest.interactionId = id;
            rest.prompt = "Pray at the shrine";
            rest.uses = uses;
            rest.glowRenderer = go.GetComponent<MeshRenderer>();
        }

        static void Door(Transform parent, Material material, string id, Vector3 position, string[] keys)
        {
            var go = Box(parent, "Door_" + id, position + new Vector3(0, WallHeight / 2f, 0), new Vector3(CorridorWidth + WallThickness, WallHeight, 0.5f), material);
            var door = go.AddComponent<LockedDoor>();
            door.interactionId = id;
            door.prompt = "Unlock gate";
            door.range = 3f;
            door.acceptedItemIds = new List<string>(keys);
        }

        static void Trap(Transform parent, Material material, string id, Vector3 position, int damage)
        {
            var plate = Box(parent, "Trap_" + id, position + new Vector3(0, 0.02f, 0), new Vector3(3f, 0.04f, 3f), material);
            var collider = plate.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(1f, 40f, 1f); // tall enough to catch the CharacterController
            var trap = plate.AddComponent<TrapZone>();
            trap.trapId = id;
            trap.damage = damage;
            trap.message = "A pressure plate clicks and darts fly!";
        }

        static readonly Color EliteGem = new Color(0.6f, 0.05f, 0.7f);

        static OverworldEnemy Enemy(Transform parent, string id, string encounterName, Vector3 position, params Vector3[] route)
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyTemplatePath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(template, parent);
            go.name = "Enemy_" + id;
            go.transform.localPosition = position;

            var enemy = go.GetComponent<OverworldEnemy>();
            enemy.enemyId = "dungeon02_" + id;
            enemy.encounter = LoadEncounter(encounterName);

            if (route.Length > 0)
            {
                var routeRoot = new GameObject("Enemy_" + id + "_Route").transform;
                routeRoot.SetParent(parent, false);
                var points = new Transform[route.Length];
                for (int i = 0; i < route.Length; i++)
                {
                    var p = new GameObject("P" + i).transform;
                    p.SetParent(routeRoot, false);
                    p.localPosition = route[i];
                    points[i] = p;
                }
                enemy.patrolPoints = points;
            }

            return enemy;
        }

        // ------------------------------------------------------------------ helpers

        static Material LoadMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{SceneFolder}/{name}.mat");

        static Material EnsureMaterial(string name, Color color, Material like)
        {
            var path = $"{SceneFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(like != null ? like.shader : Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static T Create<T>(string folder, string id, string displayName, System.Action<T> configure) where T : GameDefinition
        {
            var path = $"{DataRoot}/{folder}/{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.id = id;
            asset.displayName = displayName;
            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
