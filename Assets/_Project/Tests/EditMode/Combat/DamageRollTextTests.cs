using Game.Combat.Contracts;
using Game.Combat.UI.UGUI;
using NUnit.Framework;

namespace Game.Combat.Tests.EditMode
{
    /// <summary>The one-line roll readout is how a player learns why an attack hit or missed, so its wording is pinned.</summary>
    public class DamageRollTextTests
    {
        [Test]
        public void Hit_ShowsTheRollAtkDefTotalAndDamage()
        {
            var info = new DamageRollInfo
            {
                attackRoll = 9, attackBonus = 10, targetArmorClass = 8, hit = true, damageText = "d6", damage = 4
            };

            Assert.AreEqual("Attack  d10 9 + ATK 10 − DEF 8 = 11 vs 6 — HIT  d6 = 4 dmg", DamageRollBar.Describe(info));
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
            var info = new DamageRollInfo { attackRoll = 4, attackBonus = 5, targetArmorClass = 9, hit = false };

            Assert.AreEqual("Attack  d10 4 + ATK 5 − DEF 9 = 0 vs 6 — MISS", DamageRollBar.Describe(info));
        }

        [Test]
        public void NaturalTen_IsFlagged()
        {
            var info = new DamageRollInfo
            {
                attackRoll = 10, attackBonus = 2, targetArmorClass = 12, hit = true, naturalTen = true, damageText = "d6", damage = 3
            };

            StringAssert.Contains("natural 10!", DamageRollBar.Describe(info));
        }

        [Test]
        public void CorrodedWeapon_ShowsTheAcidPenalty()
        {
            var info = new DamageRollInfo
            {
                attackRoll = 8, attackBonus = 10, targetArmorClass = 7, hit = true, damageText = "d6", corrosionPenalty = 2, damage = 3
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
            var failed = new DamageRollInfo { spellName = "Web", hit = true, hasSave = true, saveRoll = 6, saveBonus = 7, saveDifficulty = 8, savePassed = false };
            var resisted = new DamageRollInfo { spellName = "Web", hit = true, hasSave = true, saveRoll = 7, saveBonus = 7, saveDifficulty = 8, savePassed = true, effectNegated = true };

            Assert.AreEqual("Web  save d10 6 + 7 − DV 8 = 5 vs 6 — FAILED", DamageRollBar.Describe(failed));
            Assert.AreEqual("Web  save d10 7 + 7 − DV 8 = 6 vs 6 — RESISTED", DamageRollBar.Describe(resisted));
        }

        [Test]
        public void FireballSave_ShowsFullOrHalfDamage()
        {
            var failed = new DamageRollInfo { spellName = "Fireball", hit = true, hasSave = true, damageText = "2d6", saveRoll = 4, saveBonus = 8, saveDifficulty = 7, savePassed = false, damage = 9 };
            var saved = new DamageRollInfo { spellName = "Fireball", hit = true, hasSave = true, damageText = "2d6", saveRoll = 5, saveBonus = 8, saveDifficulty = 7, savePassed = true, halved = true, damage = 4 };

            Assert.AreEqual("Fireball  save d10 4 + 8 − DV 7 = 5 vs 6 — FAILED  2d6: 9 dmg", DamageRollBar.Describe(failed));
            Assert.AreEqual("Fireball  save d10 5 + 8 − DV 7 = 6 vs 6 — SAVED, half  4 dmg", DamageRollBar.Describe(saved));
        }
    }
}
