using System.Linq;
using Game.Data.Enums;
using Game.Data.Registry;
using Game.Data.State;
using NUnit.Framework;

namespace Game.Data.Tests.EditMode
{
    public class InventoryLoadoutTests
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
        }

        [Test]
        public void Items_StackAndKeepTheirPickupOrder()
        {
            Inventory.AddItem("potion", 2);
            Inventory.AddItem("elixir");
            Inventory.AddItem("potion");

            Assert.AreEqual(3, Inventory.GetItemCount("potion"));
            CollectionAssert.AreEqual(new[] { "potion", "elixir" }, Inventory.ItemIds);
        }

        [Test]
        public void RemoveItem_RefusesWhenThereAreNotEnough_AndDropsEmptyStacks()
        {
            Inventory.AddItem("potion", 2);

            Assert.IsFalse(Inventory.RemoveItem("potion", 3));
            Assert.AreEqual(2, Inventory.GetItemCount("potion"));

            Assert.IsTrue(Inventory.RemoveItem("potion", 2));
            CollectionAssert.IsEmpty(Inventory.ItemIds);
        }

        [Test]
        public void Seeding_OnlyHappensOncePerSession()
        {
            var items = new[] { ("potion", 1) };
            Inventory.EnsureSeeded(items, null, null);
            Inventory.EnsureSeeded(items, null, null);

            Assert.AreEqual(1, Inventory.GetItemCount("potion"));
        }

        [Test]
        public void Loadout_DefaultsToTheActorsStartingGear()
        {
            var warrior = DatabaseRegistry.GetActor("actor_warrior");

            Assert.AreEqual(warrior.startingWeaponId, Loadout.GetWeaponId(warrior));
            CollectionAssert.AreEquivalent(warrior.startingArmorIds, Loadout.GetArmorIds(warrior).ToList());
        }

        [Test]
        public void EquipWeapon_SwapsWithTheInventory()
        {
            var warrior = DatabaseRegistry.GetActor("actor_warrior");
            var original = warrior.startingWeaponId;
            Inventory.AddWeapon("weapon_dagger");

            Assert.IsTrue(Loadout.EquipWeapon(warrior, "weapon_dagger"));

            Assert.AreEqual("weapon_dagger", Loadout.GetWeaponId(warrior));
            CollectionAssert.AreEqual(new[] { original }, Inventory.SpareWeapons);
        }

        [Test]
        public void EquipWeapon_FailsForAWeaponThatIsNotInTheInventory()
        {
            var warrior = DatabaseRegistry.GetActor("actor_warrior");

            Assert.IsFalse(Loadout.EquipWeapon(warrior, "weapon_dagger"));
            Assert.AreEqual(warrior.startingWeaponId, Loadout.GetWeaponId(warrior));
        }

        [Test]
        public void EquipArmor_ReplacesOnlyThePieceInTheSameSlot()
        {
            var warrior = DatabaseRegistry.GetActor("actor_warrior");
            Inventory.AddArmor("armor_leather");

            Assert.IsTrue(Loadout.EquipArmor(warrior, "armor_leather"));

            Assert.AreEqual("armor_leather", Loadout.GetArmorIdInSlot(warrior, EquipmentSlot.Body));
            Assert.AreEqual("armor_shield", Loadout.GetArmorIdInSlot(warrior, EquipmentSlot.OffHand), "the shield stays on");
            CollectionAssert.AreEqual(new[] { "armor_chain_mail" }, Inventory.SpareArmor);
        }

        [Test]
        public void UnequipArmor_ReturnsItToTheInventory()
        {
            var warrior = DatabaseRegistry.GetActor("actor_warrior");

            Assert.IsTrue(Loadout.UnequipArmor(warrior, EquipmentSlot.OffHand));

            Assert.IsNull(Loadout.GetArmorIdInSlot(warrior, EquipmentSlot.OffHand));
            CollectionAssert.AreEqual(new[] { "armor_shield" }, Inventory.SpareArmor);
        }

        [Test]
        public void Bestiary_RecordsFirstKillsInOrder_AndCountsRepeats()
        {
            Bestiary.RecordDefeat("enemy_goblin");
            Bestiary.RecordDefeat("enemy_slime");
            Bestiary.RecordDefeat("enemy_goblin");

            CollectionAssert.AreEqual(new[] { "enemy_goblin", "enemy_slime" }, Bestiary.KnownIds);
            Assert.AreEqual(2, Bestiary.GetKills("enemy_goblin"));
            Assert.IsFalse(Bestiary.IsKnown("enemy_ogre"));
        }
    }
}
