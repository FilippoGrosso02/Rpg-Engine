using System.Collections.Generic;
using Game.Data.Registry;
using Game.Data.State;
using NUnit.Framework;
using UnityEngine;

namespace Game.Overworld.Tests.EditMode
{
    public class InteractableTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            DatabaseRegistry.Initialize();
            Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            Interactable.All.Clear();
            Reset();
        }

        static void Reset()
        {
            Inventory.Reset();
            WorldFlags.Clear();
            PartyState.HealAll();
            PartyRoster.Reset(null);
        }

        T Make<T>(Vector3 position, string id) where T : Interactable
        {
            var go = new GameObject(id);
            go.transform.position = position;
            _spawned.Add(go);
            var component = go.AddComponent<T>();
            component.interactionId = id;
            // OnEnable (which registers the object) does not run on AddComponent in EditMode.
            if (!Interactable.All.Contains(component)) Interactable.All.Add(component);
            return component;
        }

        // --- LootContainer

        [Test]
        public void Chest_GivesItsLootOnce()
        {
            var chest = Make<LootContainer>(Vector3.zero, "c1");
            chest.items = new List<LootItem> { new LootItem { itemId = "item_healing_potion", count = 2 } };
            chest.weaponIds = new List<string> { "weapon_longsword" };

            Assert.IsTrue(chest.CanInteract);
            chest.Interact();
            chest.Interact();

            Assert.AreEqual(2, Inventory.GetItemCount("item_healing_potion"), "a second open must not pay out again");
            Assert.AreEqual(1, Inventory.SpareWeapons.Count);
            Assert.IsFalse(chest.CanInteract);
            Assert.IsTrue(WorldFlags.Has("chest:c1"));
        }

        [Test]
        public void Chest_StaysOpenAfterTheSceneIsRebuilt()
        {
            WorldFlags.Set("chest:c2");
            var chest = Make<LootContainer>(Vector3.zero, "c2");
            chest.items = new List<LootItem> { new LootItem { itemId = "item_healing_potion", count = 1 } };

            Assert.IsFalse(chest.CanInteract);
            chest.Interact();
            Assert.AreEqual(0, Inventory.GetItemCount("item_healing_potion"));
        }

        // --- LockedDoor

        [Test]
        public void FindKey_ReturnsAnyAcceptedKeyThePartyCarries()
        {
            var accepted = new[] { "item_iron_key", "item_crypt_sigil" };
            Assert.IsNull(LockedDoor.FindKey(accepted));

            Inventory.AddItem("item_crypt_sigil");
            Assert.AreEqual("item_crypt_sigil", LockedDoor.FindKey(accepted));
        }

        [Test]
        public void Door_StaysShutWithoutAKey()
        {
            var door = Make<LockedDoor>(Vector3.zero, "d1");
            door.acceptedItemIds = new List<string> { "item_iron_key" };

            door.Interact();

            Assert.IsFalse(door.IsUnlocked);
            Assert.IsTrue(door.gameObject.activeSelf);
        }

        [Test]
        public void Door_OpensWithEitherKey_AndUsesItUp()
        {
            var door = Make<LockedDoor>(Vector3.zero, "d2");
            door.acceptedItemIds = new List<string> { "item_iron_key", "item_crypt_sigil" };
            Inventory.AddItem("item_crypt_sigil");

            door.Interact();

            Assert.IsTrue(door.IsUnlocked);
            Assert.IsFalse(door.gameObject.activeSelf);
            Assert.AreEqual(0, Inventory.GetItemCount("item_crypt_sigil"));
            Assert.IsTrue(WorldFlags.Has("door:d2"));
        }

        // --- RestPoint

        [Test]
        public void Shrine_HealsThePartyUntilItsUsesRunOut()
        {
            PartyRoster.Add("actor_warrior");
            PartyState.SetHp("actor_warrior", 1);
            var shrine = Make<RestPoint>(Vector3.zero, "s1");
            shrine.uses = 1;

            Assert.AreEqual(1, shrine.UsesLeft);
            shrine.Interact();

            Assert.IsFalse(PartyState.TryGetHp("actor_warrior", out _), "no remembered HP means full HP");
            Assert.AreEqual(0, shrine.UsesLeft);
            Assert.IsFalse(shrine.CanInteract);

            PartyState.SetHp("actor_warrior", 1);
            shrine.Interact();
            Assert.IsTrue(PartyState.TryGetHp("actor_warrior", out int hp));
            Assert.AreEqual(1, hp, "a spent shrine heals nothing");
        }

        // --- Picking the target

        [Test]
        public void FindNearest_PicksTheClosestUsableOneInRange()
        {
            var far = Make<LootContainer>(new Vector3(10f, 0f, 0f), "far");
            var near = Make<LootContainer>(new Vector3(1.5f, 0f, 0f), "near");
            var mid = Make<LootContainer>(new Vector3(2f, 0f, 0f), "mid");

            Assert.AreSame(near, InteractionSystem.FindNearest(Vector3.zero));

            near.Interact(); // opened chests no longer count
            Assert.AreSame(mid, InteractionSystem.FindNearest(Vector3.zero));
            Assert.IsNull(InteractionSystem.FindNearest(new Vector3(0f, 0f, 50f)));
            Assert.IsNotNull(far);
        }
    }
}
