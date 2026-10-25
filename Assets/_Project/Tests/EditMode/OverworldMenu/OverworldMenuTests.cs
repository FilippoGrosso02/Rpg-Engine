using System.Linq;
using Game.Combat.TrailsBattle3D.Runtime;
using Game.Combat.UI.UGUI;
using Game.Data.Registry;
using Game.Data.Rules;
using Game.Data.State;
using Game.Overworld.Menu;
using NUnit.Framework;

namespace Game.Overworld.Menu.Tests.EditMode
{
    public class OverworldMenuTests
    {
        [SetUp]
        public void SetUp() => ResetAll();

        [TearDown]
        public void TearDown() => ResetAll();

        static void ResetAll()
        {
            Inventory.Reset();
            Loadout.Reset();
            Bestiary.Reset();
            PartyState.HealAll();
        }

        static void AssertDrawable(string text, string what)
        {
            foreach (char c in text)
            {
                if (c == ' ' || c == '\n') continue;
                Assert.IsTrue(PixelFont.TryGetUv(c, out _), $"{what}: the pixel font has no glyph for '{c}'.");
            }
        }

        [Test]
        public void Wrap_BreaksAtWordBoundaries_AndKeepsParagraphs()
        {
            var wrapped = MenuText.Wrap("ONE TWO THREE FOUR\nFIVE", 9);

            Assert.AreEqual("ONE TWO\nTHREE\nFOUR\nFIVE", wrapped);
            foreach (var line in wrapped.Split('\n')) Assert.LessOrEqual(line.Length, 9);
        }

        [Test]
        public void Fit_ShortensLongLabels()
        {
            Assert.AreEqual("SWORD", MenuText.Fit("sword", 10));
            Assert.AreEqual("LONG LAB..", MenuText.Fit("Long label here", 10));
        }

        [Test]
        public void RulesPages_OnlyUseCharactersThePixelFontCanDraw()
        {
            foreach (var topic in MenuText.Rules)
            {
                AssertDrawable(topic.title, "title " + topic.title);
                AssertDrawable(topic.body, "body of " + topic.title);
            }
        }

        [Test]
        public void HeroSheet_MatchesTheAllyCombatBuilds()
        {
            DatabaseRegistry.Initialize();
            foreach (var id in new[] { "actor_warrior", "actor_ranger", "actor_wizard" })
            {
                var actor = DatabaseRegistry.GetActor(id);
                var sheet = HeroSheet.Build(actor);
                var combatant = EncounterRunner.BuildAllyRuntime(actor);

                Assert.AreEqual(combatant.maxHp, sheet.maxHp, id + " HP");
                Assert.AreEqual(combatant.ArmorClass, sheet.armorClass, id + " AC");
                Assert.AreEqual(combatant.attackBonus, sheet.attackBonus, id + " attack");
                Assert.AreEqual(combatant.attackDamage.ToString(), sheet.damageText, id + " damage");
            }
        }

        [Test]
        public void HeroSheet_FollowsTheWornGear()
        {
            var warrior = DatabaseRegistry.GetActor("actor_warrior");
            var before = HeroSheet.Build(warrior);
            Inventory.AddWeapon("weapon_dagger");
            Inventory.AddArmor("armor_leather");

            Loadout.EquipWeapon(warrior, "weapon_dagger");
            Loadout.EquipArmor(warrior, "armor_leather");
            var after = HeroSheet.Build(warrior);

            Assert.AreEqual(before.armorClass - 3, after.armorClass, "leather +4 replaces chain mail +7");
            Assert.AreNotEqual(before.damageText, after.damageText);
            Assert.AreEqual(after.damageText, EncounterRunner.BuildAllyRuntime(warrior).attackDamage.ToString(), "combat sees the same weapon");
        }

        [Test]
        public void MonsterPages_AreDrawableAndShowTheKillCount()
        {
            DatabaseRegistry.Initialize();
            foreach (var id in new[] { "enemy_goblin", "enemy_ogre", "enemy_slime" })
            {
                var text = MenuText.Describe(DatabaseRegistry.GetEnemy(id), 3);

                AssertDrawable(text, id);
                StringAssert.Contains("DEFEATED: 3", text);
            }
        }

        [Test]
        public void HeroPages_AreDrawable()
        {
            foreach (var id in new[] { "actor_warrior", "actor_ranger", "actor_wizard" })
            {
                var text = MenuText.Describe(HeroSheet.Build(DatabaseRegistry.GetActor(id)));
                AssertDrawable(text, id);
                Assert.IsTrue(text.Contains("AC "));
            }
        }
    }
}
