using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    public class TurnSchedulerTests
    {
        /// <summary>Every d10 comes up the same number, so initiative order is decided purely by the bonuses.</summary>
        private class FlatRolls : System.Random
        {
            private readonly int _value;
            public FlatRolls(int value) => _value = value;
            public override int Next(int minValue, int maxValue) => _value;
        }

        /// <summary>initiative is the DEX minor modifier added to the d10; dexterity only breaks ties.</summary>
        private static CombatantRuntime MakeCombatant(string name, int initiative, int dexterity = 0)
        {
            return new CombatantRuntime { displayName = name, initiative = initiative, dexterity = dexterity, maxHp = 10, currentHp = 10 };
        }

        [Test]
        public void AdvanceToNextActor_OrdersByInitiativeDescending()
        {
            var fast = MakeCombatant("Fast", 2);
            var mid = MakeCombatant("Mid", 0);
            var slow = MakeCombatant("Slow", -2);
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
        public void AdvanceToNextActor_TheD10CanOutweighASmallInitiativeEdge()
        {
            // Both roll via a script: the one with the lower bonus rolls much higher.
            var scripted = new ScriptedRolls(1, 10); // a: d10 = 1 (+2 = 3); b: d10 = 10 (+0 = 10)
            var a = MakeCombatant("A", 2);
            var b = MakeCombatant("B", 0);

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
        public void AdvanceToNextActor_KeepsTheSameOrderEveryRound_RollingInitiativeOnlyOnce()
        {
            var a = MakeCombatant("A", 1);
            var b = MakeCombatant("B", -1);

            // Only two rolls are scripted: a second round would run out of dice if initiative were re-rolled.
            var scheduler = new TurnScheduler(new List<CombatantRuntime> { a, b }, new ScriptedRolls(5, 5));

            var order = new List<CombatantRuntime>();
            for (int i = 0; i < 6; i++)
            {
                order.Add(scheduler.AdvanceToNextActor());
            }

            CollectionAssert.AreEqual(new[] { a, b, a, b, a, b }, order);
        }

        [Test]
        public void AdvanceToNextActor_TiesGoToTheHigherDexterity()
        {
            var slowerDex = MakeCombatant("Low", 1, dexterity: 7);
            var higherDex = MakeCombatant("High", 1, dexterity: 9);

            var scheduler = new TurnScheduler(new List<CombatantRuntime> { slowerDex, higherDex }, new FlatRolls(6));

            Assert.AreEqual(higherDex, scheduler.AdvanceToNextActor());
            Assert.AreEqual(slowerDex, scheduler.AdvanceToNextActor());
        }

        [Test]
        public void AdvanceToNextActor_SkipsDefeatedCombatants()
        {
            var a = MakeCombatant("A", 2);
            var b = MakeCombatant("B", -1);
            b.currentHp = 0; // defeated before the round even starts

            var scheduler = new TurnScheduler(new List<CombatantRuntime> { a, b }, new FlatRolls(6));

            Assert.AreEqual(a, scheduler.AdvanceToNextActor());
            Assert.AreEqual(a, scheduler.AdvanceToNextActor()); // next round: only A is standing
        }

        [Test]
        public void AdvanceToNextActor_SkipsMonstersThatFled()
        {
            var hero = MakeCombatant("Hero", 2);
            var goblin = MakeCombatant("Goblin", 0);
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
            var x = MakeCombatant("X", 0, dexterity: 7);
            var y = MakeCombatant("Y", 0, dexterity: 7);

            // A real seeded generator: equal totals and equal DEX re-roll a d10 among the tied pair until one is higher.
            var scheduler = new TurnScheduler(new List<CombatantRuntime> { x, y }, new System.Random(1234));

            var firstRound = new HashSet<CombatantRuntime> { scheduler.AdvanceToNextActor(), scheduler.AdvanceToNextActor() };

            CollectionAssert.AreEquivalent(new[] { x, y }, firstRound);
        }

        [Test]
        public void PreviewUpcomingOrder_ReturnsRequestedCountAndSkipsDefeated()
        {
            var a = MakeCombatant("A", 1);
            var b = MakeCombatant("B", -1);
            var gone = MakeCombatant("Gone", 3);
            gone.currentHp = 0;
            var scheduler = new TurnScheduler(new List<CombatantRuntime> { a, b, gone }, new FlatRolls(6));

            var preview = scheduler.PreviewUpcomingOrder(5);

            Assert.AreEqual(5, preview.Count);
            Assert.IsTrue(preview.All(c => c == a || c == b));
        }

        [Test]
        public void PreviewUpcomingOrder_IsExactlyTheOrderThatActs()
        {
            var a = MakeCombatant("A", 2);
            var b = MakeCombatant("B", 0);
            var c = MakeCombatant("C", -2);
            var scheduler = new TurnScheduler(new List<CombatantRuntime> { c, a, b }, new FlatRolls(6));

            var preview = scheduler.PreviewUpcomingOrder(5);

            var actual = new List<CombatantRuntime>();
            for (int i = 0; i < 5; i++)
            {
                actual.Add(scheduler.AdvanceToNextActor());
            }

            CollectionAssert.AreEqual(actual, preview);
        }
    }
}
