using System.Collections.Generic;
using Game.Combat.Contracts;
using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Authors the Duelwright roster: 7 heroes and their cards, 5 monster archetypes, the
    /// status effects their cards apply, and one sample EncounterDefinition using them. Follows
    /// CombatModuleSetup's load-or-create pattern — safe to re-run, always overwrites its own
    /// output. Reuses CombatModuleSetup's placeholder prefabs (Hero/EnemyPlaceholder) rather
    /// than authoring new 3D models. Bespoke card ids here must match Combat.TrailsBattle3D's
    /// CardEffects constants.
    /// Invoke via: -batchmode -quit -executeMethod Game.EditorTools.DuelwrightRosterSetup.Run
    /// </summary>
    public static class DuelwrightRosterSetup
    {
        private const string ActorFolder = "Assets/_Project/Resources/Data/Actors";
        private const string EnemyFolder = "Assets/_Project/Resources/Data/Enemies";
        private const string SkillFolder = "Assets/_Project/Resources/Data/Skills";
        private const string StatusFolder = "Assets/_Project/Resources/Data/StatusEffects";
        private const string EncounterFolder = "Assets/_Project/Resources/Data/Encounters";

        private const string HeroPrefabPath = "Assets/_Project/Prefabs/Combat/TrailsBattle3D/HeroPlaceholder.prefab";
        private const string EnemyPrefabPath = "Assets/_Project/Prefabs/Combat/TrailsBattle3D/EnemyPlaceholder.prefab";

        // Attack range = attackRangeMultiplier * ActionResolver.RangeUnit (2 world units).
        // 1 = melee baseline (range 2). Ranged multipliers are a per-character judgment call:
        // Guardsman's shotgun is deliberately short (range 4); everyone else's ranged kit is
        // long (range 8) so it comfortably covers the arena. Skitterer sits in between (range 6).
        private const int MeleeRangeMultiplier = 1;
        private const int ShortRangedMultiplier = 2;
        private const int MediumRangedMultiplier = 3;
        private const int LongRangedMultiplier = 4;

        private const int DefaultMoveRange = 6;

        public static void Run()
        {
            Debug.Log("[DuelwrightRosterSetup] Starting...");

            EnsureFolder(SkillFolder);
            EnsureFolder(StatusFolder);
            EnsureFolder(ActorFolder);
            EnsureFolder(EnemyFolder);
            EnsureFolder(EncounterFolder);

            var heroModel = AssetDatabase.LoadAssetAtPath<GameObject>(HeroPrefabPath);
            var enemyModel = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (heroModel == null || enemyModel == null)
            {
                Debug.LogError("[DuelwrightRosterSetup] Placeholder prefabs not found — run CombatModuleSetup.Run first.");
                return;
            }

            var basicAttack = CreateSkill("skill_basic_attack", "Basic Attack", s =>
            {
                s.targetType = TargetType.SingleEnemy;
            });

            CreateStatusEffects();
            CreateHeroes(heroModel, basicAttack);
            CreateMonsters(enemyModel, basicAttack);
            var encounter = CreateSampleEncounter();
            PointDebugTriggerAtEncounter(encounter);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[DuelwrightRosterSetup] Done.");
        }

        // --- Status effects used by cards ---

        private static void CreateStatusEffects()
        {
            CreateStatus("status_rapier_flourish_def_down", "Off-Balance", s => { s.duration = 2; s.defModifier = -2; });
            CreateStatus("status_suppressing_fire_atk_down", "Suppressed", s => { s.duration = 2; s.atkModifier = -2; });
            CreateStatus("status_hex_of_weakness_atk_down", "Weakened", s => { s.duration = 2; s.atkModifier = -3; });
            CreateStatus("status_hexbolt_poison", "Poisoned", s => { s.duration = 2; s.tickDamage = 3; });
        }

        // --- Heroes ---

        private static void CreateHeroes(GameObject model, SkillDefinition basicAttack)
        {
            // The Nun — Thief w/ gun: fast, fragile, hits hard. Ranged.
            var quickDraw = CreateSkill("skill_nun_quick_draw", "Quick Draw", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 2;
                s.critThreshold = 5;
            });
            var stickyFingers = CreateSkill("skill_nun_sticky_fingers", "Sticky Fingers", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 3;
                s.skipDamage = true;
            });
            var smokeBomb = CreateSkill("skill_nun_smoke_bomb", "Smoke Bomb", s =>
            {
                s.targetType = TargetType.Self;
                s.apCost = 4;
                s.skipDamage = true;
            });
            CreateActor("actor_nun", "The Nun", "Thief w/ Gun", LongRangedMultiplier, atk: 7, def: 5, speed: 4, hp: 14, model,
                basicAttack, quickDraw, stickyFingers, smokeBomb);

            // Count — Fighter, aristocratic duelist. Melee.
            var rapierFlourish = CreateSkill("skill_count_rapier_flourish", "Rapier Flourish", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 2;
                s.statusEffectId = "status_rapier_flourish_def_down";
                s.statusEffectChance = 1f;
            });
            var nobleChallenge = CreateSkill("skill_count_noble_challenge", "Noble Challenge", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 3;
                s.skipDamage = true;
            });
            var bloodForPower = CreateSkill("skill_count_blood_for_power", "Blood for Power", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 4;
                s.bonusPower = 4;
                s.ignoresDef = true;
                s.skipDiceRoll = true;
                s.selfDamage = 3;
            });
            CreateActor("actor_count", "Count", "Fighter", MeleeRangeMultiplier, atk: 7, def: 7, speed: 2, hp: 20, model,
                basicAttack, rapierFlourish, nobleChallenge, bloodForPower);

            // Countess — Healer/support. Melee (per roster table).
            var mend = CreateSkill("skill_countess_mend", "Mend", s =>
            {
                s.targetType = TargetType.SingleAlly;
                s.apCost = 3;
                s.isHeal = true;
                s.healBonus = 2;
            });
            var calmingPresence = CreateSkill("skill_countess_calming_presence", "Calming Presence", s =>
            {
                s.targetType = TargetType.SingleAlly;
                s.apCost = 2;
                s.skipDamage = true;
                s.fearDelta = -3;
            });
            var lifeBond = CreateSkill("skill_countess_life_bond", "Life Bond", s =>
            {
                s.targetType = TargetType.SingleAlly;
                s.apCost = 5;
                s.skipDamage = true;
            });
            CreateActor("actor_countess", "Countess", "Healer/Support", MeleeRangeMultiplier, atk: 4, def: 4, speed: 4, hp: 12, model,
                basicAttack, mend, calmingPresence, lifeBond);

            // Guardsman — Shotgun, tanky protector. Ranged.
            var suppressingFire = CreateSkill("skill_guardsman_suppressing_fire", "Suppressing Fire", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 3;
                s.statusEffectId = "status_suppressing_fire_atk_down";
                s.statusEffectChance = 1f;
            });
            var bodyguard = CreateSkill("skill_guardsman_bodyguard", "Bodyguard", s =>
            {
                s.targetType = TargetType.SingleAlly;
                s.apCost = 2;
                s.skipDamage = true;
            });
            var pointBlankBlast = CreateSkill("skill_guardsman_point_blank_blast", "Point-Blank Blast", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 5;
                s.extraDiceCount = 1;
            });
            CreateActor("actor_guardsman", "Guardsman", "Shotgun, Tanky Protector", ShortRangedMultiplier, atk: 5, def: 5, speed: 1, hp: 20, model,
                basicAttack, suppressingFire, bodyguard, pointBlankBlast);

            // Papa (dog) — fast, scrappy, hard-hitting, no cards by design. Melee.
            CreateActor("actor_papa", "Papa", "Fast, Scrappy Brawler", MeleeRangeMultiplier, atk: 8, def: 5, speed: 5, hp: 10, model,
                basicAttack);

            // The Exorcist — relic/holy-water caster, cards-focused. Ranged.
            var holyWaterSplash = CreateSkill("skill_exorcist_holy_water_splash", "Holy Water Splash", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 3;
                s.fearDelta = 2;
            });
            var cleansingRite = CreateSkill("skill_exorcist_cleansing_rite", "Cleansing Rite", s =>
            {
                s.targetType = TargetType.SingleAlly;
                s.apCost = 3;
                s.skipDamage = true;
                s.fearDelta = -2;
            });
            var banish = CreateSkill("skill_exorcist_banish", "Banish", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 5;
            });
            CreateActor("actor_exorcist", "The Exorcist", "Relic/Holy-Water Caster", LongRangedMultiplier, atk: 5, def: 6, speed: 4, hp: 18, model,
                basicAttack, holyWaterSplash, cleansingRite, banish);

            // The Gypsy Witch — hex caster, spell-heavy. Ranged.
            var hexOfWeakness = CreateSkill("skill_witch_hex_of_weakness", "Hex of Weakness", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 3;
                s.skipDamage = true;
                s.statusEffectId = "status_hex_of_weakness_atk_down";
                s.statusEffectChance = 1f;
            });
            var curseOfDread = CreateSkill("skill_witch_curse_of_dread", "Curse of Dread", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 3;
                s.skipDamage = true;
                s.fearDelta = 3;
            });
            var hexbolt = CreateSkill("skill_witch_hexbolt", "Hexbolt", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 4;
                s.statusEffectId = "status_hexbolt_poison";
                s.statusEffectChance = 1f;
            });
            CreateActor("actor_gypsy_witch", "The Gypsy Witch", "Hex Caster", LongRangedMultiplier, atk: 4, def: 5, speed: 5, hp: 14, model,
                basicAttack, hexOfWeakness, curseOfDread, hexbolt);
        }

        // --- Monsters ---

        private static void CreateMonsters(GameObject model, SkillDefinition basicAttack)
        {
            CreateEnemy("enemy_grunt", "Grunt", "Trash", MeleeRangeMultiplier, atk: 3, def: 3, speed: 2, hp: 9, isHorror: false, model,
                basicAttack);
            CreateEnemy("enemy_brute", "Brute", "Melee Hard-Hitter", MeleeRangeMultiplier, atk: 7, def: 5, speed: 1, hp: 24, isHorror: false, model,
                basicAttack);
            CreateEnemy("enemy_skitterer", "Skitterer", "Ranged Pest", MediumRangedMultiplier, atk: 4, def: 2, speed: 6, hp: 8, isHorror: false, model,
                basicAttack);
            CreateEnemy("enemy_elite", "Elite", "Hero-Tier", MeleeRangeMultiplier, atk: 6, def: 6, speed: 3, hp: 22, isHorror: false, model,
                basicAttack);

            var crushingBlow = CreateSkill("skill_boss_crushing_blow", "Crushing Blow", s =>
            {
                s.targetType = TargetType.SingleEnemy;
                s.apCost = 4;
                s.bonusPower = 4;
            });
            var dreadAura = CreateSkill("skill_boss_dread_aura", "Dread Aura", s =>
            {
                s.targetType = TargetType.AllEnemies;
                s.apCost = 3;
                s.skipDamage = true;
                s.fearDelta = 3;
            });
            CreateEnemy("enemy_boss", "Boss", "Over-Budget Centerpiece", MeleeRangeMultiplier, atk: 9, def: 6, speed: 4, hp: 45, isHorror: true, model,
                basicAttack, crushingBlow, dreadAura);
        }

        // --- Sample encounter ---

        private static EncounterDefinition CreateSampleEncounter()
        {
            const string path = EncounterFolder + "/EncounterDuelwrightSkirmish.asset";
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
            if (encounter == null)
            {
                encounter = ScriptableObject.CreateInstance<EncounterDefinition>();
                AssetDatabase.CreateAsset(encounter, path);
            }

            encounter.partyActorIds = new List<string> { "actor_nun", "actor_count", "actor_countess" };
            encounter.enemyIds = new List<string> { "enemy_grunt", "enemy_grunt", "enemy_brute" };

            EditorUtility.SetDirty(encounter);
            Debug.Log($"[DuelwrightRosterSetup] Sample EncounterDefinition ready at {path}.");
            return encounter;
        }

        /// <summary>Keeps F5 (DebugEncounterTrigger, wired into SampleScene by
        /// CombatModuleSetup) pointed at the Duelwright roster instead of the legacy Test
        /// Hero/Slime fixture — re-running this script should never silently revert that.</summary>
        private static void PointDebugTriggerAtEncounter(EncounterDefinition encounter)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[DuelwrightRosterSetup] Skipping F5 rewiring — Editor is in Play mode. Re-run after exiting Play mode.");
                return;
            }

            const string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            var scene = EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);

            var trigger = Object.FindAnyObjectByType<DebugEncounterTrigger>();
            if (trigger == null)
            {
                Debug.LogWarning("[DuelwrightRosterSetup] No DebugEncounterTrigger found in SampleScene — run CombatModuleSetup first.");
                return;
            }

            trigger.sampleEncounter = encounter;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DuelwrightRosterSetup] F5 (DebugEncounterTrigger) now points at the Duelwright roster.");
        }

        // --- Asset helpers (load-or-create, matching CombatModuleSetup's pattern) ---

        private static SkillDefinition CreateSkill(string id, string displayName, System.Action<SkillDefinition> configure)
        {
            var path = $"{SkillFolder}/{id}.asset";
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<SkillDefinition>();
                AssetDatabase.CreateAsset(skill, path);
            }

            skill.id = id;
            skill.displayName = displayName;
            configure(skill);

            EditorUtility.SetDirty(skill);
            return skill;
        }

        private static void CreateStatus(string id, string displayName, System.Action<StatusEffectDefinition> configure)
        {
            var path = $"{StatusFolder}/{id}.asset";
            var status = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(path);
            if (status == null)
            {
                status = ScriptableObject.CreateInstance<StatusEffectDefinition>();
                AssetDatabase.CreateAsset(status, path);
            }

            status.id = id;
            status.displayName = displayName;
            configure(status);

            EditorUtility.SetDirty(status);
        }

        private static void CreateActor(
            string id,
            string displayName,
            string roleLabel,
            int attackRangeMultiplier,
            int atk,
            int def,
            int speed,
            int hp,
            GameObject model,
            SkillDefinition basicAttack,
            params SkillDefinition[] cards)
        {
            var path = $"{ActorFolder}/{id}.asset";
            var actor = AssetDatabase.LoadAssetAtPath<ActorDefinition>(path);
            if (actor == null)
            {
                actor = ScriptableObject.CreateInstance<ActorDefinition>();
                AssetDatabase.CreateAsset(actor, path);
            }

            actor.id = id;
            actor.displayName = displayName;
            actor.roleLabel = roleLabel;
            actor.battleModelPrefab = model;
            actor.baseAtk = atk;
            actor.baseDef = def;
            actor.baseSpeed = speed;
            actor.baseHp = hp;
            actor.baseMp = 0;
            actor.moveRange = DefaultMoveRange;
            actor.attackRangeMultiplier = attackRangeMultiplier;

            actor.startingSkillIds = new List<string> { basicAttack.id };
            foreach (var card in cards)
            {
                actor.startingSkillIds.Add(card.id);
            }

            EditorUtility.SetDirty(actor);
        }

        private static void CreateEnemy(
            string id,
            string displayName,
            string roleLabel,
            int attackRangeMultiplier,
            int atk,
            int def,
            int speed,
            int hp,
            bool isHorror,
            GameObject model,
            SkillDefinition basicAttack,
            params SkillDefinition[] cards)
        {
            var path = $"{EnemyFolder}/{id}.asset";
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (enemy == null)
            {
                enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(enemy, path);
            }

            enemy.id = id;
            enemy.displayName = displayName;
            enemy.roleLabel = roleLabel;
            enemy.battleModelPrefab = model;
            enemy.atk = atk;
            enemy.def = def;
            enemy.speed = speed;
            enemy.hp = hp;
            enemy.moveRange = DefaultMoveRange;
            enemy.attackRangeMultiplier = attackRangeMultiplier;
            enemy.isHorror = isHorror;

            enemy.skillIds = new List<string> { basicAttack.id };
            foreach (var card in cards)
            {
                enemy.skillIds.Add(card.id);
            }

            EditorUtility.SetDirty(enemy);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderName = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
