using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    public class FearRuntimeTests
    {
        private static CombatantRuntime MakeCombatant(int fear = 0)
        {
            return new CombatantRuntime { maxHp = 10, currentHp = 10, fear = fear };
        }

        [TestCase(0, 0)]
        [TestCase(3, 0)]
        [TestCase(4, 1)]
        [TestCase(6, 1)]
        [TestCase(7, 2)]
        [TestCase(9, 2)]
        [TestCase(10, 3)]
        public void GetStatPenalty_MatchesThresholds(int fear, int expectedPenalty)
        {
            var combatant = MakeCombatant(fear);
            Assert.AreEqual(expectedPenalty, FearRuntime.GetStatPenalty(combatant));
        }

        [Test]
        public void ShouldFreeze_TrueOnlyAtTenOrAbove()
        {
            Assert.IsFalse(FearRuntime.ShouldFreeze(MakeCombatant(9)));
            Assert.IsTrue(FearRuntime.ShouldFreeze(MakeCombatant(10)));
        }

        [Test]
        public void ApplyFearFromHit_NormalHit_GainsTwoAndMarksHit()
        {
            var target = MakeCombatant(0);
            FearRuntime.ApplyFearFromHit(target, wasCrit: false, attackerIsHorror: false);

            Assert.AreEqual(2, target.fear);
            Assert.IsTrue(target.wasHitSinceLastTurn);
        }

        [Test]
        public void ApplyFearFromHit_Crit_GainsThree()
        {
            var target = MakeCombatant(0);
            FearRuntime.ApplyFearFromHit(target, wasCrit: true, attackerIsHorror: false);

            Assert.AreEqual(3, target.fear);
        }

        [Test]
        public void ApplyFearFromHit_HorrorAttacker_AddsOneMore()
        {
            var target = MakeCombatant(0);
            FearRuntime.ApplyFearFromHit(target, wasCrit: false, attackerIsHorror: true);

            Assert.AreEqual(3, target.fear); // 2 normal + 1 Horror
        }

        [Test]
        public void ApplyFearFromHit_ClampsAtTen()
        {
            var target = MakeCombatant(9);
            FearRuntime.ApplyFearFromHit(target, wasCrit: true, attackerIsHorror: true); // would be 9+4=13

            Assert.AreEqual(10, target.fear);
        }

        [Test]
        public void DecayAtTurnStart_DecaysByOne_WhenNotHitSinceLastTurn()
        {
            var combatant = MakeCombatant(5);
            combatant.wasHitSinceLastTurn = false;

            FearRuntime.DecayAtTurnStart(combatant);

            Assert.AreEqual(4, combatant.fear);
        }

        [Test]
        public void DecayAtTurnStart_DoesNotDecay_WhenHitSinceLastTurn_ButResetsTheFlag()
        {
            var combatant = MakeCombatant(5);
            combatant.wasHitSinceLastTurn = true;

            FearRuntime.DecayAtTurnStart(combatant);

            Assert.AreEqual(5, combatant.fear);
            Assert.IsFalse(combatant.wasHitSinceLastTurn);
        }

        [Test]
        public void DecayAtTurnStart_NeverGoesBelowZero()
        {
            var combatant = MakeCombatant(0);
            combatant.wasHitSinceLastTurn = false;

            FearRuntime.DecayAtTurnStart(combatant);

            Assert.AreEqual(0, combatant.fear);
        }
    }
}
