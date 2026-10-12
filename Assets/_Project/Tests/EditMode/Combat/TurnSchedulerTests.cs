using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    public class TurnSchedulerTests
    {
        private static CombatantRuntime MakeCombatant(string name, int speed)
        {
            return new CombatantRuntime { displayName = name, speed = speed, maxHp = 10, currentHp = 10 };
        }

        [Test]
        public void AdvanceToNextActor_OrdersEachRoundBySpeedDescending()
        {
            var fast = MakeCombatant("Fast", 8);
            var mid = MakeCombatant("Mid", 4);
            var slow = MakeCombatant("Slow", 1);
            var combatants = new List<CombatantRuntime> { slow, fast, mid }; // deliberately out of order

            var scheduler = new TurnScheduler(combatants);

            var firstRound = new[]
            {
                scheduler.AdvanceToNextActor(),
                scheduler.AdvanceToNextActor(),
                scheduler.AdvanceToNextActor()
            };

            CollectionAssert.AreEqual(new[] { fast, mid, slow }, firstRound);
        }

        [Test]
        public void AdvanceToNextActor_StartsANewRoundOnceTheCurrentOneIsExhausted()
        {
            var a = MakeCombatant("A", 5);
            var b = MakeCombatant("B", 3);
            var combatants = new List<CombatantRuntime> { a, b };

            var scheduler = new TurnScheduler(combatants);

            var order = new List<CombatantRuntime>();
            for (int i = 0; i < 4; i++)
            {
                order.Add(scheduler.AdvanceToNextActor());
            }

            CollectionAssert.AreEqual(new[] { a, b, a, b }, order);
        }

        [Test]
        public void AdvanceToNextActor_SkipsDefeatedCombatants()
        {
            var a = MakeCombatant("A", 5);
            var b = MakeCombatant("B", 3);
            b.currentHp = 0; // defeated before the round even starts
            var combatants = new List<CombatantRuntime> { a, b };

            var scheduler = new TurnScheduler(combatants);

            Assert.AreEqual(a, scheduler.AdvanceToNextActor());
            Assert.AreEqual(a, scheduler.AdvanceToNextActor()); // next round: only A is alive
        }

        [Test]
        public void AdvanceToNextActor_ReturnsNull_WhenNobodyIsAlive()
        {
            var a = MakeCombatant("A", 5);
            a.currentHp = 0;
            var scheduler = new TurnScheduler(new List<CombatantRuntime> { a });

            Assert.IsNull(scheduler.AdvanceToNextActor());
        }

        [Test]
        public void AdvanceToNextActor_EqualSpeedTies_AreBrokenAndBothActEachRound()
        {
            var x = MakeCombatant("X", 4);
            var y = MakeCombatant("Y", 4);
            var combatants = new List<CombatantRuntime> { x, y };

            var scheduler = new TurnScheduler(combatants);

            var firstRound = new HashSet<CombatantRuntime> { scheduler.AdvanceToNextActor(), scheduler.AdvanceToNextActor() };

            CollectionAssert.AreEquivalent(new[] { x, y }, firstRound);
        }

        [Test]
        public void PreviewUpcomingOrder_ReturnsRequestedCountAndSkipsDefeated()
        {
            var a = MakeCombatant("A", 5);
            var b = MakeCombatant("B", 3);
            var combatants = new List<CombatantRuntime> { a, b };
            var scheduler = new TurnScheduler(combatants);

            var preview = scheduler.PreviewUpcomingOrder(5);

            Assert.AreEqual(5, preview.Count);
            Assert.IsTrue(preview.All(c => c == a || c == b));
        }
    }
}
