using System.Collections.Generic;
using Game.Combat.Contracts;
using Game.Combat.TrailsBattle3D.Runtime;
using Game.Combat.UI.UGUI;
using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Data.Registry;
using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests.EditMode
{
    /// <summary>The testable logic behind the new HUD: hit chances, integer scaling, which cards the carousel draws, button labels, and the pixel font.</summary>
    public class HudLogicTests
    {
        // ---------------------------------------------------------------- hit chance

        private static CombatantRuntime Attacker(int attackBonus)
        {
            return new CombatantRuntime { attackBonus = attackBonus };
        }

        private static CombatantRuntime Target(int armorClass, int save = 5)
        {
            return new CombatantRuntime { baseArmorClass = armorClass, saveVitality = save, saveMind = save };
        }

        private static SkillDefinition BasicAttack()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.usesWeaponDamage = true;
            return skill;
        }

        [TestCase(10, 9, 92)]  // Warrior vs Goblin: only a natural 1 misses
        [TestCase(10, 13, 83)] // Warrior vs Ogre
        [TestCase(7, 13, 58)]  // Wizard's thrown dagger vs Ogre
        [TestCase(5, 14, 33)]  // Goblin vs Warrior
        public void HitChance_ForAWeaponAttack_IsTheShareOfD12RollsThatReachArmorClass(int attackBonus, int armorClass, int expected)
        {
            var skill = BasicAttack();

            Assert.AreEqual(expected, ActionResolver.GetHitChancePercent(Attacker(attackBonus), Target(armorClass), skill));

            Object.DestroyImmediate(skill);
        }

        [Test]
        public void HitChance_ForAnAutoHitSpell_Is100()
        {
            var spell = ScriptableObject.CreateInstance<SkillDefinition>();
            spell.autoHit = true;

            Assert.AreEqual(100, ActionResolver.GetHitChancePercent(Attacker(0), Target(30), spell));

            Object.DestroyImmediate(spell);
        }

        [Test]
        public void HitChance_ForASaveSpell_IsTheChanceTheTargetFailsItsSave()
        {
            var web = ScriptableObject.CreateInstance<SkillDefinition>();
            web.saveType = SaveType.Vitality;

            // d12 + 7 >= 12 passes on 5+, so it fails on 1-4: 4/12 = 33%.
            Assert.AreEqual(33, ActionResolver.GetHitChancePercent(Attacker(0), Target(9, save: 7), web));
            // d12 + 4 >= 12 passes on 8+, so it fails on 1-7: 7/12 = 58%.
            Assert.AreEqual(58, ActionResolver.GetHitChancePercent(Attacker(0), Target(9, save: 4), web));

            Object.DestroyImmediate(web);
        }

        [Test]
        public void HitChance_UsesGuardAndCorrosionLikeTheRealAttackDoes()
        {
            var skill = BasicAttack();
            var target = Target(12);
            int plain = ActionResolver.GetHitChancePercent(Attacker(8), target, skill);

            target.guarding = true;
            int guarded = ActionResolver.GetHitChancePercent(Attacker(8), target, skill);

            Assert.Less(guarded, plain, "Guard raises AC by 2, so the badge must show a lower chance.");

            Object.DestroyImmediate(skill);
        }

        // ---------------------------------------------------------------- integer scaling

        [TestCase(480, 270, 1)]
        [TestCase(960, 540, 2)]
        [TestCase(1280, 720, 2)]   // 2.66x fits, so floor to 2 (never a fractional scale)
        [TestCase(1920, 1080, 4)]
        [TestCase(2560, 1440, 5)]
        [TestCase(3840, 2160, 8)]
        [TestCase(1920, 900, 3)]   // height-limited
        [TestCase(300, 200, 1)]    // smaller than the reference: never below 1
        public void PixelPerfectScaler_PicksTheLargestWholeScaleThatFits(int width, int height, int expected)
        {
            Assert.AreEqual(expected, PixelPerfectScaler.ComputeScale(width, height, 480f, 270f));
        }

        // ---------------------------------------------------------------- carousel

        [TestCase(0, new int[0])]
        [TestCase(1, new[] { 0 })]
        [TestCase(2, new[] { 0, 1 })]
        [TestCase(3, new[] { -1, 0, 1 })]
        [TestCase(4, new[] { -1, 0, 1, 2 })]
        [TestCase(5, new[] { -2, -1, 0, 1, 2 })]
        [TestCase(9, new[] { -2, -1, 0, 1, 2 })]
        public void SpellHand_ShowsAtMostFiveCardsAroundTheSelection(int count, int[] expected)
        {
            CollectionAssert.AreEqual(expected, SpellHandView.OffsetsFor(count));
        }

        // ---------------------------------------------------------------- badge and ribbon

        [TestCase(100, "tag_hit_chance_high")]
        [TestCase(75, "tag_hit_chance_high")]
        [TestCase(74, "tag_hit_chance_neutral")]
        [TestCase(25, "tag_hit_chance_neutral")]
        [TestCase(24, "tag_hit_chance_low")]
        [TestCase(0, "tag_hit_chance_low")]
        public void HitBadge_ChoosesItsFrameByTheSpecsThresholds(int percent, string expected)
        {
            Assert.AreEqual(expected, HitBadgeView.SpriteNameFor(percent));
        }

        [TestCase(MenuState.Root, "")]
        [TestCase(MenuState.Spells, "SPELLS")]
        [TestCase(MenuState.Items, "ITEMS")]
        [TestCase(MenuState.Locked, "ENEMY TURN")]
        public void ModeRibbon_SaysWhoseTurnItIsAndWhichListIsOpen(MenuState state, string expected)
        {
            Assert.AreEqual(expected, ModeRibbonView.TextFor(state));
        }

        // ---------------------------------------------------------------- command bar

        [Test]
        public void CommandBar_LabelsFollowTheSpecsTable()
        {
            CollectionAssert.AreEqual(new[] { "GUARD", "SPELLS", "ITEMS", "FLEE" }, CommandBarView.LabelsFor(MenuState.Root));
            CollectionAssert.AreEqual(new[] { "PREV", "NEXT", "ITEMS", "FLEE" }, CommandBarView.LabelsFor(MenuState.Spells));
            CollectionAssert.AreEqual(new[] { "PREV", "NEXT", "BACK", "FLEE" }, CommandBarView.LabelsFor(MenuState.Items));
            Assert.AreEqual("STAY", CommandBarView.LabelsFor(MenuState.FleeConfirm)[3]);
        }

        [Test]
        public void CommandBar_ButtonsAreDisabledWhereTheSpecSaysSo()
        {
            CollectionAssert.AreEqual(new[] { true, true, false, true }, CommandBarView.EnabledFor(MenuState.Root, hasSpells: true, hasItems: false));
            CollectionAssert.AreEqual(new[] { true, false, true, true }, CommandBarView.EnabledFor(MenuState.Root, hasSpells: false, hasItems: true));
            CollectionAssert.AreEqual(new[] { false, false, false, true }, CommandBarView.EnabledFor(MenuState.FleeConfirm, true, true));
            CollectionAssert.AreEqual(new[] { false, false, false, false }, CommandBarView.EnabledFor(MenuState.Locked, true, true));
        }

        // ---------------------------------------------------------------- font and text

        [Test]
        public void PixelFont_HasEveryCharacterTheHudDraws()
        {
            const string needed = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!?:.,-+%/'()=x<> ";
            foreach (char c in needed)
            {
                if (c == ' ')
                {
                    continue;
                }

                Assert.IsTrue(PixelFont.TryGetUv(c, out _), $"Missing glyph for '{c}'.");
            }

            Assert.IsTrue(PixelFont.TryGetUv('w', out _), "Lower case maps onto the upper-case glyphs.");
        }

        [Test]
        public void PixelFont_MeasuresLinesByTheFixedAdvance()
        {
            Assert.AreEqual(0, PixelFont.MeasureLine(""));
            Assert.AreEqual(5, PixelFont.MeasureLine("A"));
            Assert.AreEqual(11, PixelFont.MeasureLine("AB"));
            Assert.AreEqual(53, PixelFont.MeasureLine("YOUR TURN"));
        }

        [Test]
        public void SpellText_DescribesTheRealSpells()
        {
            DatabaseRegistry.Initialize();

            Assert.AreEqual("NEVER MISSES D6", SpellText.Effect(DatabaseRegistry.GetSkill("skill_magic_missile")));
            Assert.AreEqual("VIT SAVE OR STUCK 2T", SpellText.Effect(DatabaseRegistry.GetSkill("skill_web")));
            Assert.AreEqual("AREA 2D6 VIT SAVE HALF", SpellText.Effect(DatabaseRegistry.GetSkill("skill_fireball")));
            Assert.AreEqual("FIREBALL - AP 3", SpellText.Title(DatabaseRegistry.GetSkill("skill_fireball")));
        }

        // ---------------------------------------------------------------- compact roll text

        [Test]
        public void CompactRollText_FitsTheReadoutAndSaysWhatHappened()
        {
            var hit = new DamageRollInfo { attackRoll = 9, attackBonus = 10, targetArmorClass = 13, hit = true, damageText = "d8+2", damage = 7 };
            Assert.AreEqual("7", DamageRollBar.DescribeCompact(hit));

            var miss = new DamageRollInfo { attackRoll = 1, attackBonus = 10, targetArmorClass = 5, hit = false, naturalOne = true };
            Assert.AreEqual("MISS", DamageRollBar.DescribeCompact(miss));

            Assert.LessOrEqual(PixelFont.MeasureLine(DamageRollBar.DescribeCompact(hit)), 130, "The final number must fit the 130 px readout.");
        }

        [Test]
        public void DiceSumText_AddsTheModifierToTheDice()
        {
            Assert.AreEqual("5+2", DamageRollBar.SumText(new DamageRollInfo { diceTotal = 5, damageBonus = 2 }));
            Assert.AreEqual("5-1", DamageRollBar.SumText(new DamageRollInfo { diceTotal = 5, damageBonus = 0, corrosionPenalty = 1 }));
            Assert.AreEqual("9", DamageRollBar.SumText(new DamageRollInfo { diceTotal = 9 }));
        }
    }
}
