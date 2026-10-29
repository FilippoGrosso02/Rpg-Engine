using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    public class TurnSchedulerAdvantageTests
    {
        private class FlatRolls : System.Random
        {
            public override int Next(int minValue, int maxValue) => 6;
        }

        private static CombatantRuntime Make(string name, bool party, int initiative) =>
            new CombatantRuntime { displayName = name, isPlayerSide = party, initiative = initiative, maxHp = 10, currentHp = 10 };

        /// <summary>A fast enemy that would normally open, then a hero, a slower enemy and a slower hero.</summary>
        private static List<CombatantRuntime> Field() => new List<CombatantRuntime>
        {
            Make("FastEnemy", false, 4), Make("HeroA", true, 2), Make("SlowEnemy", false, 0), Make("HeroB", true, -2)
        };

        private static List<string> Take(TurnScheduler scheduler, int count) =>
            Enumerable.Range(0, count).Select(_ => scheduler.AdvanceToNextActor().displayName).ToList();

        [Test]
        public void NoAdvantage_IsTheNormalOrder()
        {
            var scheduler = new TurnScheduler(Field(), new FlatRolls());

            Assert.AreEqual(new[] { "FastEnemy", "HeroA", "SlowEnemy", "HeroB" }, Take(scheduler, 4));
        }

        [Test]
        public void PartyAdvantage_EveryHeroActsFirstThenTheNormalOrderStarts()
        {
            var scheduler = new TurnScheduler(Field(), new FlatRolls(), EncounterAdvantage.Party);

            Assert.AreEqual(new[] { "HeroA", "HeroB", "FastEnemy", "HeroA", "SlowEnemy", "HeroB" }, Take(scheduler, 6));
        }

        [Test]
        public void EnemyAdvantage_EveryEnemyActsFirst()
        {
            var scheduler = new TurnScheduler(Field(), new FlatRolls(), EncounterAdvantage.Enemies);

            Assert.AreEqual(new[] { "FastEnemy", "SlowEnemy", "FastEnemy", "HeroA", "SlowEnemy", "HeroB" }, Take(scheduler, 6));
        }

        [Test]
        public void PreviewShowsTheOpeningRoundAndShrinksAsItIsPlayed()
        {
            var scheduler = new TurnScheduler(Field(), new FlatRolls(), EncounterAdvantage.Party);

            Assert.AreEqual(new[] { "HeroA", "HeroB", "FastEnemy" }, scheduler.PreviewUpcomingOrder(3).Select(c => c.displayName).ToArray());

            scheduler.AdvanceToNextActor();

            Assert.AreEqual(new[] { "HeroB", "FastEnemy", "HeroA" }, scheduler.PreviewUpcomingOrder(3).Select(c => c.displayName).ToArray());
        }

        [Test]
        public void AHeroDefeatedBeforeTheirOpeningTurnIsSkipped()
        {
            var field = Field();
            var scheduler = new TurnScheduler(field, new FlatRolls(), EncounterAdvantage.Party);
            field.First(c => c.displayName == "HeroB").currentHp = 0;

            Assert.AreEqual(new[] { "HeroA", "FastEnemy" }, Take(scheduler, 2));
        }
    }
}
