using System.Collections.Generic;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using Game.Data.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests.EditMode
{
    public class ReviveTests
    {
        private static SkillDefinition MakeRevive(int reviveHp)
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.isRevive = true;
            skill.reviveHp = reviveHp;
            return skill;
        }

        private static CombatantRuntime Ally(int hp, int maxHp = 20, int speed = 5) => new CombatantRuntime
        {
            definitionId = "ally",
            isPlayerSide = true,
            maxHp = maxHp,
            currentHp = hp,
            speed = speed
        };

        [Test]
        public void Revive_BringsDefeatedAllyBackAtReviveHp_AndClearsStateFlags()
        {
            var fallen = Ally(0);
            fallen.fear = 8;
            fallen.guarding = true;
            fallen.statuses.Add(new StatusEffectInstance());

            Assert.IsTrue(ActionResolver.ApplyRevive(fallen, MakeRevive(5)));

            Assert.IsFalse(fallen.IsDefeated);
            Assert.AreEqual(5, fallen.currentHp);
            Assert.AreEqual(0, fallen.fear);
            Assert.IsFalse(fallen.guarding);
            Assert.IsEmpty(fallen.statuses);
        }

        [Test]
        public void Revive_OnLivingTarget_ChangesNothing()
        {
            var alive = Ally(7);
            Assert.IsFalse(ActionResolver.ApplyRevive(alive, MakeRevive(5)));
            Assert.AreEqual(7, alive.currentHp);
        }

        [Test]
        public void Revive_HpIsClampedBetweenOneAndMaxHp()
        {
            var tooLow = Ally(0);
            ActionResolver.ApplyRevive(tooLow, MakeRevive(0));
            Assert.AreEqual(1, tooLow.currentHp);

            var tooHigh = Ally(0, maxHp: 8);
            ActionResolver.ApplyRevive(tooHigh, MakeRevive(50));
            Assert.AreEqual(8, tooHigh.currentHp);
        }

        [Test]
        public void RevivedAlly_ActsAgainInTheNextRound()
        {
            var a = Ally(10, speed: 9);
            var b = Ally(10, speed: 5);
            var scheduler = new TurnScheduler(new List<CombatantRuntime> { a, b }, new System.Random(1));

            Assert.AreSame(a, scheduler.AdvanceToNextActor());   // round 1: A acts
            b.currentHp = 0;                                      // B is knocked out before acting
            Assert.AreSame(a, scheduler.AdvanceToNextActor());   // round 2 contains only A

            ActionResolver.ApplyRevive(b, MakeRevive(5));

            var next = new List<CombatantRuntime> { scheduler.AdvanceToNextActor(), scheduler.AdvanceToNextActor() };
            CollectionAssert.Contains(next, b, "A revived ally should be back in the turn order.");
        }
    }
}
