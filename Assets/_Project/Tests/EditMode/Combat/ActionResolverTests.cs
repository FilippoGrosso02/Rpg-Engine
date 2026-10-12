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
    /// Duelwright's ActionResolver rolls real dice, so these tests inject a seeded
    /// System.Random (ResolveDamage's optional rng param) to keep assertions exact instead of
    /// asserting on live UnityEngine.Random.
    /// </summary>
    public class ActionResolverTests
    {
        private static CombatantRuntime MakeCombatant(bool isPlayerSide, int atk, int def, Vector3 position)
        {
            return new CombatantRuntime
            {
                isPlayerSide = isPlayerSide,
                maxHp = 20,
                currentHp = 20,
                atk = atk,
                def = def,
                position = position
            };
        }

        private static SkillDefinition MakeBasicAttackSkill()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.targetType = TargetType.SingleEnemy;
            return skill;
        }

        /// <summary>A System.Random stub that always returns a fixed d6 roll.</summary>
        private class FixedRoll : System.Random
        {
            private readonly int _roll;
            public FixedRoll(int roll) => _roll = roll;
            public override int Next(int minValue, int maxValue) => _roll;
        }

        [Test]
        public void ResolveDamage_NonCritRoll_UsesRollPlusAtkMinusDef_WithMinimumOfOne()
        {
            var skill = MakeBasicAttackSkill();
            var attacker = MakeCombatant(true, atk: 5, def: 0, Vector3.zero);
            var target = MakeCombatant(false, atk: 0, def: 3, Vector3.zero);

            var info = ActionResolver.ResolveDamage(attacker, target, skill, new FixedRoll(4));

            Assert.AreEqual(6, info.damage); // 4 + 5 - 3
            Assert.AreEqual(4, info.diceRoll);
            Assert.IsFalse(info.wasCrit);

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveDamage_NeverGoesBelowOne()
        {
            var skill = MakeBasicAttackSkill();
            var attacker = MakeCombatant(true, atk: 0, def: 0, Vector3.zero);
            var target = MakeCombatant(false, atk: 0, def: 999, Vector3.zero);

            var info = ActionResolver.ResolveDamage(attacker, target, skill, new FixedRoll(1));

            Assert.AreEqual(1, info.damage);

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveDamage_NaturalSix_CritsAndIgnoresDef()
        {
            var skill = MakeBasicAttackSkill();
            var attacker = MakeCombatant(true, atk: 5, def: 0, Vector3.zero);
            var target = MakeCombatant(false, atk: 0, def: 999, Vector3.zero);

            var info = ActionResolver.ResolveDamage(attacker, target, skill, new FixedRoll(6));

            Assert.AreEqual(11, info.damage); // atk 5 + 6, DEF ignored entirely
            Assert.IsTrue(info.wasCrit);
            Assert.IsTrue(info.defIgnored);

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveDamage_TargetGuarding_AddsTwoEffectiveDef()
        {
            var skill = MakeBasicAttackSkill();
            var attacker = MakeCombatant(true, atk: 5, def: 0, Vector3.zero);
            var target = MakeCombatant(false, atk: 0, def: 3, Vector3.zero);
            target.guarding = true;

            var info = ActionResolver.ResolveDamage(attacker, target, skill, new FixedRoll(4));

            Assert.AreEqual(4, info.damage); // 4 + 5 - (3 + 2 guard bonus)
            Assert.AreEqual(5, info.effectiveDef);

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveTargets_AreaCircleSkill_HitsOnlyEnemiesWithinRadius()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.targetType = TargetType.AreaCircle;

            var user = MakeCombatant(true, 5, 0, Vector3.zero);
            var nearEnemy = MakeCombatant(false, 0, 0, new Vector3(1f, 0f, 0f));
            var farEnemy = MakeCombatant(false, 0, 0, new Vector3(50f, 0f, 0f));
            var ally = MakeCombatant(true, 0, 0, new Vector3(1f, 0f, 0f));

            var all = new List<CombatantRuntime> { user, nearEnemy, farEnemy, ally };

            var targets = ActionResolver.ResolveTargets(skill, user, nearEnemy, all);

            Assert.Contains(nearEnemy, targets);
            Assert.IsFalse(targets.Contains(farEnemy));
            Assert.IsFalse(targets.Contains(ally));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ResolveTargets_SingleEnemySkill_ReturnsOnlyThePrimaryTarget()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.targetType = TargetType.SingleEnemy;

            var user = MakeCombatant(true, 5, 0, Vector3.zero);
            var target = MakeCombatant(false, 0, 0, new Vector3(1f, 0f, 0f));
            var otherEnemy = MakeCombatant(false, 0, 0, new Vector3(1.5f, 0f, 0f));

            var targets = ActionResolver.ResolveTargets(skill, user, target, new List<CombatantRuntime> { user, target, otherEnemy });

            CollectionAssert.AreEqual(new[] { target }, targets);

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void ApplyGuard_SetsGuardingAndPendingApBonus_AndReducesFear()
        {
            var combatant = MakeCombatant(true, 5, 0, Vector3.zero);
            combatant.fear = 5;

            ActionResolver.ApplyGuard(combatant);

            Assert.IsTrue(combatant.guarding);
            Assert.IsTrue(combatant.pendingGuardApBonus);
            Assert.AreEqual(3, combatant.fear);
        }
    }
}
