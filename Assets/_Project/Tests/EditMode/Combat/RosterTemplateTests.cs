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
    /// The authored assets, built into combat runtimes, must reproduce the d10 roster sheets number for number (see OsrRosterSetup).
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

            Assert.AreEqual(9, warrior.maxHp, "d8 + VIT 8 (+1)");
            Assert.AreEqual(7, warrior.ArmorClass, "2 + chain 4 + shield 1, no DEX for a non-evade class");
            Assert.AreEqual(10, warrior.attackBonus, "STR 10 + shortsword +0");
            AssertDice(new DiceRoll(1, 6, 0), warrior.attackDamage); // shortsword d6, no ability bonus
            Assert.AreEqual(8, warrior.saveVitality);
            Assert.AreEqual(7, warrior.saveMind);
            Assert.IsFalse(warrior.usesAp);
            Assert.AreEqual(1, warrior.attackRangeMultiplier, "melee");
            Assert.IsTrue(warrior.weaponIsMetal);
        }

        [Test]
        public void Ranger_MatchesTheSheet()
        {
            var ranger = Hero("actor_ranger");

            Assert.AreEqual(9, ranger.maxHp, "d8 + VIT 8 (+1)");
            Assert.AreEqual(6, ranger.ArmorClass, "2 + leather 2 + DEX 10 minor modifier (+2): rangers evade");
            Assert.AreEqual(10, ranger.attackBonus, "ranged attack uses the DEX 10 score + shortbow +0");
            AssertDice(new DiceRoll(1, 6, 0), ranger.attackDamage); // shortbow d6
            Assert.AreEqual(8, ranger.saveVitality);
            Assert.AreEqual(7, ranger.saveMind);
            Assert.Greater(ranger.attackRangeMultiplier, 1, "ranged");
            Assert.IsFalse(ranger.weaponIsMetal, "a bow does not corrode");
        }

        [Test]
        public void Thief_MatchesTheSheet()
        {
            var thief = Hero("actor_thief");

            Assert.AreEqual(6, thief.maxHp, "d6 + VIT 7 (+0)");
            Assert.AreEqual(5, thief.ArmorClass, "2 + leather 2 + DEX 9 minor modifier (+1): thieves evade");
            Assert.AreEqual(11, thief.attackBonus, "melee: STR 9 + dagger +2");
            AssertDice(new DiceRoll(1, 4, 0), thief.attackDamage); // dagger d4
            Assert.AreEqual(7, thief.saveVitality);
            Assert.AreEqual(7, thief.saveMind);
            Assert.IsFalse(thief.usesAp);
            Assert.AreEqual(1, thief.attackRangeMultiplier, "melee");
            CollectionAssert.AreEqual(new[] { "skill_basic_attack" }, thief.skillIds);
        }

        [Test]
        public void Heroes_RollInitiativeWithTheirDexterityMinorModifier()
        {
            Assert.AreEqual(0, Hero("actor_warrior").initiative, "DEX 7");
            Assert.AreEqual(2, Hero("actor_ranger").initiative, "DEX 10");
            Assert.AreEqual(1, Hero("actor_thief").initiative, "DEX 9");
        }

        // ---------------------------------------------------------------- monsters

        [Test]
        public void Goblin_MatchesTheSheet()
        {
            var goblin = Monster("enemy_goblin");

            Assert.AreEqual(5, goblin.maxHp);
            Assert.AreEqual(4, goblin.ArmorClass);
            Assert.AreEqual(7, goblin.attackBonus);
            AssertDice(new DiceRoll(1, 6, 0), goblin.attackDamage);
            Assert.AreEqual(7, goblin.morale);
        }

        [Test]
        public void Ogre_MatchesTheSheet()
        {
            var ogre = Monster("enemy_ogre");

            Assert.AreEqual(22, ogre.maxHp);
            Assert.AreEqual(8, ogre.ArmorClass);
            Assert.AreEqual(9, ogre.attackBonus);
            AssertDice(new DiceRoll(2, 6, 0), ogre.attackDamage);
            Assert.AreEqual(9, ogre.morale);
        }

        [Test]
        public void Slime_MatchesTheSheet_AndIsAcidicMindlessAndImmune()
        {
            var slime = Monster("enemy_slime");

            Assert.AreEqual(14, slime.maxHp);
            Assert.AreEqual(4, slime.ArmorClass);
            Assert.AreEqual(6, slime.attackBonus);
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
            Assert.AreEqual(8, web.saveDifficulty);
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
            Assert.AreEqual(7, fireball.saveDifficulty);
            Assert.IsTrue(fireball.halfDamageOnSave);
        }

        // ---------------------------------------------------------------- persistence

        [Test]
        public void AcidDamage_CarriesIntoTheNextFight_UntilRepaired()
        {
            PartyState.SetCorrosion("actor_warrior", armor: 2, weapon: 3);

            var warrior = Hero("actor_warrior");
            Assert.AreEqual(2, warrior.armorCorrosion);
            Assert.AreEqual(5, warrior.ArmorClass); // 7 - 2
            Assert.AreEqual(3, warrior.weaponCorrosion);

            PartyState.HealAll(); // resting repairs it
            Assert.AreEqual(7, Hero("actor_warrior").ArmorClass);
        }

        [Test]
        public void ArmorCorrosion_CannotExceedTheArmorWorn()
        {
            PartyState.SetCorrosion("actor_ranger", armor: 9, weapon: 0); // leather is only worth 2

            Assert.AreEqual(4, Hero("actor_ranger").ArmorClass); // 6 - 2
        }
    }
}
