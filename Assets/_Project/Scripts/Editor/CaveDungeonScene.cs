using System.Collections.Generic;
using System.IO;
using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Overworld;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds Dungeon_03, the playable cave dungeon: the scene is copied from Dungeon_02 (player, party, camera, director, HUD wiring),
    /// then everything dungeon-specific is replaced by the CaveDungeon_Sample geometry from the cave kit, plus enemies, chests,
    /// shrines, a locked gate and a boss. Also drops entrance portals into Dungeon_01. Re-runnable.
    /// Run via: Tools/Overworld/Build Cave Dungeon (Dungeon_03), or -executeMethod Game.EditorTools.CaveDungeonScene.Run
    /// </summary>
    public static class CaveDungeonScene
    {
        const string SceneFolder = "Assets/_Project/Scenes/Overworld";
        const string SourceScenePath = SceneFolder + "/Dungeon_02.unity";
        const string ScenePath = SceneFolder + "/Dungeon_03.unity";
        const string Dungeon01Path = SceneFolder + "/Dungeon_01.unity";
        const string PrefabPath = "Assets/_Project/Prefabs/Cave/CaveDungeon_Sample.prefab";

        const string Potion = "item_healing_potion";
        const string CaveKey = "item_cave_key";
        const string IdPrefix = "dungeon03_";

        const float Cell = CaveDungeonBuilder.Cell;

        [MenuItem("Tools/Overworld/Build Cave Dungeon (Dungeon_03)")]
        public static void Run()
        {
            DemoDungeonSetup.CreateContent();
            DemoDungeonSetup.Create<ItemDefinition>("Items", CaveKey, "Cave Key", i =>
            {
                i.description = "A rusted key with moss on the teeth. It opens the gate to the deep cavern.";
                i.itemType = ItemType.KeyItem;
                i.stackable = false;
                i.maxStack = 1;
            });
            AssetDatabase.SaveAssets();

            CaveDungeonBuilder.BuildPrefab();
            BuildScene();
            AddPortalsToDungeon01();
            AssetDatabase.Refresh();
        }

        // Cell coordinates (the same grid CaveDungeonBuilder carves) to a world position.
        static Vector3 At(float cellX, float cellZ) => new Vector3(cellX * Cell, 0f, cellZ * Cell);

        static void BuildScene()
        {
            if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset(SourceScenePath, ScenePath);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find("OverworldRoot");
            if (root == null) { Debug.LogError("[CaveDungeonScene] OverworldRoot not found in " + ScenePath); return; }

            DemoDungeonSetup.EnsureEnemyTemplate(root);

            foreach (var name in new[] { "Dungeon", "Enemies", "Props", "Doors", "Roofs" })
            {
                var old = root.transform.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            var dungeon = new GameObject("Dungeon").transform;
            dungeon.SetParent(root.transform, false);
            var props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            var doors = new GameObject("Doors").transform;
            doors.SetParent(root.transform, false);
            var enemies = new GameObject("Enemies").transform;
            enemies.SetParent(root.transform, false);

            // Geometry: unpack the sample prefab so the roofs can live outside the NavMesh surface and get the cutaway behaviour.
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            foreach (var name in new[] { "Floors", "Walls", "Posts", "Decor", "Lights" })
            {
                instance.transform.Find(name).SetParent(dungeon, false);
            }
            var roofs = instance.transform.Find("Roofs");
            roofs.SetParent(root.transform, false);
            roofs.gameObject.AddComponent<RoofCutaway>();
            Object.DestroyImmediate(instance);

            var wall = DemoDungeonSetup.LoadMaterial("Greybox_Wall");
            var chestMat = DemoDungeonSetup.EnsureMaterial("Greybox_Chest", new Color(0.55f, 0.36f, 0.14f), wall);
            var shrineMat = DemoDungeonSetup.EnsureMaterial("Greybox_Shrine", new Color(0.3f, 0.75f, 0.95f), wall);
            var doorMat = DemoDungeonSetup.EnsureMaterial("Greybox_Door", new Color(0.4f, 0.25f, 0.12f), wall);
            var portalMat = DemoDungeonSetup.EnsureMaterial("Greybox_Portal", new Color(0.35f, 0.9f, 0.7f), wall);

            // --- A: start chamber. The player spawns here; a portal leads back to Dungeon_01.
            var spawn = At(2.5f, 2f);
            Portal(props, portalMat, "portal_exit", At(3, 1) + new Vector3(0f, 0f, 0.5f), "Dungeon_01", "Leave the cave");
            DemoDungeonSetup.Chest(props, chestMat, "chest_start", At(5, 3) + new Vector3(0.4f, 0f, 0.3f), items: new[] { DemoDungeonSetup.Loot(Potion, 1) });

            // --- B: pillared hall, first fight
            Enemy(enemies, "hall_patrol", "EncounterDemoHall", At(13.5f, 3f), At(11.5f, 3f), At(15.5f, 3f));
            DemoDungeonSetup.Chest(props, chestMat, "chest_hall", At(17, 1) + new Vector3(-0.2f, 0f, 0.4f), weapons: new[] { "weapon_keen_dagger" });

            // --- C: lower hall (skeletons, shrine)
            Enemy(enemies, "lower_skeletons", "EncounterDemoCrypt", At(13.5f, 10.5f), At(11.5f, 10.5f), At(16f, 10.5f));
            DemoDungeonSetup.RestShrine(props, shrineMat, "shrine_lower", At(16.5f, 12) + new Vector3(0f, 0f, 0.2f), uses: 1);
            DemoDungeonSetup.Chest(props, chestMat, "chest_lower", At(10, 12) + new Vector3(0.3f, 0f, 0.3f),
                items: new[] { DemoDungeonSetup.Loot(Potion, 1) }, armor: new[] { "armor_studded_leather" });

            // --- E: treasure chamber, guarded by a captain; holds the key to the boss cavern
            Enemy(enemies, "treasure_captain", "EncounterDemoGuardPost", At(3.5f, 9f), At(2f, 9f), At(5f, 9f)).patrolColor = DemoDungeonSetup.EliteGem;
            DemoDungeonSetup.Chest(props, chestMat, "chest_treasure_key", At(1, 11) + new Vector3(0.2f, 0f, 0.2f),
                items: new[] { DemoDungeonSetup.Loot(CaveKey, 1), DemoDungeonSetup.Loot(Potion, 1) });
            DemoDungeonSetup.Chest(props, chestMat, "chest_treasure_gear", At(6, 11) + new Vector3(-0.2f, 0f, 0.2f),
                weapons: new[] { "weapon_longsword" }, armor: new[] { "armor_banded_mail" });

            // --- Gate: the corridor between the hall and the boss cavern
            var gate = At(18.5f, 3f);
            DemoDungeonSetup.Door(doors, doorMat, "door_cave_gate", gate, new[] { CaveKey });
            var door = doors.GetChild(0);
            door.localRotation = Quaternion.Euler(0f, 90f, 0f);
            door.localPosition = gate + new Vector3(0f, 2f, 0f);
            door.localScale = new Vector3(DemoDungeonSetup.CorridorWidth + DemoDungeonSetup.WallThickness, 4f, 0.5f); // full cave height

            // --- D: boss cavern
            DemoDungeonSetup.RestShrine(props, shrineMat, "shrine_boss", At(21, 1) + new Vector3(0f, 0f, 0.3f), uses: 1);
            var boss = Enemy(enemies, "boss_warlord", "EncounterDemoBoss", At(23.5f, 3f));
            boss.patrolColor = DemoDungeonSetup.EliteGem;
            boss.endsDemo = true;
            boss.viewRadius = 9f;
            boss.transform.localScale = Vector3.one * 1.35f;

            // --- Player: spawn and respawn in the start chamber
            var player = root.transform.Find("Player");
            if (player != null)
            {
                var controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                player.position = spawn;
                if (controller != null) controller.enabled = true;
            }
            var spawnPoint = root.transform.Find("PlayerSpawn");
            if (spawnPoint != null) spawnPoint.position = spawn;

            // --- NavMesh over the dungeon geometry only (roofs, props and doors are outside it on purpose)
            var surface = dungeon.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            DemoDungeonSetup.AddToBuildSettings(ScenePath);
            Debug.Log("[CaveDungeonScene] Dungeon_03 built and saved.");
        }

        static OverworldEnemy Enemy(Transform parent, string id, string encounter, Vector3 position, params Vector3[] route)
        {
            var enemy = DemoDungeonSetup.Enemy(parent, id, encounter, position, route);
            enemy.enemyId = IdPrefix + id; // keep defeated-enemy memory separate from Dungeon_02
            return enemy;
        }

        static void Portal(Transform parent, Material material, string id, Vector3 position, string scene, string prompt)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Portal_" + id;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position + new Vector3(0f, 1.2f, 0f);
            go.transform.localScale = new Vector3(1.4f, 1.2f, 1.4f);
            Object.DestroyImmediate(go.GetComponent<Collider>()); // walk-through marker; the prompt does the work
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var portal = go.AddComponent<DungeonPortal>();
            portal.interactionId = id;
            portal.sceneName = scene;
            portal.prompt = prompt;
            portal.range = 2.8f;
        }

        // Entrances in Dungeon_01 next to the player spawn, so every dungeon scene is reachable in play.
        static void AddPortalsToDungeon01()
        {
            var scene = EditorSceneManager.OpenScene(Dungeon01Path, OpenSceneMode.Single);
            var root = GameObject.Find("OverworldRoot");
            var spawn = root != null ? root.transform.Find("PlayerSpawn") : null;
            if (spawn == null) { Debug.LogWarning("[CaveDungeonScene] Dungeon_01 has no PlayerSpawn; portals not added."); return; }

            var old = root.transform.Find("Portals");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("Portals").transform;
            group.SetParent(root.transform, false);

            var material = DemoDungeonSetup.EnsureMaterial("Greybox_Portal", new Color(0.35f, 0.9f, 0.7f), DemoDungeonSetup.LoadMaterial("Greybox_Wall"));
            Portal(group, material, "to_dungeon02", spawn.position + new Vector3(-3f, 0f, 3f), "Dungeon_02", "Enter the Demo Dungeon");
            Portal(group, material, "to_dungeon03", spawn.position + new Vector3(3f, 0f, 3f), "Dungeon_03", "Enter the Cave Dungeon");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, Dungeon01Path);
            Debug.Log("[CaveDungeonScene] Portals added to Dungeon_01 at " + spawn.position);
        }
    }
}
