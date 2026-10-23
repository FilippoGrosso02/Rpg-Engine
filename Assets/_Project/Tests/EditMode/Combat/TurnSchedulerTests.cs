using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    public class TurnSchedulerTests
    {
        /// <summary>Every d12 comes up the same number, so initiative order is decided purely by the bonuses.</summary>
        private class FlatRolls : System.Random
        {
            private readonly int _value;
            public FlatRolls(int value) => _value = value;
            public override int Next(int minValue, int maxValue) => _value;
        }

        private static CombatantRuntime MakeCombatant(string name, int initiative)
        {
            return new CombatantRuntime { displayName = name, initiative = initiative, maxHp = 10, currentHp = 10 };
        }

        [Test]
        public void AdvanceToNextActor_OrdersEachRoundByInitiativeDescending()
        {
            var fast = MakeCombatant("Fast", 10);
            var mid = MakeCombatant("Mid", 7);
            var slow = MakeCombatant("Slow", 3);
            var combatants = new List<CombatantRuntime> { slow, fast, mid }; // deliberately out of order

            var scheduler = new TurnScheduler(combatants, new FlatRolls(6));

            var firstRound = new[]
            {
                scheduler.AdvanceToNextActor(),
                scheduler.AdvanceToNextActor(),
                scheduler.AdvanceToNextActor()
            };

            CollectionAssert.AreEqual(new[] { fast, mid, slow }, firstRound);
        }

        [Test]
        public void AdvanceToNextActor_TheD12CanOutweighASmallInitiativeEdge()
        {
            // Both roll via a script: the one with the lower bonus rolls much higher.
            var scripted = new ScriptedRolls(1, 12); // a: d12 = 1 (+10 = 11); b: d12 = 12 (+6 = 18)
            var a = MakeCombatant("A", 10);
            var b = MakeCombatant("B", 6);

            var scheduler = new TurnScheduler(new List<CombatantRuntime> { a, b }, scripted);

            Assert.AreEqual(b, scheduler.AdvanceToNextActor());
            Assert.AreEqual(a, scheduler.AdvanceToNextActor());
        }

        private class ScriptedRolls : System.Random
        {
            private readonly Queue<int> _rolls;
            public ScriptedRolls(params int[] rolls) => _rolls = new Queue<int>(rolls);
            public override int Next(int minValue, int maxValue) => _rolls.Dequeue();
        }

        [Test]
        public void AdvanceToNextActor_StartsANewRoundOnceTheCurrentOneIsExhausted()
        {
            var a = MakeCombatant("A", 9);
            var b = MakeCombatant("B", 4);

            var scheduler = new TurnScheduler(new List<CombatantRuntime> { a, b }, new FlatRolls(6));

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
            var a = MakeCombatant("A", 9);
            var b = MakeCombatant("B", 4);
            b.currentHp = 0; // defeated before the round even starts

            var scheduler = new TurnScheduler(new List<CombatantRuntime> { a, b }, new FlatRolls(6));

            Assert.AreEqual(a, scheduler.AdvanceToNextActor());
            Assert.AreEqual(a, scheduler.AdvanceToNextActor()); // next round: only A is standing
        }

        [Test]
        public void AdvanceToNextActor_SkipsMonstersThatFled()
        {
            var hero = MakeCombatant("Hero", 9);
            var goblin = MakeCombatant("Goblin", 7);
            goblin.hasFled = true;

            var scheduler = new TurnScheduler(new List<CombatantRuntime> { hero, goblin }, new FlatRolls(6));

            Assert.AreEqual(hero, scheduler.AdvanceToNextActor());
            Assert.AreEqual(hero, scheduler.AdvanceToNextActor());
        }

        [Test]
        public void AdvanceToNextActor_ReturnsNull_WhenNobodyIsStanding()
        {
            var a = MakeCombatant("A", 5);
            a.currentHp = 0;
            var scheduler = new TurnScheduler(new List<CombatantRuntime> { a }, new FlatRolls(6));

            Assert.IsNull(scheduler.AdvanceToNextActor());
        }

        [Test]
        public void AdvanceToNextActor_ExactTies_AreBrokenAndBothActEachRound()
        {
            var x = MakeCombatant("X", 4);
            var y = MakeCombatant("Y", 4);

            // A real seeded generator: equal totals re-roll a d12 among the tied pair until one is higher.
            var scheduler = new TurnScheduler(new List<CombatantRuntime> { x, y }, new System.Random(1234));

            var firstRound = new HashSet<CombatantRuntime> { scheduler.AdvanceToNextActor(), scheduler.AdvanceToNextActor() };

            CollectionAssert.AreEquivalent(new[] { x, y }, firstRound);
        }

        [Test]
        public void PreviewUpcomingOrder_ReturnsRequestedCountAndSkipsDefeated()
        {
            var a = MakeCombatant("A", 5);
            var b = MakeCombatant("B", 3);
            var gone = MakeCombatant("Gone", 8);
            gone.currentHp = 0;
            var scheduler = new TurnScheduler(new List<CombatantRuntime> { a, b, gone }, new FlatRolls(6));

            var preview = scheduler.PreviewUpcomingOrder(5);

            Assert.AreEqual(5, preview.Count);
            Assert.IsTrue(preview.All(c => c == a || c == b));
        }
    }
}
