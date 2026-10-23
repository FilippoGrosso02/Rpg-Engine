using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Data.Registry;
using Game.Data.State;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    /// <summary>
    /// The authored assets, built into combat runtimes, must reproduce the "Template Heroes &amp; Monsters" sheets number for number.
    /// If someone edits a hero or monster asset (or a formula) these fail loudly instead of silently rebalancing the game.
    /// </summary>
    public class RosterTemplateTests
    {
        [SetUp]
        public void SetUp()
        {
            DatabaseRegistry.Initialize();
            PartyState.HealAll();
        }

        [TearDown]
        public void TearDown()
        {
            PartyState.HealAll();
        }

        private static CombatantRuntime Hero(string id)
        {
            var def = DatabaseRegistry.GetActor(id);
            Assert.IsNotNull(def, $"Hero '{id}' is missing from the database.");
            return EncounterRunner.BuildAllyRuntime(def);
        }

        private static CombatantRuntime Monster(string id)
        {
            var def = DatabaseRegistry.GetEnemy(id);
            Assert.IsNotNull(def, $"Monster '{id}' is missing from the database.");
            return EncounterRunner.BuildEnemyRuntime(def);
        }

        private static void AssertDice(DiceRoll expected, DiceRoll actual)
        {
            Assert.AreEqual(expected.count, actual.count, "dice count");
            Assert.AreEqual(expected.sides, actual.sides, "die sides");
            Assert.AreEqual(expected.bonus, actual.bonus, "bonus");
        }

        // ---------------------------------------------------------------- heroes

        [Test]
        public void Warrior_MatchesTheSheet()
        {
            var warrior = Hero("actor_warrior");

            Assert.AreEqual(9, warrior.maxHp, "d8 + 1");
            Assert.AreEqual(14, warrior.ArmorClass, "6 + chain 7 + shield 1 + DEX 0");
            Assert.AreEqual(10, warrior.attackBonus, "STR 10");
            AssertDice(new DiceRoll(1, 8, 2), warrior.attackDamage); // sword d8 + 2
            Assert.AreEqual(8, warrior.saveVitality);
            Assert.AreEqual(5, warrior.saveMind);
            Assert.IsFalse(warrior.usesAp);
            Assert.AreEqual(1, warrior.attackRangeMultiplier, "melee");
            Assert.IsTrue(warrior.weaponIsMetal);
        }

        [Test]
        public void Ranger_MatchesTheSheet()
        {
            var ranger = Hero("actor_ranger");

            Assert.AreEqual(8, ranger.maxHp, "d8 + 0");
            Assert.AreEqual(12, ranger.ArmorClass, "6 + leather 4 + DEX 2");
            Assert.AreEqual(10, ranger.attackBonus, "ranged attack uses DEX 10");
            AssertDice(new DiceRoll(1, 6, 0), ranger.attackDamage); // shortbow d6, no stat bonus
            Assert.AreEqual(7, ranger.saveVitality);
            Assert.AreEqual(6, ranger.saveMind);
            Assert.Greater(ranger.attackRangeMultiplier, 1, "ranged");
            Assert.IsFalse(ranger.weaponIsMetal, "a bow does not corrode");
        }

        [Test]
        public void Wizard_MatchesTheSheet()
        {
            var wizard = Hero("actor_wizard");

            Assert.AreEqual(4, wizard.maxHp, "d4 + 0");
            Assert.AreEqual(6, wizard.ArmorClass, "6 + no armor + DEX 0");
            Assert.AreEqual(7, wizard.attackBonus, "thrown dagger attacks with DEX 7");
            AssertDice(new DiceRoll(1, 4, -1), wizard.attackDamage); // dagger d4 + STR mod (-1) when thrown
            Assert.AreEqual(7, wizard.saveVitality);
            Assert.AreEqual(10, wizard.saveMind);
            Assert.IsTrue(wizard.usesAp);
            Assert.AreEqual(1, wizard.currentAp, "AP starts at 1");
            CollectionAssert.AreEqual(
                new[] { "skill_basic_attack", "skill_magic_missile", "skill_web", "skill_fireball" },
                wizard.skillIds);
        }

        [Test]
        public void Heroes_RollInitiativeWithTheirDexterityScore()
        {
            Assert.AreEqual(6, Hero("actor_warrior").initiative);
            Assert.AreEqual(10, Hero("actor_ranger").initiative);
            Assert.AreEqual(7, Hero("actor_wizard").initiative);
        }

        // ---------------------------------------------------------------- monsters

        [Test]
        public void Goblin_MatchesTheSheet()
        {
            var goblin = Monster("enemy_goblin");

            Assert.AreEqual(5, goblin.maxHp);
            Assert.AreEqual(9, goblin.ArmorClass);
            Assert.AreEqual(5, goblin.attackBonus);
            AssertDice(new DiceRoll(1, 6, 0), goblin.attackDamage);
            Assert.AreEqual(7, goblin.morale);
        }

        [Test]
        public void Ogre_MatchesTheSheet()
        {
            var ogre = Monster("enemy_ogre");

            Assert.AreEqual(22, ogre.maxHp);
            Assert.AreEqual(13, ogre.ArmorClass);
            Assert.AreEqual(8, ogre.attackBonus);
            AssertDice(new DiceRoll(2, 6, 0), ogre.attackDamage);
            Assert.AreEqual(9, ogre.morale);
        }

        [Test]
        public void Slime_MatchesTheSheet_AndIsAcidicMindlessAndImmune()
        {
            var slime = Monster("enemy_slime");

            Assert.AreEqual(14, slime.maxHp);
            Assert.AreEqual(7, slime.ArmorClass);
            Assert.AreEqual(4, slime.attackBonus);
            AssertDice(new DiceRoll(1, 6, 0), slime.attackDamage);
            Assert.AreEqual(0, slime.morale, "mindless: never checks morale");
            Assert.IsTrue(slime.corrodesArmor);
            Assert.IsTrue(slime.corrodesWeapons);
            Assert.IsTrue(slime.immuneToCharm);
            Assert.IsTrue(slime.immuneToFear);
            Assert.IsTrue(slime.immuneToSleep);
        }

        // ---------------------------------------------------------------- spells

        [Test]
        public void MagicMissile_IsLevelOne_AutoHit_D6()
        {
            var spell = DatabaseRegistry.GetSkill("skill_magic_missile");

            Assert.IsTrue(spell.autoHit);
            AssertDice(new DiceRoll(1, 6, 0), spell.damage);
            Assert.AreEqual(1, spell.apCost);
            Assert.AreEqual(1, spell.spellLevel);
        }

        [Test]
        public void Web_IsLevelTwo_AVitalitySaveNegatesAStuckStatusOfTwoTurns()
        {
            var web = DatabaseRegistry.GetSkill("skill_web");

            Assert.AreEqual(2, web.apCost);
            Assert.AreEqual(SaveType.Vitality, web.saveType);
            Assert.IsFalse(web.halfDamageOnSave, "a passed save negates a web, it does not halve it");
            Assert.AreEqual(1f, web.statusEffectChance);

            var stuck = DatabaseRegistry.GetStatusEffect(web.statusEffectId);
            Assert.IsNotNull(stuck);
            Assert.AreEqual(2, stuck.duration);
            Assert.IsTrue(stuck.preventsMovement);
        }

        [Test]
        public void Fireball_IsLevelThree_TwoD6InAnAreaWithAHalvingSave()
        {
            var fireball = DatabaseRegistry.GetSkill("skill_fireball");

            Assert.AreEqual(3, fireball.apCost);
            Assert.AreEqual(TargetType.AreaCircle, fireball.targetType);
            AssertDice(new DiceRoll(2, 6, 0), fireball.damage);
            Assert.AreEqual(SaveType.Vitality, fireball.saveType);
            Assert.IsTrue(fireball.halfDamageOnSave);
        }

        // ---------------------------------------------------------------- persistence

        [Test]
        public void AcidDamage_CarriesIntoTheNextFight_UntilRepaired()
        {
            PartyState.SetCorrosion("actor_warrior", armor: 2, weapon: 3);

            var warrior = Hero("actor_warrior");
            Assert.AreEqual(2, warrior.armorCorrosion);
            Assert.AreEqual(12, warrior.ArmorClass); // 14 - 2
            Assert.AreEqual(3, warrior.weaponCorrosion);

            PartyState.HealAll(); // resting repairs it
            Assert.AreEqual(14, Hero("actor_warrior").ArmorClass);
        }

        [Test]
        public void ArmorCorrosion_CannotExceedTheArmorWorn()
        {
            PartyState.SetCorrosion("actor_ranger", armor: 9, weapon: 0); // leather is only worth 4

            Assert.AreEqual(8, Hero("actor_ranger").ArmorClass); // 12 - 4
        }
    }
}
