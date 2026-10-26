using System.Collections.Generic;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using Game.Data.Definitions;
using Game.Data.Enums;
using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests.EditMode
{
    /// <summary>
    /// The d10 resolver rolls real dice, so these tests inject a scripted System.Random: every call to Next() returns the next
    /// queued value. ResolveAttack draws in a fixed order — (1) the d10 attack roll, unless the spell auto-hits or is save-based,
    /// (2) each damage die, (3) the d10 save roll — so a test can say exactly which roll is which.
    /// </summary>
    public class ActionResolverTests
    {
        private class ScriptedRolls : System.Random
        {
            private readonly Queue<int> _rolls;

            public ScriptedRolls(params int[] rolls) => _rolls = new Queue<int>(rolls);

            public int Remaining => _rolls.Count;

            public override int Next(int minValue, int maxValue)
            {
                Assert.IsTrue(_rolls.Count > 0, "The resolver rolled more dice than the test scripted.");
                return _rolls.Dequeue();
            }
        }

        private static CombatantRuntime MakeCombatant(bool isPlayerSide, Vector3 position = default)
        {
            return new CombatantRuntime
            {
                isPlayerSide = isPlayerSide,
                maxHp = 20,
                currentHp = 20,
                position = position
            };
        }

        /// <summary>A Warrior: ATK 10 (STR 10, shortsword +0), shortsword d6.</summary>
        private static CombatantRuntime MakeWarrior()
        {
            var warrior = MakeCombatant(true);
            warrior.attackBonus = 10;
            warrior.attackDamage = new DiceRoll(1, 6);
            warrior.weaponIsMetal = true;
            return warrior;
        }

        private static CombatantRuntime MakeMonster(int armorClass, int saveBonus = 7)
        {
            var monster = MakeCombatant(false);
            monster.baseArmorClass = armorClass;
            monster.saveVitality = saveBonus;
            monster.saveMind = saveBonus;
            return monster;
        }

        private static SkillDefinition MakeBasicAttack()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.targetType = TargetType.SingleEnemy;
            skill.usesWeaponDamage = true;
            return skill;
        }

        private static SkillDefinition MakeSpell(string name, DiceRoll damage, bool autoHit = false, SaveType save = SaveType.None, bool half = false, int dv = 7)
        {
            var spell = ScriptableObject.CreateInstance<SkillDefinition>();
            spell.displayName = name;
            spell.targetType = TargetType.SingleEnemy;
            spell.damage = damage;
            spell.autoHit = autoHit;
            spell.saveType = save;
            spell.saveDifficulty = dv;
            spell.halfDamageOnSave = half;
            return spell;
        }

        // ---------------------------------------------------------------- weapon attacks

        [Test]
        public void ResolveAttack_RollPlusAtkMinusDefReachingSix_Hits_AndDealsTheWeaponDieOnly()
        {
            var skill = MakeBasicAttack();
            var rolls = new ScriptedRolls(5, 6); // d10 attack = 5 (+10 ATK - 9 DEF = 6, just enough), then d6 = 6

            var info = ActionResolver.ResolveAttack(MakeWarrior(), MakeMonster(armorClass: 9), skill, rolls);

            Assert.IsTrue(info.hit);
            Assert.AreEqual(5, info.attackRoll);
            Assert.AreEqual(9, info.targetArmorClass);
            Assert.AreEqual(6, info.damage); // d6 only, no ability bonus
            Assert.AreEqual("d6", info.damageText);
            Assert.AreEqual(0, rolls.Remaining);

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveAttack_RollPlusAtkMinusDefBelowSix_Misses_AndDealsNothing()
        {
            var skill = MakeBasicAttack();
            var attacker = MakeWarrior();
            attacker.attackBonus = 5;
            var rolls = new ScriptedRolls(8); // 8 + 5 - 9 = 4 < 6: a miss, so no damage die is rolled

            var info = ActionResolver.ResolveAttack(attacker, MakeMonster(armorClass: 9), skill, rolls);

            Assert.IsFalse(info.hit);
            Assert.AreEqual(0, info.damage);
            Assert.AreEqual(0, rolls.Remaining);

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveAttack_NaturalOneMissesEvenWhenTheGapIsHuge()
        {
            var skill = MakeBasicAttack();

            var info = ActionResolver.ResolveAttack(MakeWarrior(), MakeMonster(armorClass: 2), skill, new ScriptedRolls(1));

            Assert.IsFalse(info.hit);
            Assert.IsTrue(info.naturalOne);

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveAttack_NaturalTenHitsEvenWhenTheGapIsFarBelowFive()
        {
            var skill = MakeBasicAttack();
            var attacker = MakeWarrior();
            attacker.attackBonus = 2;

            var info = ActionResolver.ResolveAttack(attacker, MakeMonster(armorClass: 12), skill, new ScriptedRolls(10, 3));

            Assert.IsTrue(info.hit);
            Assert.IsTrue(info.naturalTen);
            Assert.AreEqual(3, info.damage); // d6

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveAttack_GuardAddsTwoToDefense()
        {
            var skill = MakeBasicAttack();
            var attacker = MakeWarrior();
            attacker.attackBonus = 5;
            var target = MakeMonster(armorClass: 7);

            // 8 + 5 - 7 = 6 hits ...
            Assert.IsTrue(ActionResolver.ResolveAttack(attacker, target, skill, new ScriptedRolls(8, 1)).hit);

            // ... but not once the target is guarding (DEF 9: 8 + 5 - 9 = 4).
            target.guarding = true;
            Assert.IsFalse(ActionResolver.ResolveAttack(attacker, target, skill, new ScriptedRolls(8)).hit);

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveAttack_CorrodedWeaponLosesDamage_ButAlwaysDoesAtLeastOne()
        {
            var skill = MakeBasicAttack();
            var attacker = MakeWarrior();
            attacker.attackDamage = new DiceRoll(1, 4, 0);
            attacker.weaponCorrosion = 2;

            var info = ActionResolver.ResolveAttack(attacker, MakeMonster(armorClass: 5), skill, new ScriptedRolls(10, 3));
            Assert.AreEqual(1, info.damage); // 3 - 2
            Assert.AreEqual(2, info.corrosionPenalty);

            attacker.weaponCorrosion = 9;
            info = ActionResolver.ResolveAttack(attacker, MakeMonster(armorClass: 5), skill, new ScriptedRolls(10, 1));
            Assert.AreEqual(1, info.damage, "A hit always does at least 1 damage.");

            Object.DestroyImmediate(skill);
        }

        // ---------------------------------------------------------------- spells

        [Test]
        public void ResolveAttack_AutoHitSpell_SkipsTheAttackRoll()
        {
            var magicMissile = MakeSpell("Magic Missile", new DiceRoll(1, 6), autoHit: true);
            var rolls = new ScriptedRolls(4); // only the d6

            var info = ActionResolver.ResolveAttack(MakeCombatant(true), MakeMonster(armorClass: 99), magicMissile, rolls);

            Assert.IsTrue(info.hit);
            Assert.IsTrue(info.autoHit);
            Assert.AreEqual(4, info.damage);
            Assert.AreEqual("Magic Missile", info.spellName);

            Object.DestroyImmediate(magicMissile);
        }

        [Test]
        public void ResolveAttack_NegatingSave_FailedSaveKeepsTheEffect_PassedSaveResistsIt()
        {
            var web = MakeSpell("Web", DiceRoll.None, save: SaveType.Vitality, dv: 8);
            var target = MakeMonster(armorClass: 9, saveBonus: 7);

            var failed = ActionResolver.ResolveAttack(MakeCombatant(true), target, web, new ScriptedRolls(6)); // 6 + 7 - DV 8 = 5 < 6
            Assert.IsTrue(failed.hasSave);
            Assert.IsFalse(failed.savePassed);
            Assert.AreEqual(8, failed.saveDifficulty);
            Assert.IsFalse(failed.effectNegated);

            var passed = ActionResolver.ResolveAttack(MakeCombatant(true), target, web, new ScriptedRolls(7)); // 7 + 7 - 8 = 6
            Assert.IsTrue(passed.savePassed);
            Assert.IsTrue(passed.effectNegated);

            Object.DestroyImmediate(web);
        }

        [Test]
        public void ResolveAttack_HalvingSave_FullDamageOnFail_HalfOnPass()
        {
            var fireball = MakeSpell("Fireball", new DiceRoll(2, 6), save: SaveType.Vitality, half: true);
            var target = MakeMonster(armorClass: 9, saveBonus: 8);

            var failed = ActionResolver.ResolveAttack(MakeCombatant(true), target, fireball, new ScriptedRolls(4, 5, 4)); // 2d6 = 9, save 4 + 8 - DV 7 = 5
            Assert.AreEqual(9, failed.damage);
            Assert.IsFalse(failed.halved);

            var saved = ActionResolver.ResolveAttack(MakeCombatant(true), target, fireball, new ScriptedRolls(4, 5, 5)); // save 5 + 8 - 7 = 6
            Assert.AreEqual(4, saved.damage); // 9 / 2, rounded down
            Assert.IsTrue(saved.halved);
            Assert.IsFalse(saved.effectNegated);

            Object.DestroyImmediate(fireball);
        }

        // ---------------------------------------------------------------- acid

        [Test]
        public void ApplyAcidEffects_AcidicHitCorrodesArmorByOne_UntilNoArmorIsLeft()
        {
            var slime = MakeMonster(armorClass: 7);
            slime.corrodesArmor = true;
            var hero = MakeCombatant(true);
            hero.baseArmorClass = 7;
            hero.armorBonus = 2;
            var hit = new DamageRollInfo { attacker = slime, target = hero, hit = true };

            ActionResolver.ApplyAcidEffects(hit);
            Assert.AreEqual(1, hero.armorCorrosion);
            Assert.AreEqual(6, hero.ArmorClass);

            ActionResolver.ApplyAcidEffects(hit);
            ActionResolver.ApplyAcidEffects(hit);
            Assert.AreEqual(2, hero.armorCorrosion, "Armor can only corrode as far as the armor worn.");
        }

        [Test]
        public void ApplyAcidEffects_UnarmoredTargetIsUnaffected_AndMissesDoNothing()
        {
            var slime = MakeMonster(armorClass: 7);
            slime.corrodesArmor = true;
            var wizard = MakeCombatant(true);
            wizard.baseArmorClass = 2;

            ActionResolver.ApplyAcidEffects(new DamageRollInfo { attacker = slime, target = wizard, hit = true });
            Assert.AreEqual(0, wizard.armorCorrosion);

            wizard.armorBonus = 4;
            ActionResolver.ApplyAcidEffects(new DamageRollInfo { attacker = slime, target = wizard, hit = false });
            Assert.AreEqual(0, wizard.armorCorrosion);
        }

        [Test]
        public void ApplyAcidEffects_MetalWeaponHittingASlimeLosesDamage_ButABowDoesNot()
        {
            var slime = MakeMonster(armorClass: 7);
            slime.corrodesWeapons = true;

            var warrior = MakeWarrior();
            ActionResolver.ApplyAcidEffects(new DamageRollInfo { attacker = warrior, target = slime, hit = true });
            ActionResolver.ApplyAcidEffects(new DamageRollInfo { attacker = warrior, target = slime, hit = true });
            Assert.AreEqual(2, warrior.weaponCorrosion, "Cumulative.");

            var ranger = MakeCombatant(true);
            ranger.weaponIsMetal = false;
            ActionResolver.ApplyAcidEffects(new DamageRollInfo { attacker = ranger, target = slime, hit = true });
            Assert.AreEqual(0, ranger.weaponCorrosion);
        }

        [Test]
        public void ApplyAcidEffects_SpellsNeverCorrode()
        {
            var slime = MakeMonster(armorClass: 7);
            slime.corrodesWeapons = true;
            var wizard = MakeCombatant(true);
            wizard.weaponIsMetal = true;

            ActionResolver.ApplyAcidEffects(new DamageRollInfo { attacker = wizard, target = slime, hit = true, spellName = "Magic Missile" });

            Assert.AreEqual(0, wizard.weaponCorrosion);
        }

        // ---------------------------------------------------------------- guard and spell points

        [Test]
        public void ApplyGuard_GivesTwoDefense_AndOnlyCastersBankTheExtraAp()
        {
            var fighter = MakeCombatant(true);
            fighter.baseArmorClass = 7;
            ActionResolver.ApplyGuard(fighter);
            Assert.IsTrue(fighter.guarding);
            Assert.IsFalse(fighter.pendingGuardApBonus);
            Assert.AreEqual(9, fighter.ArmorClass);

            var wizard = MakeCombatant(true);
            wizard.usesAp = true;
            ActionResolver.ApplyGuard(wizard);
            Assert.IsTrue(wizard.pendingGuardApBonus);
        }

        [Test]
        public void BeginTurn_EndsGuard()
        {
            var fighter = MakeCombatant(true);
            fighter.baseArmorClass = 7;
            ActionResolver.ApplyGuard(fighter);

            ActionResolver.BeginTurn(fighter);

            Assert.IsFalse(fighter.guarding);
            Assert.AreEqual(7, fighter.ArmorClass);
        }

        [Test]
        public void BeginTurn_CasterStartsWithOneAp_AndGainsOnePerTurnFromTheSecond()
        {
            var wizard = MakeCombatant(true);
            wizard.usesAp = true;
            wizard.currentAp = 1;

            ActionResolver.BeginTurn(wizard);
            Assert.AreEqual(1, wizard.currentAp, "First turn: the starting 1 AP.");

            ActionResolver.BeginTurn(wizard);
            Assert.AreEqual(2, wizard.currentAp);

            ActionResolver.BeginTurn(wizard);
            Assert.AreEqual(3, wizard.currentAp, "Unspent AP accumulates, so Fireball (3) is castable on the third turn.");
        }

        [Test]
        public void BeginTurn_GuardingLastTurnEarnsOneExtraAp()
        {
            var wizard = MakeCombatant(true);
            wizard.usesAp = true;
            wizard.currentAp = 1;
            ActionResolver.BeginTurn(wizard);

            ActionResolver.ApplyGuard(wizard);
            ActionResolver.BeginTurn(wizard);

            Assert.AreEqual(3, wizard.currentAp); // 1 + 1 regular + 1 for guarding
            Assert.IsFalse(wizard.pendingGuardApBonus, "The bonus is spent once.");
        }

        [Test]
        public void BeginTurn_NonCasterNeverGainsAp()
        {
            var fighter = MakeCombatant(true);
            ActionResolver.BeginTurn(fighter);
            ActionResolver.BeginTurn(fighter);

            Assert.AreEqual(0, fighter.currentAp);
        }

        [Test]
        public void RollInitiative_IsAD10PlusTheDexterityMinorModifier()
        {
            var ranger = MakeCombatant(true);
            ranger.initiative = 2;

            Assert.AreEqual(7, ActionResolver.RollInitiative(ranger, new ScriptedRolls(5)));
        }

        // ---------------------------------------------------------------- targeting and areas

        [Test]
        public void ResolveTargets_AreaCircleSkill_HitsOnlyEnemiesWithinRadius()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.targetType = TargetType.AreaCircle;

            var user = MakeCombatant(true, Vector3.zero);
            var nearEnemy = MakeCombatant(false, new Vector3(1f, 0f, 0f));
            var farEnemy = MakeCombatant(false, new Vector3(50f, 0f, 0f));
            var ally = MakeCombatant(true, new Vector3(1f, 0f, 0f));

            var all = new List<CombatantRuntime> { user, nearEnemy, farEnemy, ally };

            var targets = ActionResolver.ResolveTargets(skill, user, nearEnemy, all);

            Assert.Contains(nearEnemy, targets);
            Assert.IsFalse(targets.Contains(farEnemy));
            Assert.IsFalse(targets.Contains(ally));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveTargets_AreaCircleSkill_UsesTheSkillsOwnRadius()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.targetType = TargetType.AreaCircle;
            skill.aoeSize = 8f;

            var user = MakeCombatant(true, Vector3.zero);
            var target = MakeCombatant(false, new Vector3(0f, 0f, 10f));
            var edgeEnemy = MakeCombatant(false, new Vector3(6f, 0f, 10f));   // 6 from the target: inside radius 8, outside the default 3.5
            var tooFar = MakeCombatant(false, new Vector3(9f, 0f, 10f));

            var targets = ActionResolver.ResolveTargets(skill, user, target, new List<CombatantRuntime> { user, target, edgeEnemy, tooFar });

            Assert.Contains(edgeEnemy, targets);
            Assert.IsFalse(targets.Contains(tooFar));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveTargets_AreaConeSkill_HitsEnemiesInTheWedgeFromTheCasterTowardsTheTarget()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.targetType = TargetType.AreaCone;
            skill.aoeSize = 8f;
            skill.aoeAngle = 60f;

            var user = MakeCombatant(true, Vector3.zero);
            var target = MakeCombatant(false, new Vector3(0f, 0f, 5f));
            var inWedge = MakeCombatant(false, new Vector3(1.5f, 0f, 7f));
            var offToTheSide = MakeCombatant(false, new Vector3(6f, 0f, 3f));
            var behind = MakeCombatant(false, new Vector3(0f, 0f, -3f));
            var beyondLength = MakeCombatant(false, new Vector3(0f, 0f, 9f));

            var all = new List<CombatantRuntime> { user, target, inWedge, offToTheSide, behind, beyondLength };
            var targets = ActionResolver.ResolveTargets(skill, user, target, all);

            Assert.Contains(target, targets);
            Assert.Contains(inWedge, targets);
            Assert.IsFalse(targets.Contains(offToTheSide));
            Assert.IsFalse(targets.Contains(behind));
            Assert.IsFalse(targets.Contains(beyondLength));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveTargets_AreaLineSkill_HitsEnemiesInTheStripBetweenCasterAndBeyond()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.targetType = TargetType.AreaLine;
            skill.aoeSize = 10f;
            skill.aoeWidth = 2f;

            var user = MakeCombatant(true, Vector3.zero);
            var target = MakeCombatant(false, new Vector3(4f, 0f, 0f)); // line points along +X
            var behindTarget = MakeCombatant(false, new Vector3(9f, 0.2f, 0.5f));
            var tooWide = MakeCombatant(false, new Vector3(5f, 0f, 2f));

            var targets = ActionResolver.ResolveTargets(skill, user, target, new List<CombatantRuntime> { user, target, behindTarget, tooWide });

            Assert.Contains(target, targets);
            Assert.Contains(behindTarget, targets);
            Assert.IsFalse(targets.Contains(tooWide));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void TryGetAreaShape_FallsBackToDefaultsAndRejectsNonAreaSkills()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();

            skill.targetType = TargetType.AreaCircle;
            Assert.IsTrue(ActionResolver.TryGetAreaShape(skill, out var circle));
            Assert.AreEqual(ActionResolver.DefaultAoeRadius, circle.range);

            skill.targetType = TargetType.AreaCone;
            Assert.IsTrue(ActionResolver.TryGetAreaShape(skill, out var cone));
            Assert.AreEqual(ActionResolver.DefaultConeLength, cone.range);
            Assert.AreEqual(ActionResolver.DefaultConeAngle, cone.angle);

            skill.targetType = TargetType.SingleEnemy;
            Assert.IsFalse(ActionResolver.TryGetAreaShape(skill, out _));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveTargets_SingleEnemySkill_ReturnsOnlyThePrimaryTarget()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.targetType = TargetType.SingleEnemy;

            var user = MakeCombatant(true, Vector3.zero);
            var target = MakeCombatant(false, new Vector3(1f, 0f, 0f));
            var otherEnemy = MakeCombatant(false, new Vector3(1.5f, 0f, 0f));

            var targets = ActionResolver.ResolveTargets(skill, user, target, new List<CombatantRuntime> { user, target, otherEnemy });

            CollectionAssert.AreEqual(new[] { target }, targets);

            Object.DestroyImmediate(skill);
        }
    }
}
