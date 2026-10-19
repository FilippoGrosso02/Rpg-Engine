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
        public void ResolveTargets_AreaCircleSkill_UsesTheSkillsOwnRadius()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.targetType = TargetType.AreaCircle;
            skill.aoeSize = 8f;

            var user = MakeCombatant(true, 5, 0, Vector3.zero);
            var target = MakeCombatant(false, 0, 0, new Vector3(0f, 0f, 10f));
            var edgeEnemy = MakeCombatant(false, 0, 0, new Vector3(6f, 0f, 10f));   // 6 from the target: inside radius 8, outside the default 3.5
            var tooFar = MakeCombatant(false, 0, 0, new Vector3(9f, 0f, 10f));

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

            var user = MakeCombatant(true, 5, 0, Vector3.zero);
            var target = MakeCombatant(false, 0, 0, new Vector3(0f, 0f, 5f));
            var inWedge = MakeCombatant(false, 0, 0, new Vector3(1.5f, 0f, 7f));
            var offToTheSide = MakeCombatant(false, 0, 0, new Vector3(6f, 0f, 3f));
            var behind = MakeCombatant(false, 0, 0, new Vector3(0f, 0f, -3f));
            var beyondLength = MakeCombatant(false, 0, 0, new Vector3(0f, 0f, 9f));

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

            var user = MakeCombatant(true, 5, 0, Vector3.zero);
            var target = MakeCombatant(false, 0, 0, new Vector3(4f, 0f, 0f)); // line points along +X
            var behindTarget = MakeCombatant(false, 0, 0, new Vector3(9f, 0.2f, 0.5f));
            var tooWide = MakeCombatant(false, 0, 0, new Vector3(5f, 0f, 2f));

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
