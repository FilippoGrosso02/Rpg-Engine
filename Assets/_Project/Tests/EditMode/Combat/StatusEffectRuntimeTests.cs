using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using Game.Data.Registry;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    public class StatusEffectRuntimeTests
    {
        [SetUp]
        public void SetUp()
        {
            DatabaseRegistry.Initialize();
        }

        [Test]
        public void TryApply_AlwaysHitChance_AddsStatusWithFullDuration()
        {
            var combatant = new CombatantRuntime { currentHp = 20 };

            StatusEffectRuntime.TryApply(combatant, "status_test_poison", 1f);

            Assert.AreEqual(1, combatant.statuses.Count);
            Assert.AreEqual(3, combatant.statuses[0].remainingDuration); // StatusTestPoison.duration
        }

        [Test]
        public void TryApply_ZeroChance_DoesNotAddStatus()
        {
            var combatant = new CombatantRuntime { currentHp = 20 };

            StatusEffectRuntime.TryApply(combatant, "status_test_poison", 0f);

            Assert.AreEqual(0, combatant.statuses.Count);
        }

        [Test]
        public void TryApply_AlreadyActive_RefreshesDurationInsteadOfStacking()
        {
            var combatant = new CombatantRuntime { currentHp = 20 };
            StatusEffectRuntime.TryApply(combatant, "status_test_poison", 1f);
            combatant.statuses[0].remainingDuration = 1;

            StatusEffectRuntime.TryApply(combatant, "status_test_poison", 1f);

            Assert.AreEqual(1, combatant.statuses.Count);
            Assert.AreEqual(3, combatant.statuses[0].remainingDuration);
        }

        [Test]
        public void TickAndExpire_AppliesTickDamageAndRemovesAfterDurationEnds()
        {
            var combatant = new CombatantRuntime { currentHp = 20 };
            StatusEffectRuntime.TryApply(combatant, "status_test_poison", 1f); // duration 3, tickDamage 2

            StatusEffectRuntime.TickAndExpire(combatant);
            Assert.AreEqual(18, combatant.currentHp);
            Assert.AreEqual(1, combatant.statuses.Count);

            StatusEffectRuntime.TickAndExpire(combatant);
            StatusEffectRuntime.TickAndExpire(combatant);

            Assert.AreEqual(14, combatant.currentHp);
            Assert.AreEqual(0, combatant.statuses.Count);
        }

        [Test]
        public void IsActionPrevented_NoPreventingStatus_ReturnsFalse()
        {
            var combatant = new CombatantRuntime { currentHp = 20 };
            StatusEffectRuntime.TryApply(combatant, "status_test_poison", 1f); // preventsAction = false

            Assert.IsFalse(StatusEffectRuntime.IsActionPrevented(combatant));
        }
    }
}
