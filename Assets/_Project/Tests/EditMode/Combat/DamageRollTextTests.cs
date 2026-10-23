using Game.Combat.Contracts;
using Game.Combat.UI.UGUI;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    /// <summary>The one-line roll readout is how a player learns why an attack hit or missed, so its wording is pinned.</summary>
    public class DamageRollTextTests
    {
        [Test]
        public void Hit_ShowsTheRollBonusTotalArmorClassAndDamage()
        {
            var info = new DamageRollInfo
            {
                attackRoll = 9, attackBonus = 10, targetArmorClass = 13, hit = true, damageText = "d8+2", damage = 7
            };

            Assert.AreEqual("Attack  d12 9 + 10 = 19 vs AC 13 — HIT  d8+2 = 7 dmg", DamageRollBar.Describe(info));
        }

        [Test]
        public void NaturalOne_SaysWhyItMissed()
        {
            var info = new DamageRollInfo
            {
                attackRoll = 1, attackBonus = 10, targetArmorClass = 5, hit = false, naturalOne = true
            };

            StringAssert.Contains("MISS (natural 1)", DamageRollBar.Describe(info));
        }

        [Test]
        public void PlainMiss_ShowsTheShortfall()
        {
            var info = new DamageRollInfo { attackRoll = 4, attackBonus = 5, targetArmorClass = 14, hit = false };

            Assert.AreEqual("Attack  d12 4 + 5 = 9 vs AC 14 — MISS", DamageRollBar.Describe(info));
        }

        [Test]
        public void NaturalTwelve_IsFlagged()
        {
            var info = new DamageRollInfo
            {
                attackRoll = 12, attackBonus = 0, targetArmorClass = 20, hit = true, naturalTwelve = true, damageText = "d6", damage = 3
            };

            StringAssert.Contains("natural 12!", DamageRollBar.Describe(info));
        }

        [Test]
        public void CorrodedWeapon_ShowsTheAcidPenalty()
        {
            var info = new DamageRollInfo
            {
                attackRoll = 8, attackBonus = 10, targetArmorClass = 7, hit = true, damageText = "d8+2", corrosionPenalty = 2, damage = 3
            };

            StringAssert.Contains("(−2 corroded)", DamageRollBar.Describe(info));
        }

        [Test]
        public void AutoHitSpell_SaysAutoHit()
        {
            var info = new DamageRollInfo { spellName = "Magic Missile", autoHit = true, hit = true, damageText = "d6", damage = 4 };

            Assert.AreEqual("Magic Missile  auto-hit  d6 = 4 dmg", DamageRollBar.Describe(info));
        }

        [Test]
        public void WebSave_ShowsFailedOrResisted()
        {
            var failed = new DamageRollInfo { spellName = "Web", hit = true, hasSave = true, saveRoll = 3, saveBonus = 7, savePassed = false };
            var resisted = new DamageRollInfo { spellName = "Web", hit = true, hasSave = true, saveRoll = 6, saveBonus = 7, savePassed = true, effectNegated = true };

            Assert.AreEqual("Web  save d12 3 + 7 = 10 vs 12 — FAILED", DamageRollBar.Describe(failed));
            Assert.AreEqual("Web  save d12 6 + 7 = 13 vs 12 — RESISTED", DamageRollBar.Describe(resisted));
        }

        [Test]
        public void FireballSave_ShowsFullOrHalfDamage()
        {
            var failed = new DamageRollInfo { spellName = "Fireball", hit = true, hasSave = true, damageText = "2d6", saveRoll = 3, saveBonus = 8, savePassed = false, damage = 9 };
            var saved = new DamageRollInfo { spellName = "Fireball", hit = true, hasSave = true, damageText = "2d6", saveRoll = 4, saveBonus = 8, savePassed = true, halved = true, damage = 4 };

            Assert.AreEqual("Fireball  save d12 3 + 8 = 11 vs 12 — FAILED  2d6: 9 dmg", DamageRollBar.Describe(failed));
            Assert.AreEqual("Fireball  save d12 4 + 8 = 12 vs 12 — SAVED, half  4 dmg", DamageRollBar.Describe(saved));
        }
    }
}
