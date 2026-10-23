using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using Game.Combat.UI.UGUI;
using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Services;
using UnityEditor;
using UnityEditor.AI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// One-off batch-mode setup for the Trails Battle module: builds BattleArena_01 (baked
    /// NavMesh included), the placeholder combatant/EncounterRunner prefabs, the
    /// CombatSystemConfig asset, one sample AOE skill, and the sample EncounterDefinition.
    /// Safe to re-run — every step overwrites its own output rather than appending.
    /// Invoke via: -batchmode -quit -executeMethod Game.EditorTools.CombatModuleSetup.Run
    /// </summary>
    public static class CombatModuleSetup
    {
        private const string BattleScenePath = "Assets/_Project/Scenes/Battle/BattleArena_01.unity";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Combat/TrailsBattle3D";
        private const string UiPrefabFolder = "Assets/_Project/Prefabs/Combat/UI";
        private const string ConfigFolder = "Assets/_Project/Config";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        public static void Run()
        {
            Debug.Log("[CombatModuleSetup] Starting...");

            EnsureFolder(ConfigFolder);

            BuildBattleArenaScene();
            RegisterSceneInBuildSettings(BattleScenePath);

            var heroPrefab = CreatePlaceholderPrefab(PrimitiveType.Capsule, "HeroPlaceholder");
            var enemyPrefab = CreatePlaceholderPrefab(PrimitiveType.Cube, "EnemyPlaceholder");
            var encounterRunnerPrefab = CreateEncounterRunnerPrefab();
            var hudPrefab = BattleHudBuilder.Build();

            var combatSystemConfig = CreateCombatSystemConfig(encounterRunnerPrefab, hudPrefab);

            AssignBattleModelPrefab("Assets/_Project/Resources/Data/Actors/ActorTestHero.asset", heroPrefab);
            AssignBattleModelPrefab("Assets/_Project/Resources/Data/Enemies/EnemyTestSlime.asset", enemyPrefab);

            CreateAoeSkill();
            AddAoeSkillToHero();

            var encounter = CreateSampleEncounter();
            WireDebugTriggerInSampleScene(combatSystemConfig, encounter);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CombatModuleSetup] Done.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderName = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static void BuildBattleArenaScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "ArenaFloor";
            floor.transform.localScale = new Vector3(3f, 1f, 3f);
            floor.AddComponent<BattleArenaFloor>();
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var cameraObject = new GameObject("Battle Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 12f, -14f);
            camera.transform.LookAt(Vector3.zero);
            cameraObject.tag = "MainCamera";

            var spawnRoot = new GameObject("BattleArenaSpawnPoints");
            var spawnPoints = spawnRoot.AddComponent<BattleArenaSpawnPoints>();

            spawnPoints.partySpawnPoints = new[]
            {
                CreateSpawnMarker(spawnRoot.transform, "Party_1", new Vector3(-4f, 0f, -5f)),
                CreateSpawnMarker(spawnRoot.transform, "Party_2", new Vector3(0f, 0f, -5f)),
                CreateSpawnMarker(spawnRoot.transform, "Party_3", new Vector3(4f, 0f, -5f))
            };

            spawnPoints.enemySpawnPoints = new[]
            {
                CreateSpawnMarker(spawnRoot.transform, "Enemy_1", new Vector3(-6f, 0f, 5f)),
                CreateSpawnMarker(spawnRoot.transform, "Enemy_2", new Vector3(-2f, 0f, 5f)),
                CreateSpawnMarker(spawnRoot.transform, "Enemy_3", new Vector3(2f, 0f, 5f)),
                CreateSpawnMarker(spawnRoot.transform, "Enemy_4", new Vector3(6f, 0f, 5f))
            };

            // The scene must already have a path on disk before baking: NavMeshBuilder
            // saves the resulting NavMeshData asset alongside the scene file, and silently
            // discards the bake (leaving NavMeshSettings.m_NavMeshData at fileID 0) if the
            // scene hasn't been saved yet.
            Directory.CreateDirectory(Path.GetDirectoryName(BattleScenePath)!);
            EditorSceneManager.SaveScene(scene, BattleScenePath);

            NavMeshBuilder.BuildNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BattleScenePath);

            Debug.Log($"[CombatModuleSetup] Battle arena scene saved to {BattleScenePath} with baked NavMesh.");
        }

        private static Transform CreateSpawnMarker(Transform parent, string name, Vector3 localPosition)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent);
            marker.transform.localPosition = localPosition;
            return marker.transform;
        }

        private static void RegisterSceneInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[CombatModuleSetup] Registered {scenePath} in Build Settings.");
        }

        private static GameObject CreatePlaceholderPrefab(PrimitiveType primitiveType, string prefabName)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var instance = GameObject.CreatePrimitive(primitiveType);
            instance.name = prefabName;
            instance.AddComponent<CombatantView>();

            EnsureFolder(PrefabFolder);
            var path = $"{PrefabFolder}/{prefabName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            Debug.Log($"[CombatModuleSetup] Created placeholder prefab at {path}.");
            return prefab;
        }

        private static GameObject CreateEncounterRunnerPrefab()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var instance = new GameObject("EncounterRunner");
            instance.AddComponent<EncounterRunner>();
            instance.AddComponent<TargetingInput>();

            EnsureFolder(PrefabFolder);
            var path = $"{PrefabFolder}/EncounterRunner.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);

            Debug.Log($"[CombatModuleSetup] Created EncounterRunner prefab at {path}.");
            return prefab;
        }

        private static CombatSystemConfig CreateCombatSystemConfig(GameObject encounterRunnerPrefab, GameObject hudPrefab)
        {
            var path = $"{ConfigFolder}/CombatSystemConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<CombatSystemConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CombatSystemConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            BindSerialized(config, new Dictionary<string, Object>
            {
                { "encounterRunnerPrefab", encounterRunnerPrefab },
                { "hudPrefab", hudPrefab }
            });
            EditorUtility.SetDirty(config);

            Debug.Log($"[CombatModuleSetup] CombatSystemConfig ready at {path}.");
            return config;
        }

        /// <summary>
        /// Assigns Unity Object references to private [SerializeField] fields by name via
        /// SerializedObject — works across assemblies/access modifiers since it operates on
        /// serialized data, not C# reflection rules.
        /// </summary>
        private static void BindSerialized(Object target, Dictionary<string, Object> fieldValues)
        {
            var serialized = new SerializedObject(target);
            foreach (var kvp in fieldValues)
            {
                var property = serialized.FindProperty(kvp.Key);
                if (property == null)
                {
                    Debug.LogError($"[CombatModuleSetup] Property '{kvp.Key}' not found on {target.GetType().Name}.");
                    continue;
                }

                property.objectReferenceValue = kvp.Value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBattleModelPrefab(string definitionAssetPath, GameObject prefab)
        {
            var actor = AssetDatabase.LoadAssetAtPath<ActorDefinition>(definitionAssetPath);
            if (actor != null)
            {
                actor.battleModelPrefab = prefab;
                EditorUtility.SetDirty(actor);
                return;
            }

            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(definitionAssetPath);
            if (enemy != null)
            {
                enemy.battleModelPrefab = prefab;
                EditorUtility.SetDirty(enemy);
                return;
            }

            Debug.LogError($"[CombatModuleSetup] Could not find an ActorDefinition or EnemyDefinition at {definitionAssetPath}.");
        }

        private static void CreateAoeSkill()
        {
            const string path = "Assets/_Project/Resources/Data/Skills/SkillTestAoeBurst.asset";
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<SkillDefinition>();
                AssetDatabase.CreateAsset(skill, path);
            }

            skill.id = "skill_test_aoe_burst";
            skill.displayName = "Test AOE Burst";
            skill.description = "Sample AOE craft used by the Trails Battle smoke test / manual play.";
            skill.actionCost = ActionCost.Action;
            skill.targetType = TargetType.AreaCircle;
            skill.range = 4;
            skill.damage = new DiceRoll(1, 6);
            skill.apCost = 0;
            skill.damageType = DamageType.Fire;
            skill.statusEffectId = "status_test_poison";
            skill.statusEffectChance = 0.5f;

            EditorUtility.SetDirty(skill);
            Debug.Log($"[CombatModuleSetup] Sample AOE skill ready at {path}.");
        }

        private static void AddAoeSkillToHero()
        {
            var actor = AssetDatabase.LoadAssetAtPath<ActorDefinition>("Assets/_Project/Resources/Data/Actors/ActorTestHero.asset");
            if (actor == null)
            {
                return;
            }

            if (!actor.startingSkillIds.Contains("skill_test_aoe_burst"))
            {
                actor.startingSkillIds.Add("skill_test_aoe_burst");
                EditorUtility.SetDirty(actor);
            }
        }

        private static EncounterDefinition CreateSampleEncounter()
        {
            const string path = "Assets/_Project/Resources/Data/Encounters/EncounterSampleSkirmish.asset";
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
            if (encounter == null)
            {
                encounter = ScriptableObject.CreateInstance<EncounterDefinition>();
                AssetDatabase.CreateAsset(encounter, path);
            }

            encounter.partyActorIds = new List<string> { "actor_test_hero", "actor_test_hero", "actor_test_hero" };
            encounter.enemyIds = new List<string> { "enemy_test_slime", "enemy_test_slime" };

            EditorUtility.SetDirty(encounter);
            Debug.Log($"[CombatModuleSetup] Sample EncounterDefinition ready at {path}.");
            return encounter;
        }

        private static void WireDebugTriggerInSampleScene(CombatSystemConfig config, EncounterDefinition encounter)
        {
            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

            var existing = Object.FindObjectOfType<DebugEncounterTrigger>();
            var triggerObject = existing != null ? existing.gameObject : new GameObject("DebugCombatBootstrap");
            var trigger = triggerObject.GetComponent<DebugEncounterTrigger>();
            if (trigger == null)
            {
                trigger = triggerObject.AddComponent<DebugEncounterTrigger>();
            }

            trigger.combatSystemConfig = config;
            trigger.sampleEncounter = encounter;
            trigger.hotkey = KeyCode.F5;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[CombatModuleSetup] Wired DebugEncounterTrigger (F5) into SampleScene.");
        }
    }
}
