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
        const float RoomSize = 12.5f;
        const float HalfRoom = 6f;
        const float WallHeight = 3f;
        const float WallThickness = 0.5f;
        const float DoorGap = 3.5f;

        enum Dir { N, E, S, W }

        sealed class Room
        {
            public string name;
            public Vector2 center;
            public readonly HashSet<Dir> openings = new HashSet<Dir>();
        }

        static readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();

        [MenuItem("Tools/Overworld/Build Demo Dungeon (Dungeon_02)")]
        public static void Run()
        {
            CreateContent();
            BuildScene();
        }

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
            AddRoom("BossRoom", 0, 80);

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

        static void AddRoom(string name, float x, float z) => Rooms[name] = new Room { name = name, center = new Vector2(x, z) };

        static Dir Opposite(Dir d) => d == Dir.N ? Dir.S : d == Dir.S ? Dir.N : d == Dir.E ? Dir.W : Dir.E;

        static void BuildRoom(Transform parent, Room room, Material wall, Material floor)
        {
            var c = room.center;
            Box(parent, room.name + "_Floor", new Vector3(c.x, -0.5f, c.y), new Vector3(RoomSize, 1f, RoomSize), floor);

            foreach (Dir dir in System.Enum.GetValues(typeof(Dir)))
            {
                bool horizontal = dir == Dir.N || dir == Dir.S; // wall runs along X
                Vector3 offset = dir == Dir.N ? new Vector3(0, 0, HalfRoom) : dir == Dir.S ? new Vector3(0, 0, -HalfRoom)
                    : dir == Dir.E ? new Vector3(HalfRoom, 0, 0) : new Vector3(-HalfRoom, 0, 0);
                Vector3 wallCenter = new Vector3(c.x, WallHeight / 2f, c.y) + offset;
                string baseName = $"{room.name}_{dir}";

                if (!room.openings.Contains(dir))
                {
                    Box(parent, baseName, wallCenter, horizontal ? new Vector3(RoomSize, WallHeight, WallThickness) : new Vector3(WallThickness, WallHeight, RoomSize), wall);
                    continue;
                }

                float seg = (RoomSize - DoorGap) / 2f;
                float shift = DoorGap / 2f + seg / 2f;
                for (int i = 0; i < 2; i++)
                {
                    float s = i == 0 ? -shift : shift;
                    Vector3 pos = wallCenter + (horizontal ? new Vector3(s, 0, 0) : new Vector3(0, 0, s));
                    Vector3 size = horizontal ? new Vector3(seg, WallHeight, WallThickness) : new Vector3(WallThickness, WallHeight, seg);
                    Box(parent, baseName + (i == 0 ? "_A" : "_B"), pos, size, wall);
                }
            }
        }

        static void BuildCorridor(Transform parent, Room a, Room b, Material wall, Material floor)
        {
            Vector2 mid = (a.center + b.center) / 2f;
            bool vertical = Mathf.Abs(b.center.y - a.center.y) > Mathf.Abs(b.center.x - a.center.x);
            float length = 20f - 2f * HalfRoom; // gap between the two room walls
            string name = $"Corridor_{a.name}_{b.name}";

            if (vertical)
            {
                Box(parent, name + "_Floor", new Vector3(mid.x, -0.5f, mid.y), new Vector3(3f, 1f, length), floor);
                Box(parent, name + "_W", new Vector3(mid.x - 1.75f, WallHeight / 2f, mid.y), new Vector3(WallThickness, WallHeight, length), wall);
                Box(parent, name + "_E", new Vector3(mid.x + 1.75f, WallHeight / 2f, mid.y), new Vector3(WallThickness, WallHeight, length), wall);
            }
            else
            {
                Box(parent, name + "_Floor", new Vector3(mid.x, -0.5f, mid.y), new Vector3(length, 1f, 3f), floor);
                Box(parent, name + "_S", new Vector3(mid.x, WallHeight / 2f, mid.y - 1.75f), new Vector3(length, WallHeight, WallThickness), wall);
                Box(parent, name + "_N", new Vector3(mid.x, WallHeight / 2f, mid.y + 1.75f), new Vector3(length, WallHeight, WallThickness), wall);
            }
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
            var go = Box(parent, "Door_" + id, position + new Vector3(0, WallHeight / 2f, 0), new Vector3(3.6f, WallHeight, 0.5f), material);
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
