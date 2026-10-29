using System.Linq;
using Game.Data.Registry;
using Game.Data.State;
using NUnit.Framework;

namespace Game.Data.Tests.EditMode
{
    public class DemoDungeonLogicTests
    {
        const string Warrior = "actor_warrior";
        const string Ranger = "actor_ranger";
        const string Thief = "actor_thief";

        [SetUp]
        public void SetUp()
        {
            DatabaseRegistry.Initialize();
            Reset();
            PartyRoster.Add(Warrior);
            PartyRoster.Add(Ranger);
            PartyRoster.Add(Thief);
        }

        [TearDown]
        public void TearDown() => Reset();

        static void Reset()
        {
            Inventory.Reset();
            Loadout.Reset();
            PartyState.HealAll();
            WorldFlags.Clear();
            PartyRoster.Reset(null);
        }

        static int MaxHp(string actorId) => Game.Data.Rules.HeroSheet.Build(DatabaseRegistry.GetActor(actorId)).maxHp;

        // --- WorldFlags

        [Test]
        public void WorldFlags_RememberFlagsAndCounts()
        {
            Assert.IsFalse(WorldFlags.Has("chest:a"));
            WorldFlags.Set("chest:a");
            Assert.IsTrue(WorldFlags.Has("chest:a"));

            Assert.AreEqual(0, WorldFlags.GetCount("rest:x"));
            Assert.AreEqual(1, WorldFlags.Increment("rest:x"));
            Assert.AreEqual(2, WorldFlags.Increment("rest:x"));

            WorldFlags.Clear();
            Assert.IsFalse(WorldFlags.Has("chest:a"));
            Assert.AreEqual(0, WorldFlags.GetCount("rest:x"));
        }

        // --- LootGrant

        [Test]
        public void LootGrant_PutsItemsAndGearInTheInventory_AndNamesThem()
        {
            var lines = LootGrant.Apply(
                new[] { new LootItem { itemId = "item_healing_potion", count = 2 }, new LootItem { itemId = "item_iron_key", count = 1 } },
                new[] { "weapon_longsword" },
                new[] { "armor_banded_mail" });

            Assert.AreEqual(2, Inventory.GetItemCount("item_healing_potion"));
            Assert.AreEqual(1, Inventory.GetItemCount("item_iron_key"));
            CollectionAssert.Contains(Inventory.SpareWeapons, "weapon_longsword");
            CollectionAssert.Contains(Inventory.SpareArmor, "armor_banded_mail");
            CollectionAssert.AreEqual(new[] { "Healing Potion x2", "Iron Key", "Longsword", "Banded Mail" }, lines);
        }

        [Test]
        public void LootGrant_IgnoresEmptyEntries()
        {
            var lines = LootGrant.Apply(new[] { new LootItem { itemId = "", count = 1 }, new LootItem { itemId = "item_healing_potion", count = 0 } }, null, null);

            Assert.IsEmpty(lines);
            Assert.AreEqual(0, Inventory.GetItemCount("item_healing_potion"));
        }

        [Test]
        public void DemoContent_ExistsInTheDatabase()
        {
            foreach (var id in new[] { "item_healing_potion", "item_iron_key", "item_crypt_sigil" })
                Assert.IsNotNull(DatabaseRegistry.GetItem(id), id);
            foreach (var id in new[] { "weapon_longsword", "weapon_longbow", "weapon_keen_dagger" })
                Assert.IsNotNull(DatabaseRegistry.GetWeapon(id), id);
            foreach (var id in new[] { "armor_banded_mail", "armor_studded_leather" })
                Assert.IsNotNull(DatabaseRegistry.GetArmor(id), id);
            foreach (var id in new[] { "enemy_skeleton", "enemy_bandit_captain", "enemy_ogre_warlord" })
                Assert.IsNotNull(DatabaseRegistry.GetEnemy(id), id);
        }

        // --- PartyCare

        [Test]
        public void HealMostWounded_PicksTheLowestHpFraction_AndNeverOverheals()
        {
            PartyState.SetHp(Warrior, MaxHp(Warrior) - 1);
            PartyState.SetHp(Thief, 1);

            string name = PartyCare.HealMostWounded(100, out int healed);

            Assert.AreEqual(DatabaseRegistry.GetActor(Thief).displayName, name);
            Assert.AreEqual(MaxHp(Thief) - 1, healed);
            Assert.AreEqual(MaxHp(Thief), PartyState.GetStartingHp(Thief, MaxHp(Thief)));
            Assert.AreEqual(MaxHp(Warrior) - 1, PartyState.GetStartingHp(Warrior, MaxHp(Warrior)), "the other hero is untouched");
        }

        [Test]
        public void HealMostWounded_WithNobodyHurt_DoesNothing()
        {
            Assert.IsNull(PartyCare.HealMostWounded(5, out int healed));
            Assert.AreEqual(0, healed);
        }

        [Test]
        public void UseHealingItem_HealsAndConsumesOne()
        {
            PartyState.SetHp(Ranger, 1);
            Inventory.AddItem("item_healing_potion", 2);

            Assert.IsTrue(PartyCare.UseHealingItem("item_healing_potion", out string message));

            Assert.AreEqual(1, Inventory.GetItemCount("item_healing_potion"));
            Assert.AreEqual(System.Math.Min(MaxHp(Ranger), 1 + DatabaseRegistry.GetItem("item_healing_potion").healAmount), PartyState.GetStartingHp(Ranger, MaxHp(Ranger)));
            StringAssert.Contains("recovers", message);
        }

        [Test]
        public void UseHealingItem_KeepsTheItemWhenNobodyIsHurt()
        {
            Inventory.AddItem("item_healing_potion");

            Assert.IsFalse(PartyCare.UseHealingItem("item_healing_potion", out string message));

            Assert.AreEqual(1, Inventory.GetItemCount("item_healing_potion"));
            Assert.AreEqual("Nobody needs healing.", message);
        }

        [Test]
        public void UseHealingItem_RefusesKeysAndMissingItems()
        {
            Inventory.AddItem("item_iron_key");

            Assert.IsFalse(PartyCare.UseHealingItem("item_iron_key", out _));
            Assert.IsFalse(PartyCare.UseHealingItem("item_healing_potion", out _));
            Assert.AreEqual(1, Inventory.GetItemCount("item_iron_key"));
        }

        [Test]
        public void DamageParty_HurtsEveryoneButNeverBelowOne()
        {
            PartyState.SetHp(Thief, 2);

            PartyCare.DamageParty(5);

            Assert.AreEqual(1, PartyState.GetStartingHp(Thief, MaxHp(Thief)));
            foreach (var id in new[] { Warrior, Ranger })
                Assert.AreEqual(System.Math.Max(1, MaxHp(id) - 5), PartyState.GetStartingHp(id, MaxHp(id)), id);
        }
    }
}
