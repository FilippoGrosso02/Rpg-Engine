using Game.Data.Definitions;
using Game.Data.Rules;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    /// <summary>
    /// Pins the d12 rules to the template sheets: every number here (HP, AC, the 92% / 83% / 33% matchup percentages) is copied
    /// from the "Template Heroes &amp; Monsters" notes, so a change to a formula that would silently rebalance the game fails here.
    /// </summary>
    public class OsrRulesTests
    {
        [TestCase(4, -1)]
        [TestCase(5, -1)]
        [TestCase(6, 0)]
        [TestCase(7, 0)]
        [TestCase(8, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        public void Modifier_IsFloorOfScoreMinusSixOverTwo(int score, int expected)
        {
            Assert.AreEqual(expected, OsrRules.Modifier(score));
        }

        [Test]
        public void MaxHitPoints_MatchTheTemplateHeroes()
        {
            Assert.AreEqual(9, OsrRules.MaxHitPoints(8, vitality: 8), "Warrior: d8 + 1");
            Assert.AreEqual(8, OsrRules.MaxHitPoints(8, vitality: 7), "Ranger: d8 + 0");
            Assert.AreEqual(4, OsrRules.MaxHitPoints(4, vitality: 7), "Wizard: d4 + 0");
        }

        [Test]
        public void ArmorClass_MatchesTheTemplateHeroes()
        {
            Assert.AreEqual(14, OsrRules.ArmorClass(armorBonus: 7 + 1), "Warrior: chain 7 + shield 1");
            Assert.AreEqual(10, OsrRules.ArmorClass(armorBonus: 4), "Ranger: leather 4 (DEX no longer counts)");
            Assert.AreEqual(6, OsrRules.ArmorClass(armorBonus: 0), "Wizard: no armor");
        }

        [Test]
        public void AttackHits_NaturalOneAlwaysMisses_NaturalTwelveAlwaysHits()
        {
            Assert.IsFalse(OsrRules.AttackHits(1, attackBonus: 20, armorClass: 1));
            Assert.IsTrue(OsrRules.AttackHits(12, attackBonus: -5, armorClass: 30));
        }

        [Test]
        public void AttackHits_OtherwiseNeedsRollPlusBonusToReachArmorClass()
        {
            Assert.IsTrue(OsrRules.AttackHits(5, 5, 10));
            Assert.IsFalse(OsrRules.AttackHits(4, 5, 10));
        }

        [Test]
        public void SavePasses_NeedsRollPlusScoreOfTwelve()
        {
            Assert.IsTrue(OsrRules.SavePasses(4, 8));
            Assert.IsFalse(OsrRules.SavePasses(3, 8));
        }

        private static int HitPercent(int attackBonus, int armorClass)
        {
            int hits = 0;
            for (int roll = 1; roll <= OsrRules.DieSides; roll++)
            {
                if (OsrRules.AttackHits(roll, attackBonus, armorClass))
                {
                    hits++;
                }
            }

            return (int)System.Math.Round(hits * 100f / OsrRules.DieSides);
        }

        // "Monster hits vs. hero AC" table.
        [TestCase(5, 14, 33)] // Goblin vs Warrior
        [TestCase(5, 10, 67)] // Goblin vs Ranger (AC 10 now that DEX no longer counts)
        [TestCase(5, 6, 92)]  // Goblin vs Wizard
        [TestCase(8, 14, 58)] // Ogre vs Warrior
        [TestCase(8, 10, 92)] // Ogre vs Ranger
        [TestCase(8, 6, 92)]  // Ogre vs Wizard
        [TestCase(4, 14, 25)] // Slime vs Warrior
        [TestCase(4, 10, 58)] // Slime vs Ranger
        [TestCase(4, 6, 92)]  // Slime vs Wizard
        public void MonsterHitChances_MatchTheMatchupTable(int attack, int heroAc, int expectedPercent)
        {
            Assert.AreEqual(expectedPercent, HitPercent(attack, heroAc));
        }

        // Hero hits vs. monster AC. Heroes attack with their ability MODIFIER now (Warrior STR 10 = +2, Wizard DEX 7 = +0), not the raw score.
        [TestCase(2, 9, 50)]   // Warrior vs Goblin
        [TestCase(2, 13, 17)]  // Warrior vs Ogre
        [TestCase(2, 7, 67)]   // Warrior vs Slime
        [TestCase(0, 9, 33)]   // Wizard (thrown dagger) vs Goblin
        [TestCase(0, 13, 8)]   // Wizard vs Ogre
        [TestCase(0, 7, 50)]   // Wizard vs Slime
        public void HeroHitChances_MatchTheMatchupTable(int attack, int monsterAc, int expectedPercent)
        {
            Assert.AreEqual(expectedPercent, HitPercent(attack, monsterAc));
        }

        [Test]
        public void DiceRoll_ToStringReadsLikeTheSheet()
        {
            Assert.AreEqual("d8+2", new DiceRoll(1, 8, 2).ToString());
            Assert.AreEqual("2d6", new DiceRoll(2, 6).ToString());
            Assert.AreEqual("d4-1", new DiceRoll(1, 4, -1).ToString());
        }

        [Test]
        public void DiceRoll_RollSumsEachDieAndAddsTheBonus()
        {
            var rng = new System.Random(7);
            var dice = new DiceRoll(2, 6, 1);
            for (int i = 0; i < 200; i++)
            {
                int roll = dice.Roll(rng);
                Assert.That(roll, Is.InRange(dice.Min, dice.Max));
            }
        }
    }
}
