using Game.Data.Definitions;
using Game.Data.Rules;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    /// <summary>
    /// Pins the 5-stat d10 rules: the modifier tables, DEF, the hit-chance table (gap -> 10%..90%), skill tests and the matchup
    /// percentages for the template roster, so a change to a formula that would silently rebalance the game fails here.
    /// </summary>
    public class OsrRulesTests
    {
        [TestCase(2, -3)]
        [TestCase(3, -2)]
        [TestCase(4, -2)]
        [TestCase(5, -1)]
        [TestCase(6, -1)]
        [TestCase(7, 0)]
        [TestCase(8, 1)]
        [TestCase(9, 1)]
        [TestCase(10, 2)]
        [TestCase(11, 2)]
        [TestCase(12, 3)]
        public void Modifier_FollowsTheMinorModifierTable(int score, int expected)
        {
            Assert.AreEqual(expected, OsrRules.Modifier(score));
        }

        [TestCase(2, -5)]
        [TestCase(7, 0)]
        [TestCase(12, 5)]
        public void MajorModifier_IsScoreMinusSeven(int score, int expected)
        {
            Assert.AreEqual(expected, OsrRules.MajorModifier(score));
        }

        [Test]
        public void MaxHitPoints_IsTheHitDieMaximumPlusTheVitMinorModifier()
        {
            Assert.AreEqual(9, OsrRules.MaxHitPoints(8, vitality: 8), "fighter d8 + 1");
            Assert.AreEqual(8, OsrRules.MaxHitPoints(8, vitality: 7), "d8 + 0");
            Assert.AreEqual(5, OsrRules.MaxHitPoints(4, vitality: 8), "caster d4 + 1");
            Assert.AreEqual(1, OsrRules.MaxHitPoints(4, vitality: 2), "never below 1");
        }

        [Test]
        public void Defense_IsTwoPlusArmorAndShield_WithDexOnlyForEvadeClasses()
        {
            Assert.AreEqual(7, OsrRules.Defense(armorBonus: 4 + 1, dexterity: 7, evadesWithDex: false), "Warrior: chain 4 + shield 1");
            Assert.AreEqual(6, OsrRules.Defense(armorBonus: 2, dexterity: 10, evadesWithDex: true), "Ranger: leather 2 + DEX 10 (+2)");
            Assert.AreEqual(2, OsrRules.Defense(armorBonus: 0, dexterity: 7, evadesWithDex: false), "Wizard: nothing");
            Assert.AreEqual(2, OsrRules.Defense(armorBonus: 0, dexterity: 12, evadesWithDex: false), "DEX is ignored by other classes");
            Assert.AreEqual(2, OsrRules.Defense(armorBonus: 0, dexterity: 2, evadesWithDex: true), "minimum 2");
        }

        [Test]
        public void AttackHits_NaturalOneAlwaysMisses_NaturalTenAlwaysHits()
        {
            Assert.IsFalse(OsrRules.AttackHits(1, attack: 20, defense: 2));
            Assert.IsTrue(OsrRules.AttackHits(10, attack: 2, defense: 12));
        }

        [Test]
        public void AttackHits_OtherwiseNeedsRollPlusAtkMinusDefOfSix()
        {
            Assert.IsTrue(OsrRules.AttackHits(5, 10, 9), "5 + 10 - 9 = 6");
            Assert.IsFalse(OsrRules.AttackHits(4, 10, 9), "4 + 10 - 9 = 5");
        }

        [Test]
        public void TestPasses_UsesTheSameRuleAgainstADifficultyValue()
        {
            Assert.IsTrue(OsrRules.TestPasses(6, stat: 7, difficulty: 7), "6 + 7 - 7 = 6");
            Assert.IsFalse(OsrRules.TestPasses(5, stat: 7, difficulty: 7));
            Assert.IsTrue(OsrRules.TestPasses(10, stat: 2, difficulty: 12), "natural 10 always succeeds");
            Assert.IsFalse(OsrRules.TestPasses(1, stat: 12, difficulty: 2), "natural 1 always fails");
        }

        // The hit-chance table: gap = ATK - DEF, chance = (gap + 5) * 10%, clamped to 10-90%.
        [TestCase(-5, 10)]
        [TestCase(-4, 10)]
        [TestCase(-3, 20)]
        [TestCase(-2, 30)]
        [TestCase(-1, 40)]
        [TestCase(0, 50)]
        [TestCase(1, 60)]
        [TestCase(2, 70)]
        [TestCase(3, 80)]
        [TestCase(4, 90)]
        [TestCase(9, 90)]
        public void ChancePercent_IsGapPlusFiveTimesTen_ClampedToTenToNinety(int gap, int expected)
        {
            Assert.AreEqual(expected, OsrRules.ChancePercent(stat: 7 + gap, target: 7));
        }

        // The skill-test table at stat 7: DV 4 / 6 / 7 / 8 / 10 / 12.
        [TestCase(4, 80)]
        [TestCase(6, 60)]
        [TestCase(7, 50)]
        [TestCase(8, 40)]
        [TestCase(10, 20)]
        [TestCase(12, 10)]
        public void ChancePercent_MatchesTheSkillTestTableAtStatSeven(int difficulty, int expected)
        {
            Assert.AreEqual(expected, OsrRules.ChancePercent(stat: 7, target: difficulty));
        }

        [Test]
        public void Initiative_IsD10PlusTheDexMinorModifier()
        {
            Assert.AreEqual(12, OsrRules.Initiative(10, dexterity: 10));
            Assert.AreEqual(3, OsrRules.Initiative(5, dexterity: 4));
        }

        // Monster hits vs. hero DEF (Warrior 7, Ranger 6, Wizard 2).
        [TestCase(7, 7, 50)]  // Bandit vs Warrior
        [TestCase(7, 6, 60)]  // Bandit vs Ranger
        [TestCase(7, 2, 90)]  // Bandit vs Wizard
        [TestCase(9, 7, 70)]  // Ogre vs Warrior
        [TestCase(9, 6, 80)]  // Ogre vs Ranger
        [TestCase(9, 2, 90)]  // Ogre vs Wizard
        [TestCase(6, 7, 40)]  // Witch vs Warrior
        [TestCase(6, 6, 50)]  // Witch vs Ranger
        [TestCase(6, 2, 90)]  // Witch vs Wizard
        public void MonsterHitChances_MatchTheMatchupTable(int attack, int heroDef, int expectedPercent)
        {
            Assert.AreEqual(expectedPercent, OsrRules.ChancePercent(attack, heroDef));
        }

        // Hero hits vs. monster DEF (Bandit 4, Archer 8, Witch 4). Warrior and Ranger ATK 10, Wizard ATK 9 (DEX 7 + thrown dagger +2).
        [TestCase(10, 4, 90)]  // Warrior / Ranger vs Bandit
        [TestCase(10, 8, 70)]  // Warrior / Ranger vs Archer
        [TestCase(9, 4, 90)]   // Wizard (thrown dagger) vs Bandit
        [TestCase(9, 8, 60)]   // Wizard vs Archer
        public void HeroHitChances_MatchTheMatchupTable(int attack, int monsterDef, int expectedPercent)
        {
            Assert.AreEqual(expectedPercent, OsrRules.ChancePercent(attack, monsterDef));
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
