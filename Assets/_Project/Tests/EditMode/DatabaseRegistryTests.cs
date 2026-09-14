using NUnit.Framework;
using UnityEngine.TestTools;
using Game.Data.Definitions;
using Game.Data.Registry;

namespace Game.Data.Tests.EditMode
{
    public class DatabaseRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            // Re-run a fresh load before every test so tests don't depend on execution order
            // or on whatever state a previous test (or lazy first-access) left behind.
            DatabaseRegistry.Initialize();
        }

        [Test]
        public void Initialize_LoadsSampleDataWithoutLoggingErrors()
        {
            // Resources.LoadAll for every definition type, plus the duplicate-id guard, ran in
            // SetUp already. Any LogError (e.g. a duplicate id in the sample data) would fail
            // this test automatically; asserting it explicitly documents the intent.
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GetSkill_KnownSampleId_ResolvesToCorrectAsset()
        {
            var skill = DatabaseRegistry.GetSkill("skill_test_strike");

            Assert.IsNotNull(skill, "Expected sample skill 'skill_test_strike' to resolve.");
            Assert.AreEqual("skill_test_strike", skill.id);
            Assert.AreEqual("Test Strike", skill.displayName);
        }

        [Test]
        public void GetItem_KnownSampleId_ResolvesToCorrectAsset()
        {
            var item = DatabaseRegistry.GetItem("item_test_potion");

            Assert.IsNotNull(item, "Expected sample item 'item_test_potion' to resolve.");
            Assert.AreEqual("item_test_potion", item.id);
        }

        [Test]
        public void GetActor_KnownSampleId_ResolvesToCorrectAsset()
        {
            var actor = DatabaseRegistry.GetActor("actor_test_hero");

            Assert.IsNotNull(actor, "Expected sample actor 'actor_test_hero' to resolve.");
            Assert.AreEqual("actor_test_hero", actor.id);
            Assert.AreEqual("class_test_warrior", actor.classId);
        }

        [Test]
        public void GetWeapon_KnownSampleId_ResolvesToCorrectAsset()
        {
            var weapon = DatabaseRegistry.GetWeapon("weapon_test_sword");

            Assert.IsNotNull(weapon, "Expected sample weapon 'weapon_test_sword' to resolve.");
            Assert.AreEqual("weapon_test_sword", weapon.id);
        }

        [Test]
        public void GetArmor_KnownSampleId_ResolvesToCorrectAsset()
        {
            var armor = DatabaseRegistry.GetArmor("armor_test_shield");

            Assert.IsNotNull(armor, "Expected sample armor 'armor_test_shield' to resolve.");
            Assert.AreEqual("armor_test_shield", armor.id);
        }

        [Test]
        public void GetEnemy_KnownSampleId_ResolvesToCorrectAsset()
        {
            var enemy = DatabaseRegistry.GetEnemy("enemy_test_slime");

            Assert.IsNotNull(enemy, "Expected sample enemy 'enemy_test_slime' to resolve.");
            Assert.AreEqual("enemy_test_slime", enemy.id);
        }

        [Test]
        public void GetStatusEffect_KnownSampleId_ResolvesToCorrectAsset()
        {
            var statusEffect = DatabaseRegistry.GetStatusEffect("status_test_poison");

            Assert.IsNotNull(statusEffect, "Expected sample status effect 'status_test_poison' to resolve.");
            Assert.AreEqual("status_test_poison", statusEffect.id);
        }

        [Test]
        public void GetClass_KnownSampleId_ResolvesToCorrectAsset()
        {
            var classDef = DatabaseRegistry.GetClass("class_test_warrior");

            Assert.IsNotNull(classDef, "Expected sample class 'class_test_warrior' to resolve.");
            Assert.AreEqual("class_test_warrior", classDef.id);
        }

        [Test]
        public void GetGameConfig_ResolvesTheSingleSampleAsset()
        {
            var config = DatabaseRegistry.GetGameConfig();

            Assert.IsNotNull(config, "Expected the sample GameConfigDefinition to resolve.");
            Assert.Contains("actor_test_hero", config.startingActorIds);
        }

        [Test]
        public void GetSkill_UnknownId_ReturnsNullWithoutThrowing()
        {
            SkillDefinition skill = null;

            Assert.DoesNotThrow(() => skill = DatabaseRegistry.GetSkill("skill_does_not_exist"));
            Assert.IsNull(skill);
        }

        [Test]
        public void GetActor_UnknownId_ReturnsNullWithoutThrowing()
        {
            ActorDefinition actor = null;

            Assert.DoesNotThrow(() => actor = DatabaseRegistry.GetActor("actor_does_not_exist"));
            Assert.IsNull(actor);
        }

        [Test]
        public void GetItem_NullOrEmptyId_ReturnsNullWithoutThrowing()
        {
            Assert.DoesNotThrow(() => DatabaseRegistry.GetItem(null));
            Assert.DoesNotThrow(() => DatabaseRegistry.GetItem(string.Empty));
            Assert.IsNull(DatabaseRegistry.GetItem(null));
            Assert.IsNull(DatabaseRegistry.GetItem(string.Empty));
        }
    }
}
