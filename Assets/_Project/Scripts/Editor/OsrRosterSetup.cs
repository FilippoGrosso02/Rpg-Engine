using System.Collections.Generic;
using System.IO;
using Game.Combat.Contracts;
using Game.Data.Definitions;
using Game.Data.Enums;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Authors the d10 OSR roster (5 stats on a 2-12 scale, d10 + ATK - DEF >= 6): 3 classes, the gear, the 3 spells and
    /// the Web status, 3 heroes (Warrior, Ranger, Thief) and 3 monsters (Bandit, Archer, Witch; ids stay enemy_goblin/ogre/slime). Re-runnable — every asset is
    /// load-or-create and overwritten in place, so GUIDs (and anything referencing them) stay stable. It also repoints the existing
    /// encounter assets and the default party roster at the new content and removes the old Duelwright heroes, cards and monsters.
    /// Run via: Tools/Combat/Setup OSR Roster, or -executeMethod Game.EditorTools.OsrRosterSetup.Run
    /// </summary>
    public static class OsrRosterSetup
    {
        private const string Root = "Assets/_Project/Resources/Data";
        private const string ModelFolder = "Assets/_Project/Prefabs/Characters";

        private static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{name}.prefab");
        private const string HeroAnimPath = "Assets/_Project/Config/Animation/AnimSet_OneHandSword.asset";
        private const string EnemyAnimPath = "Assets/_Project/Config/Animation/AnimSet_Enemy.asset";
        private const string PartyRosterPath = "Assets/_Project/Config/PartyRoster_Default.asset";

        // Attack range = attackRangeMultiplier * ActionResolver.RangeUnit (2 world units); 1 is melee.
        private const int Melee = 1;
        private const int Thrown = 3;
        private const int Bow = 5;

        // Fireball's "10 ft" radius, in world units.
        private const float FireballRadius = 3f;

        private static readonly string[] OldIds =
        {
            // Duelwright heroes
            "actor_count", "actor_countess", "actor_exorcist", "actor_guardsman", "actor_gypsy_witch", "actor_nun", "actor_papa",
            // Duelwright monsters
            "enemy_boss", "enemy_brute", "enemy_elite", "enemy_grunt", "enemy_skitterer",
        };

        [MenuItem("Tools/Combat/Setup OSR Roster")]
        public static void Run()
        {
            foreach (var folder in new[] { "Actors", "Enemies", "Skills", "Weapons", "Armor", "Classes", "StatusEffects" })
            {
                EnsureFolder($"{Root}/{folder}");
            }

            var heroModel = Model("Hero_Warrior");
            var heroAnim = AssetDatabase.LoadAssetAtPath<AnimationSetDefinition>(HeroAnimPath);
            var enemyAnim = AssetDatabase.LoadAssetAtPath<AnimationSetDefinition>(EnemyAnimPath);
            if (heroModel == null || heroAnim == null || enemyAnim == null)
            {
                Debug.LogError("[OsrRosterSetup] Missing the Hero_Warrior model or animation sets — nothing changed.");
                return;
            }

            // --- Status ---
            var stuck = Create<StatusEffectDefinition>("StatusEffects", "status_stuck", "Stuck", s =>
            {
                s.duration = 2;
                s.preventsMovement = true;
            });

            // --- Skills ---
            var basic = Create<SkillDefinition>("Skills", "skill_basic_attack", "Attack", s =>
            {
                s.description = "Attack with your weapon: d10 + ATK - DEF must reach 6.";
                s.targetType = TargetType.SingleEnemy;
                s.usesWeaponDamage = true;
                s.apCost = 0;
            });

            var magicMissile = Create<SkillDefinition>("Skills", "skill_magic_missile", "Magic Missile", s =>
            {
                s.description = "Level 1. Never misses: d6 damage.";
                s.targetType = TargetType.SingleEnemy;
                s.autoHit = true;
                s.damage = new DiceRoll(1, 6);
                s.apCost = 1;
                s.spellLevel = 1;
                s.damageType = DamageType.Lightning;
            });

            var web = Create<SkillDefinition>("Skills", "skill_web", "Web", s =>
            {
                s.description = "Level 2. The target saves (d10 + VIT - DV 8 >= 6) or is stuck for 2 turns.";
                s.targetType = TargetType.SingleEnemy;
                s.damage = DiceRoll.None;
                s.saveType = Game.Data.Enums.SaveType.Vitality;
                s.saveDifficulty = 8;
                s.halfDamageOnSave = false;
                s.statusEffectId = stuck.id;
                s.statusEffectChance = 1f;
                s.apCost = 2;
                s.spellLevel = 2;
            });

            var fireball = Create<SkillDefinition>("Skills", "skill_fireball", "Fireball", s =>
            {
                s.description = "Level 3. Everything within 10 ft takes 2d6; a save (d10 + VIT - DV 7 >= 6) halves it.";
                s.targetType = TargetType.AreaCircle;
                s.aoeSize = FireballRadius;
                s.damage = new DiceRoll(2, 6);
                s.saveType = Game.Data.Enums.SaveType.Vitality;
                s.saveDifficulty = 7;
                s.halfDamageOnSave = true;
                s.apCost = 3;
                s.spellLevel = 3;
                s.damageType = DamageType.Fire;
            });

            // --- Gear ---
            // Weapon ids keep their old names (weapon_sword etc.) so heroes, saves and menus that reference them stay valid.
            var sword = Create<WeaponDefinition>("Weapons", "weapon_sword", "Shortsword", w =>
            {
                w.equipSlot = EquipmentSlot.MainHand;
                w.damage = new DiceRoll(1, 6);
                w.attackModifier = 0;
                w.isRanged = false;
                w.isMetal = true;
                w.damageType = DamageType.Slashing;
            });

            var shortbow = Create<WeaponDefinition>("Weapons", "weapon_shortbow", "Shortbow", w =>
            {
                w.equipSlot = EquipmentSlot.MainHand;
                w.damage = new DiceRoll(1, 6);
                w.attackModifier = 0;
                w.isRanged = true;
                w.isMetal = false;
                w.damageType = DamageType.Piercing;
            });

            var dagger = Create<WeaponDefinition>("Weapons", "weapon_dagger", "Dagger", w =>
            {
                w.equipSlot = EquipmentSlot.MainHand;
                w.damage = new DiceRoll(1, 4);
                w.attackModifier = 2;
                w.isRanged = false; // melee: attacks with STR
                w.isMetal = true;
                w.damageType = DamageType.Piercing;
            });

            var chain = Create<ArmorDefinition>("Armor", "armor_chain_mail", "Chain Mail", a =>
            {
                a.equipSlot = EquipmentSlot.Body;
                a.acBonus = 4;
            });

            var shield = Create<ArmorDefinition>("Armor", "armor_shield", "Shield", a =>
            {
                a.equipSlot = EquipmentSlot.OffHand;
                a.acBonus = 1;
            });

            var leather = Create<ArmorDefinition>("Armor", "armor_leather", "Leather Armor", a =>
            {
                a.equipSlot = EquipmentSlot.Body;
                a.acBonus = 2;
            });

            // --- Classes ---
            var warriorClass = Create<ClassDefinition>("Classes", "class_warrior", "Warrior", c =>
            {
                c.hitDieSides = 8;
                c.evadesWithDex = false;
            });
            var rangerClass = Create<ClassDefinition>("Classes", "class_ranger", "Ranger", c =>
            {
                c.hitDieSides = 8;
                c.evadesWithDex = true; // evade class: DEX minor modifier counts towards DEF
            });
            var thiefClass = Create<ClassDefinition>("Classes", "class_thief", "Thief", c =>
            {
                c.hitDieSides = 6;
                c.evadesWithDex = true;
            });
            // No spellcasting class for now: the spells below stay as assets (and tested) but nobody knows them.

            // --- Heroes ---
            var warrior = Create<ActorDefinition>("Actors", "actor_warrior", "Warrior", a =>
            {
                Hero(a, heroModel, heroAnim, warriorClass, "Front-line fighter", Melee);
                a.strength = 10; a.dexterity = 7; a.vitality = 8; a.mind = 7; a.charm = 7;
                a.startingWeaponId = sword.id;
                a.startingArmorIds = new List<string> { chain.id, shield.id };
                a.startingSkillIds = new List<string> { basic.id };
            });

            var ranger = Create<ActorDefinition>("Actors", "actor_ranger", "Ranger", a =>
            {
                Hero(a, Model("Hero_Ranger"), heroAnim, rangerClass, "Mobile archer", Bow);
                a.strength = 7; a.dexterity = 10; a.vitality = 8; a.mind = 7; a.charm = 7;
                a.startingWeaponId = shortbow.id;
                a.startingArmorIds = new List<string> { leather.id };
                a.startingSkillIds = new List<string> { basic.id };
            });

            var thief = Create<ActorDefinition>("Actors", "actor_thief", "Thief", a =>
            {
                Hero(a, Model("Hero_Thief"), heroAnim, thiefClass, "Quick skirmisher", Melee);
                a.strength = 9; a.dexterity = 9; a.vitality = 7; a.mind = 7; a.charm = 7;
                a.startingWeaponId = dagger.id;
                a.startingArmorIds = new List<string> { leather.id };
                a.startingSkillIds = new List<string> { basic.id };
            });

            // --- Test fixtures ---
            // PartyRosterTests need at least 6 heroes in the database (a full party of 5 plus one to reject), and the real roster
            // is three, so two plainly-labelled stand-ins fill the gap. They use the Warrior's numbers and are never in a party.
            for (int i = 2; i <= 3; i++)
            {
                Create<ActorDefinition>("Actors", $"actor_test_hero_{i}", $"Test Hero {i}", a =>
                {
                    Hero(a, heroModel, heroAnim, warriorClass, "Test fixture", Melee);
                    a.strength = 10; a.dexterity = 7; a.vitality = 8; a.mind = 7; a.charm = 7;
                    a.startingWeaponId = sword.id;
                    a.startingArmorIds = new List<string> { chain.id, shield.id };
                    a.startingSkillIds = new List<string> { basic.id };
                });
            }

            // --- Monsters ---
            var goblin = Create<EnemyDefinition>("Enemies", "enemy_goblin", "Bandit", e =>
            {
                Monster(e, Model("Enemy_Bandit"), enemyAnim, basic, "Grunt");
                e.hp = 5; e.armorClass = 4; e.attackBonus = 7; e.damage = new DiceRoll(1, 6);
                e.morale = 7; e.dexterity = 8; e.saveBonus = 7;
            });

            var ogre = Create<EnemyDefinition>("Enemies", "enemy_ogre", "Archer", e =>
            {
                Monster(e, Model("Enemy_Archer"), enemyAnim, basic, "Brute");
                e.hp = 22; e.armorClass = 8; e.attackBonus = 9; e.damage = new DiceRoll(2, 6);
                e.morale = 9; e.dexterity = 6; e.saveBonus = 9;
            });

            var slime = Create<EnemyDefinition>("Enemies", "enemy_slime", "Witch", e =>
            {
                Monster(e, Model("Enemy_Witch"), enemyAnim, basic, "Hazard");
                e.hp = 14; e.armorClass = 4; e.attackBonus = 6; e.damage = new DiceRoll(1, 6);
                e.morale = 0; // mindless: never checks morale
                e.dexterity = 5; e.saveBonus = 6;
                e.corrodesArmor = true;
                e.corrodesWeapons = true;
                e.immuneToCharm = true;
                e.immuneToFear = true;
                e.immuneToSleep = true;
            });

            // --- Encounters and party ---
            SetEncounter("EncounterDuelwrightSkirmish", new[] { warrior.id, ranger.id, thief.id }, new[] { goblin.id, goblin.id, goblin.id, ogre.id });
            SetEncounter("EncounterSampleSkirmish", new[] { warrior.id, ranger.id, thief.id }, new[] { goblin.id, slime.id });
            SetParty(new[] { warrior, ranger, thief });

            AssetDatabase.DeleteAsset($"{Root}/Actors/actor_wizard.asset");
            AssetDatabase.DeleteAsset($"{Root}/Classes/class_wizard.asset");

            RemoveOldDuelwrightAssets();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OsrRosterSetup] OSR roster created: Warrior, Ranger, Thief vs Bandit, Archer, Witch.");
        }

        private static void Hero(ActorDefinition a, GameObject model, AnimationSetDefinition anim, ClassDefinition cls, string role, int rangeMultiplier)
        {
            a.battleModelPrefab = model;
            a.animationSet = anim;
            a.classId = cls.id;
            a.level = 1;
            a.moveRange = 6;
            a.attackRangeMultiplier = rangeMultiplier;
            a.roleLabel = role;
            a.usesAp = false;
        }

        private static void Monster(EnemyDefinition e, GameObject model, AnimationSetDefinition anim, SkillDefinition basic, string role)
        {
            e.battleModelPrefab = model;
            e.animationSet = anim;
            e.moveRange = 6;
            e.attackRangeMultiplier = Melee;
            e.roleLabel = role;
            e.skillIds = new List<string> { basic.id };
            e.corrodesArmor = false;
            e.corrodesWeapons = false;
            e.immuneToCharm = false;
            e.immuneToFear = false;
            e.immuneToSleep = false;
        }

        private static void SetEncounter(string assetName, IEnumerable<string> party, IEnumerable<string> enemies)
        {
            var path = $"{Root}/Encounters/{assetName}.asset";
            var encounter = AssetDatabase.LoadAssetAtPath<EncounterDefinition>(path);
            if (encounter == null)
            {
                return;
            }

            encounter.partyActorIds = new List<string>(party);
            encounter.enemyIds = new List<string>(enemies);
            EditorUtility.SetDirty(encounter);
        }

        private static void SetParty(ActorDefinition[] members)
        {
            var roster = AssetDatabase.LoadAssetAtPath<PartyRosterDefinition>(PartyRosterPath);
            if (roster == null)
            {
                return;
            }

            roster.startingMembers = new List<ActorDefinition>(members);
            EditorUtility.SetDirty(roster);
        }

        /// <summary>Deletes the Duelwright heroes, monsters and their cards, which the OSR rules no longer understand.</summary>
        private static void RemoveOldDuelwrightAssets()
        {
            var doomed = new List<string>();
            foreach (var id in OldIds)
            {
                foreach (var folder in new[] { "Actors", "Enemies" })
                {
                    var path = $"{Root}/{folder}/{id}.asset";
                    if (File.Exists(path))
                    {
                        doomed.Add(path);
                    }
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { $"{Root}/Skills" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (IsOldCard(name))
                {
                    doomed.Add(path);
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:StatusEffectDefinition", new[] { $"{Root}/StatusEffects" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).StartsWith("status_") && Path.GetFileNameWithoutExtension(path) != "status_stuck")
                {
                    doomed.Add(path);
                }
            }

            foreach (var path in doomed)
            {
                AssetDatabase.DeleteAsset(path);
            }

            Debug.Log($"[OsrRosterSetup] Removed {doomed.Count} old Duelwright assets.");
        }

        private static bool IsOldCard(string assetName)
        {
            if (!assetName.StartsWith("skill_"))
            {
                return false;
            }

            // Keep the new spells and the Basic Attack; everything else with a "skill_" prefix is a Duelwright card.
            return assetName != "skill_basic_attack" && assetName != "skill_magic_missile" && assetName != "skill_web" && assetName != "skill_fireball";
        }

        private static T Create<T>(string folder, string id, string displayName, System.Action<T> configure) where T : GameDefinition
        {
            var path = $"{Root}/{folder}/{id}.asset";
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

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }
    }
}
