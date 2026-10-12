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
            var hudPrefab = BuildBattleHudPrefab();

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

        /// <summary>SerializedProperty array elements need arraySize + per-index
        /// objectReferenceValue, not a single objectReferenceValue assignment — BindSerialized
        /// only handles scalar Object fields.</summary>
        private static void BindDotsArray(Object target, Image[] dots)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty("dots");
            if (property == null)
            {
                Debug.LogError($"[CombatModuleSetup] Property 'dots' not found on {target.GetType().Name}.");
                return;
            }

            property.arraySize = dots.Length;
            for (int i = 0; i < dots.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = dots[i];
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
            skill.resourceCost = 8;
            skill.power = 6;
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

        private static GameObject BuildBattleHudPrefab()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var allyNameplatePrefab = BuildNameplatePrefab("AllyNameplate", new Color(0.25f, 0.6f, 0.95f));
            var enemyNameplatePrefab = BuildNameplatePrefab("EnemyNameplate", new Color(0.85f, 0.2f, 0.2f));

            var root = new GameObject("BattleHUD");
            var hudRoot = root.AddComponent<BattleHudRoot>();

            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.transform.SetParent(root.transform, false);
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();

            var canvasGO = new GameObject("OverlayCanvas");
            canvasGO.transform.SetParent(root.transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasGO.AddComponent<GraphicRaycaster>();

            // First child of the canvas so nameplates draw beneath every other HUD panel.
            var nameplateLayerGO = new GameObject("NameplateLayer", typeof(RectTransform));
            nameplateLayerGO.transform.SetParent(canvasGO.transform, false);
            var nameplateLayer = nameplateLayerGO.GetComponent<RectTransform>();
            SetStretch(nameplateLayer, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one);

            var commandMenu = BuildCommandMenu(canvasGO.transform);
            var actingUnitCard = BuildActingUnitCard(canvasGO.transform);
            var turnBanner = BuildTurnBanner(canvasGO.transform);
            var turnOrderStrip = BuildTurnOrderStrip(canvasGO.transform);
            var resultPanel = BuildResultPanel(canvasGO.transform);
            var hoverStatsPanel = BuildHoverStatsPanel(canvasGO.transform);
            var damageRollBar = BuildDamageRollBar(canvasGO.transform);

            BindSerialized(hudRoot, new Dictionary<string, Object>
            {
                { "commandMenu", commandMenu },
                { "actingUnitCard", actingUnitCard },
                { "turnBanner", turnBanner },
                { "turnOrderStrip", turnOrderStrip },
                { "resultPanel", resultPanel },
                { "allyNameplatePrefab", allyNameplatePrefab },
                { "enemyNameplatePrefab", enemyNameplatePrefab },
                { "nameplateLayer", nameplateLayer },
                { "hoverStatsPanel", hoverStatsPanel },
                { "damageRollBar", damageRollBar }
            });

            EnsureFolder(UiPrefabFolder);
            var path = $"{UiPrefabFolder}/BattleHUD.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Debug.Log($"[CombatModuleSetup] Created BattleHUD prefab at {path}.");
            return prefab;
        }

        private static CommandMenuController BuildCommandMenu(Transform parent)
        {
            var rootGO = new GameObject("CommandMenu", typeof(RectTransform));
            rootGO.transform.SetParent(parent, false);
            var commandMenu = rootGO.AddComponent<CommandMenuController>();

            var menuRootGO = new GameObject("MenuRoot", typeof(RectTransform));
            menuRootGO.transform.SetParent(rootGO.transform, false);
            var menuRootRt = menuRootGO.GetComponent<RectTransform>();
            AnchorBottomCenter(menuRootRt, new Vector2(0f, 20f));
            var menuLayout = menuRootGO.AddComponent<HorizontalLayoutGroup>();
            menuLayout.spacing = 8f;
            menuLayout.childAlignment = TextAnchor.MiddleCenter;
            menuRootGO.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var attackButton = CreateButton(menuRootGO.transform, "AttackButton", "Attack", new Vector2(90f, 36f));
            var craftButton = CreateButton(menuRootGO.transform, "CraftButton", "Craft", new Vector2(90f, 36f));
            var guardButton = CreateButton(menuRootGO.transform, "GuardButton", "Guard", new Vector2(90f, 36f));

            var craftSubmenu = BuildCraftSubmenu(rootGO.transform);

            BindSerialized(commandMenu, new Dictionary<string, Object>
            {
                { "menuRoot", menuRootGO },
                { "attackButton", attackButton },
                { "craftButton", craftButton },
                { "guardButton", guardButton },
                { "craftSubmenu", craftSubmenu }
            });

            menuRootGO.SetActive(false);
            return commandMenu;
        }

        private static CraftSubmenu BuildCraftSubmenu(Transform parent)
        {
            var rootGO = new GameObject("CraftSubmenu", typeof(RectTransform));
            rootGO.transform.SetParent(parent, false);
            var craftSubmenu = rootGO.AddComponent<CraftSubmenu>();

            var submenuRootGO = new GameObject("SubmenuRoot", typeof(RectTransform));
            submenuRootGO.transform.SetParent(rootGO.transform, false);
            var submenuRootRt = submenuRootGO.GetComponent<RectTransform>();
            AnchorBottomCenter(submenuRootRt, new Vector2(0f, 20f));
            submenuRootGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var submenuVertical = submenuRootGO.AddComponent<VerticalLayoutGroup>();
            submenuVertical.padding = new RectOffset(10, 10, 10, 10);
            submenuVertical.spacing = 4f;
            submenuVertical.childAlignment = TextAnchor.MiddleCenter;
            submenuRootGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var listParentGO = new GameObject("ListParent", typeof(RectTransform));
            listParentGO.transform.SetParent(submenuRootGO.transform, false);
            var listVertical = listParentGO.AddComponent<VerticalLayoutGroup>();
            listVertical.spacing = 4f;
            listParentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var listParentRt = listParentGO.GetComponent<RectTransform>();

            var entryTemplate = CreateButton(listParentGO.transform, "EntryTemplate", "Skill", new Vector2(180f, 32f));
            entryTemplate.gameObject.SetActive(false);

            var backButton = CreateButton(submenuRootGO.transform, "BackButton", "Back", new Vector2(180f, 28f));

            BindSerialized(craftSubmenu, new Dictionary<string, Object>
            {
                { "submenuRoot", submenuRootGO },
                { "listParent", listParentRt },
                { "entryTemplate", entryTemplate },
                { "backButton", backButton }
            });

            submenuRootGO.SetActive(false);
            return craftSubmenu;
        }

        /// <summary>
        /// A single always-visible "whoever's turn it is" card (ally or enemy), matching the
        /// Duelwright Table reference's one-card-only layout — not a per-ally roster list.
        /// Basic structural placeholder; the hand-tuned live BattleHUD.prefab carries the real
        /// visual polish (colors/borders/fear dots) applied directly via the Editor, same as
        /// activeTurnIndicator/targetingPrompt before it — see project memory.
        /// </summary>
        private static ActingUnitCard BuildActingUnitCard(Transform parent)
        {
            var rootGO = new GameObject("ActingUnitCard", typeof(RectTransform));
            rootGO.transform.SetParent(parent, false);
            var rootRt = rootGO.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 0f);
            rootRt.anchorMax = new Vector2(0f, 0f);
            rootRt.pivot = new Vector2(0f, 0f);
            rootRt.anchoredPosition = new Vector2(20f, 20f);
            rootRt.sizeDelta = new Vector2(260f, 150f);
            rootGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            var card = rootGO.AddComponent<ActingUnitCard>();

            BindSerialized(card, BuildStatCardElements(rootGO));

            rootGO.SetActive(false);
            return card;
        }

        /// <summary>
        /// Whoever's under the cursor (ally or enemy), independent of whose turn it is — same
        /// stat set/field names as ActingUnitCard (hence sharing BuildStatCardElements), just a
        /// different trigger event and screen position (bottom-right vs. ActingUnitCard's
        /// bottom-left) so both can be visible at once.
        /// </summary>
        private static HoverStatsPanel BuildHoverStatsPanel(Transform parent)
        {
            var rootGO = new GameObject("HoverStatsPanel", typeof(RectTransform));
            rootGO.transform.SetParent(parent, false);
            var rootRt = rootGO.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(1f, 0f);
            rootRt.anchorMax = new Vector2(1f, 0f);
            rootRt.pivot = new Vector2(1f, 0f);
            rootRt.anchoredPosition = new Vector2(-20f, 20f);
            rootRt.sizeDelta = new Vector2(260f, 150f);
            rootGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            var panel = rootGO.AddComponent<HoverStatsPanel>();

            BindSerialized(panel, BuildStatCardElements(rootGO));

            rootGO.SetActive(false);
            return panel;
        }

        /// <summary>
        /// Builds the Name/Role/HP/ATK/DEF/Fear element set shared by ActingUnitCard and
        /// HoverStatsPanel (identical [SerializeField] names on both) so the two callers don't
        /// duplicate this layout — only their root panel's position/size and the component
        /// type added to it differ.
        /// </summary>
        private static Dictionary<string, Object> BuildStatCardElements(GameObject rootGO)
        {
            var nameLabel = CreateText(rootGO.transform, "NameLabel", "Name", 16, TextAnchor.UpperLeft);
            SetStretch(nameLabel.GetComponent<RectTransform>(), new Vector2(10f, -30f), new Vector2(-10f, -10f), new Vector2(0f, 1f), new Vector2(1f, 1f));

            var roleLabel = CreateText(rootGO.transform, "RoleLabel", "role", 12, TextAnchor.UpperLeft);
            SetStretch(roleLabel.GetComponent<RectTransform>(), new Vector2(10f, -48f), new Vector2(-10f, -32f), new Vector2(0f, 1f), new Vector2(1f, 1f));

            var hpValueLabel = CreateText(rootGO.transform, "HpValueLabel", "0/0", 12, TextAnchor.MiddleRight);
            SetStretch(hpValueLabel.GetComponent<RectTransform>(), new Vector2(-70f, -68f), new Vector2(-10f, -52f), new Vector2(1f, 1f), new Vector2(1f, 1f));

            var hpSlider = CreateSlider(rootGO.transform, "HpSlider", new Color(0.7f, 0.15f, 0.15f));
            SetStretch(hpSlider.GetComponent<RectTransform>(), new Vector2(10f, -80f), new Vector2(-10f, -68f), new Vector2(0f, 1f), new Vector2(1f, 1f));

            var atkValueLabel = CreateText(rootGO.transform, "AtkValueLabel", "0", 12, TextAnchor.MiddleLeft);
            SetStretch(atkValueLabel.GetComponent<RectTransform>(), new Vector2(10f, -100f), new Vector2(70f, -84f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            var defValueLabel = CreateText(rootGO.transform, "DefValueLabel", "0", 12, TextAnchor.MiddleLeft);
            SetStretch(defValueLabel.GetComponent<RectTransform>(), new Vector2(80f, -100f), new Vector2(140f, -84f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            var fearValueLabel = CreateText(rootGO.transform, "FearValueLabel", "fear 0/10", 12, TextAnchor.MiddleLeft);
            SetStretch(fearValueLabel.GetComponent<RectTransform>(), new Vector2(10f, -120f), new Vector2(120f, -104f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            var fearDotMeterGO = new GameObject("FearDotMeter", typeof(RectTransform));
            fearDotMeterGO.transform.SetParent(rootGO.transform, false);
            var fearDotMeterRt = fearDotMeterGO.GetComponent<RectTransform>();
            SetStretch(fearDotMeterRt, new Vector2(10f, -136f), new Vector2(-10f, -122f), new Vector2(0f, 1f), new Vector2(1f, 1f));
            var fearLayout = fearDotMeterGO.AddComponent<HorizontalLayoutGroup>();
            fearLayout.spacing = 4f;
            fearLayout.childControlWidth = true;
            fearLayout.childControlHeight = true;
            fearLayout.childForceExpandWidth = false;
            fearLayout.childForceExpandHeight = false;
            var fearDotMeter = fearDotMeterGO.AddComponent<FearDotMeter>();
            var dots = new Image[10];
            for (int i = 0; i < dots.Length; i++)
            {
                var dotGO = new GameObject($"Dot{i + 1}", typeof(RectTransform));
                dotGO.transform.SetParent(fearDotMeterGO.transform, false);
                // Explicit sizeDelta too, not just LayoutElement — belt-and-braces in case a
                // future edit disables childControlWidth/Height again (this is exactly the bug
                // that shipped once already: preferredWidth alone did nothing without those
                // flags, and every dot rendered at Unity's default 100x100 Image size).
                dotGO.GetComponent<RectTransform>().sizeDelta = new Vector2(14f, 14f);
                var dotLayout = dotGO.AddComponent<LayoutElement>();
                dotLayout.preferredWidth = 14f;
                dotLayout.preferredHeight = 14f;
                dots[i] = dotGO.AddComponent<Image>();
            }

            BindDotsArray(fearDotMeter, dots);

            return new Dictionary<string, Object>
            {
                { "root", rootGO },
                { "nameLabel", nameLabel },
                { "roleLabel", roleLabel },
                { "hpValueLabel", hpValueLabel },
                { "hpSlider", hpSlider },
                { "atkValueLabel", atkValueLabel },
                { "defValueLabel", defValueLabel },
                { "fearValueLabel", fearValueLabel },
                { "fearDotMeter", fearDotMeter }
            };
        }

        /// <summary>
        /// A bar above the command menu showing "ATK + Roll - DEF = damage" for the most
        /// recent hit, with the roll digit cycling before settling (see DamageRollBar).
        /// </summary>
        private static DamageRollBar BuildDamageRollBar(Transform parent)
        {
            var rootGO = new GameObject("DamageRollBar", typeof(RectTransform));
            rootGO.transform.SetParent(parent, false);
            var rootRt = rootGO.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(0f, 190f);
            rootRt.sizeDelta = new Vector2(480f, 40f);
            rootGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var bar = rootGO.AddComponent<DamageRollBar>();

            var formulaLabel = CreateText(rootGO.transform, "FormulaLabel", "", 14, TextAnchor.MiddleCenter);
            SetStretch(formulaLabel.GetComponent<RectTransform>(), new Vector2(10f, 4f), new Vector2(-60f, -4f), Vector2.zero, Vector2.one);

            var rollDigitLabel = CreateText(rootGO.transform, "RollDigitLabel", "", 20, TextAnchor.MiddleCenter);
            SetStretch(rollDigitLabel.GetComponent<RectTransform>(), new Vector2(-50f, 4f), new Vector2(-10f, -4f), Vector2.zero, Vector2.one);

            BindSerialized(bar, new Dictionary<string, Object>
            {
                { "root", rootGO },
                { "formulaLabel", formulaLabel },
                { "rollDigitLabel", rollDigitLabel }
            });

            rootGO.SetActive(false);
            return bar;
        }

        private static TurnBanner BuildTurnBanner(Transform parent)
        {
            var rootGO = new GameObject("TurnBanner", typeof(RectTransform));
            rootGO.transform.SetParent(parent, false);
            var rootRt = rootGO.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 1f);
            rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);
            rootRt.anchoredPosition = new Vector2(0f, -60f);
            rootRt.sizeDelta = new Vector2(360f, 50f);
            var banner = rootGO.AddComponent<TurnBanner>();

            var nameLabel = CreateText(rootGO.transform, "NameLabel", "", 18, TextAnchor.MiddleCenter);
            SetStretch(nameLabel.GetComponent<RectTransform>(), new Vector2(0f, -25f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f));

            var hintLabel = CreateText(rootGO.transform, "HintLabel", "", 12, TextAnchor.MiddleCenter);
            SetStretch(hintLabel.GetComponent<RectTransform>(), new Vector2(0f, -50f), new Vector2(0f, -25f), new Vector2(0f, 1f), new Vector2(1f, 1f));

            BindSerialized(banner, new Dictionary<string, Object>
            {
                { "root", rootGO },
                { "nameLabel", nameLabel },
                { "hintLabel", hintLabel }
            });

            rootGO.SetActive(false);
            return banner;
        }

        private static void SetStretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax, Vector2 anchorMin, Vector2 anchorMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static TurnOrderStrip BuildTurnOrderStrip(Transform parent)
        {
            var rootGO = new GameObject("TurnOrderStrip", typeof(RectTransform));
            rootGO.transform.SetParent(parent, false);
            var rootRt = rootGO.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 1f);
            rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);
            rootRt.anchoredPosition = new Vector2(0f, -20f);
            var strip = rootGO.AddComponent<TurnOrderStrip>();
            var horizontal = rootGO.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 6f;
            rootGO.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var iconGO = new GameObject("IconTemplate", typeof(RectTransform));
            iconGO.transform.SetParent(rootGO.transform, false);
            var iconRt = iconGO.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(48f, 48f);
            iconGO.AddComponent<LayoutElement>().preferredWidth = 48f;
            var iconBackground = iconGO.AddComponent<Image>();
            iconBackground.color = Color.gray;
            var icon = iconGO.AddComponent<TurnOrderIcon>();

            var iconLabel = CreateText(iconGO.transform, "Label", string.Empty, 10, TextAnchor.LowerCenter);
            var iconLabelRt = iconLabel.GetComponent<RectTransform>();
            iconLabelRt.anchorMin = Vector2.zero;
            iconLabelRt.anchorMax = Vector2.one;
            iconLabelRt.offsetMin = Vector2.zero;
            iconLabelRt.offsetMax = Vector2.zero;

            BindSerialized(icon, new Dictionary<string, Object>
            {
                { "label", iconLabel },
                { "background", iconBackground }
            });

            BindSerialized(strip, new Dictionary<string, Object>
            {
                { "iconParent", rootRt },
                { "iconTemplate", icon }
            });

            iconGO.SetActive(false);
            return strip;
        }

        private static EncounterResultPanel BuildResultPanel(Transform parent)
        {
            var rootGO = new GameObject("EncounterResultPanel", typeof(RectTransform));
            rootGO.transform.SetParent(parent, false);
            var panel = rootGO.AddComponent<EncounterResultPanel>();

            var panelRootGO = new GameObject("PanelRoot", typeof(RectTransform));
            panelRootGO.transform.SetParent(rootGO.transform, false);
            var panelRootRt = panelRootGO.GetComponent<RectTransform>();
            panelRootRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRootRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRootRt.pivot = new Vector2(0.5f, 0.5f);
            panelRootRt.sizeDelta = new Vector2(360f, 220f);
            panelRootGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);
            var vertical = panelRootGO.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(16, 16, 16, 16);
            vertical.spacing = 12f;
            vertical.childAlignment = TextAnchor.MiddleCenter;

            var titleLabel = CreateText(panelRootGO.transform, "TitleLabel", "Victory!", 24, TextAnchor.MiddleCenter);
            var detailsLabel = CreateText(panelRootGO.transform, "DetailsLabel", string.Empty, 16, TextAnchor.MiddleCenter);
            var continueButton = CreateButton(panelRootGO.transform, "ContinueButton", "Continue", new Vector2(140f, 36f));

            BindSerialized(panel, new Dictionary<string, Object>
            {
                { "panelRoot", panelRootGO },
                { "titleLabel", titleLabel },
                { "detailsLabel", detailsLabel },
                { "continueButton", continueButton }
            });

            panelRootGO.SetActive(false);
            return panel;
        }

        /// <summary>
        /// A standalone screen-space nameplate prefab (name label over an HP slider) — one per
        /// side, so ally and enemy nameplates can be restyled independently by editing the
        /// prefab asset, without touching BattleHUD.prefab. Pivot is bottom-center so the
        /// plate sits just above the point it tracks.
        /// </summary>
        private static CombatantNameplate BuildNameplatePrefab(string prefabName, Color fillColor)
        {
            var rootGO = new GameObject(prefabName, typeof(RectTransform));
            var rt = rootGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 34f);
            rt.pivot = new Vector2(0.5f, 0f);
            rootGO.AddComponent<CanvasGroup>();

            var plate = rootGO.AddComponent<CombatantNameplate>();

            var nameLabel = CreateText(rootGO.transform, "NameLabel", "Name", 14, TextAnchor.LowerCenter);
            SetStretch(nameLabel.GetComponent<RectTransform>(), new Vector2(0f, -18f), Vector2.zero, new Vector2(0f, 1f), new Vector2(1f, 1f));

            var hpSlider = CreateSlider(rootGO.transform, "HpSlider", fillColor);
            SetStretch(hpSlider.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, 14f), Vector2.zero, new Vector2(1f, 0f));

            BindSerialized(plate, new Dictionary<string, Object>
            {
                { "hpSlider", hpSlider },
                { "nameLabel", nameLabel }
            });

            EnsureFolder(UiPrefabFolder);
            var path = $"{UiPrefabFolder}/{prefabName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGO, path);
            Object.DestroyImmediate(rootGO);

            Debug.Log($"[CombatModuleSetup] Created {prefabName} prefab at {path}.");
            return prefab.GetComponent<CombatantNameplate>();
        }

        private static void AnchorBottomCenter(RectTransform rt, Vector2 anchoredPosition)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPosition;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var label_ = CreateText(go.transform, "Label", label, 14, TextAnchor.MiddleCenter);
            var labelRt = label_.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, Color fillColor)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 16f);

            root.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

            var slider = root.AddComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            var fillAreaGO = new GameObject("FillArea", typeof(RectTransform));
            fillAreaGO.transform.SetParent(root.transform, false);
            var fillAreaRt = fillAreaGO.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = new Vector2(2f, 2f);
            fillAreaRt.offsetMax = new Vector2(-2f, -2f);

            var fillGO = new GameObject("Fill", typeof(RectTransform));
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRt = fillGO.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillGO.AddComponent<Image>().color = fillColor;

            slider.fillRect = fillRt;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
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
