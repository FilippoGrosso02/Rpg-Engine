using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    /// <summary>Morale is 2d6 against the monster's number: a roll HIGHER than morale flees. The scripted dice come in pairs.</summary>
    public class MoraleRuntimeTests
    {
        private class ScriptedRolls : System.Random
        {
            private readonly Queue<int> _rolls;
            public ScriptedRolls(params int[] rolls) => _rolls = new Queue<int>(rolls);
            public int Remaining => _rolls.Count;
            public override int Next(int minValue, int maxValue) => _rolls.Dequeue();
        }

        private static CombatantRuntime Goblin(int morale = 7, int hp = 5)
        {
            return new CombatantRuntime { displayName = "Goblin", maxHp = 5, currentHp = hp, morale = morale };
        }

        private static CombatantRuntime Hero()
        {
            return new CombatantRuntime { displayName = "Hero", isPlayerSide = true, maxHp = 9, currentHp = 9 };
        }

        [Test]
        public void NoCasualtiesYet_NobodyChecks()
        {
            var group = new List<CombatantRuntime> { Hero(), Goblin(), Goblin(), Goblin() };

            var checks = MoraleRuntime.CheckSide(group, new ScriptedRolls());

            Assert.IsEmpty(checks);
        }

        [Test]
        public void FirstCasualty_MakesTheSurvivorsRoll_AndAHighRollSendsThemFleeing()
        {
            var dead = Goblin(hp: 0);
            var scared = Goblin(morale: 7);
            var brave = Goblin(morale: 7);
            var group = new List<CombatantRuntime> { Hero(), dead, scared, brave };

            // scared: 4 + 5 = 9 > 7 flees. brave: 3 + 3 = 6 <= 7 holds. (Order follows the list.)
            var checks = MoraleRuntime.CheckSide(group, new ScriptedRolls(4, 5, 3, 3));

            Assert.AreEqual(2, checks.Count);
            Assert.IsTrue(scared.hasFled);
            Assert.IsFalse(brave.hasFled);
            Assert.IsFalse(dead.hasFled, "The slain do not roll.");
        }

        [Test]
        public void ARollEqualToMoraleHolds()
        {
            var dead = Goblin(hp: 0);
            var survivor = Goblin(morale: 7);

            MoraleRuntime.CheckSide(new List<CombatantRuntime> { Hero(), dead, survivor }, new ScriptedRolls(3, 4)); // 7

            Assert.IsFalse(survivor.hasFled);
        }

        [Test]
        public void EachTriggerFiresOnlyOncePerMonster()
        {
            var dead = Goblin(hp: 0);
            var survivor = Goblin(morale: 12); // can never flee (2d6 max is 12, not higher)
            var group = new List<CombatantRuntime> { Hero(), dead, survivor };

            var first = MoraleRuntime.CheckSide(group, new ScriptedRolls(6, 6));
            var second = MoraleRuntime.CheckSide(group, new ScriptedRolls());

            Assert.AreEqual(1, first.Count);
            Assert.IsEmpty(second, "The first-casualty and half-down triggers were already spent.");
        }

        [Test]
        public void HalfTheGroupDown_TriggersACheck_EvenWithoutAFreshCasualty()
        {
            // One goblin fled earlier (so it is down but not slain), two still stand: half the group (1 of 3 isn't half, 2 of 4 is).
            var fled = Goblin();
            fled.hasFled = true;
            var alsoFled = Goblin();
            alsoFled.hasFled = true;
            var standing = Goblin(morale: 7);
            var alsoStanding = Goblin(morale: 7);

            var checks = MoraleRuntime.CheckSide(new List<CombatantRuntime> { Hero(), fled, alsoFled, standing, alsoStanding }, new ScriptedRolls(6, 6, 1, 1));

            Assert.AreEqual(2, checks.Count);
            Assert.IsTrue(checks.All(c => c.trigger.Contains("half the group")));
            Assert.IsTrue(standing.hasFled);
        }

        [Test]
        public void LoneMonsterAtHalfHp_ChecksOnce()
        {
            var ogre = new CombatantRuntime { displayName = "Ogre", maxHp = 22, currentHp = 11, morale = 9 };

            var checks = MoraleRuntime.CheckSide(new List<CombatantRuntime> { Hero(), ogre }, new ScriptedRolls(6, 6)); // 12 > 9

            Assert.AreEqual(1, checks.Count);
            Assert.AreEqual("half HP alone", checks[0].trigger);
            Assert.IsTrue(ogre.hasFled);
        }

        [Test]
        public void LoneMonsterAboveHalfHp_DoesNotCheck()
        {
            var ogre = new CombatantRuntime { displayName = "Ogre", maxHp = 22, currentHp = 12, morale = 9 };

            Assert.IsEmpty(MoraleRuntime.CheckSide(new List<CombatantRuntime> { Hero(), ogre }, new ScriptedRolls()));
        }

        [Test]
        public void MindlessMonsters_NeverCheck()
        {
            var dead = Goblin(hp: 0);
            var slime = new CombatantRuntime { displayName = "Slime", maxHp = 14, currentHp = 3, morale = 0 };

            var checks = MoraleRuntime.CheckSide(new List<CombatantRuntime> { Hero(), dead, slime }, new ScriptedRolls());

            Assert.IsEmpty(checks);
            Assert.IsFalse(slime.hasFled);
        }

        [Test]
        public void HeroesNeverCheckMorale()
        {
            var fallenHero = new CombatantRuntime { displayName = "Down", isPlayerSide = true, maxHp = 9, currentHp = 0, morale = 7 };
            var hero = new CombatantRuntime { displayName = "Hero", isPlayerSide = true, maxHp = 9, currentHp = 2, morale = 7 };

            Assert.IsEmpty(MoraleRuntime.CheckSide(new List<CombatantRuntime> { fallenHero, hero }, new ScriptedRolls()));
        }

        [Test]
        public void AFledMonster_CountsAsDefeatedButNotSlain()
        {
            var goblin = Goblin();
            goblin.hasFled = true;

            Assert.IsTrue(goblin.IsDefeated);
            Assert.IsFalse(goblin.IsSlain);
        }
    }
}
