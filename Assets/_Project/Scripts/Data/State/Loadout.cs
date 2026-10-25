using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Enums;
using Game.Data.Registry;
using UnityEngine;

namespace Game.Data.State
{
    /// <summary>
    /// The weapon and armor each hero is wearing right now. A hero with no entry wears what its ActorDefinition starts with; once the
    /// player changes anything the whole kit is remembered here. Swapping gear moves the old piece to <see cref="Inventory"/> and takes
    /// the new one from it, so nothing is duplicated or lost. Session-only; cleared on Play start.
    /// </summary>
    public static class Loadout
    {
        class Worn
        {
            public string weaponId;
            public readonly List<string> armorIds = new List<string>();
        }

        static readonly Dictionary<string, Worn> Kits = new Dictionary<string, Worn>();

        public static string GetWeaponId(ActorDefinition actor)
        {
            if (actor == null) return null;
            return Kits.TryGetValue(actor.id, out var worn) ? worn.weaponId : actor.startingWeaponId;
        }

        public static IReadOnlyList<string> GetArmorIds(ActorDefinition actor)
        {
            if (actor == null) return new List<string>();
            return Kits.TryGetValue(actor.id, out var worn) ? worn.armorIds : actor.startingArmorIds;
        }

        /// <summary>The armor piece worn in <paramref name="slot"/> (Body, OffHand...), or null.</summary>
        public static string GetArmorIdInSlot(ActorDefinition actor, EquipmentSlot slot)
        {
            foreach (var id in GetArmorIds(actor))
            {
                var armor = DatabaseRegistry.GetArmor(id);
                if (armor != null && armor.equipSlot == slot) return id;
            }

            return null;
        }

        /// <summary>Wields a spare weapon (null = go bare-handed). The old weapon goes back to the inventory. False if the weapon is not in the inventory.</summary>
        public static bool EquipWeapon(ActorDefinition actor, string weaponId)
        {
            if (actor == null) return false;

            var worn = KitFor(actor);
            if (!string.IsNullOrEmpty(weaponId))
            {
                if (!Inventory.TakeWeapon(weaponId)) return false;
            }

            if (!string.IsNullOrEmpty(worn.weaponId)) Inventory.AddWeapon(worn.weaponId);
            worn.weaponId = string.IsNullOrEmpty(weaponId) ? null : weaponId;
            Inventory.NotifyChanged();
            return true;
        }

        /// <summary>Wears a spare armor piece in its slot, returning whatever was in that slot to the inventory. False if it is not in the inventory.</summary>
        public static bool EquipArmor(ActorDefinition actor, string armorId)
        {
            var armor = DatabaseRegistry.GetArmor(armorId);
            if (actor == null || armor == null || !Inventory.TakeArmor(armorId)) return false;

            var worn = KitFor(actor);
            var old = GetArmorIdInSlot(actor, armor.equipSlot);
            if (old != null)
            {
                worn.armorIds.Remove(old);
                Inventory.AddArmor(old);
            }

            worn.armorIds.Add(armorId);
            Inventory.NotifyChanged();
            return true;
        }

        /// <summary>Takes off whatever is worn in an armor slot and puts it in the inventory.</summary>
        public static bool UnequipArmor(ActorDefinition actor, EquipmentSlot slot)
        {
            if (actor == null) return false;

            var old = GetArmorIdInSlot(actor, slot);
            if (old == null) return false;

            KitFor(actor).armorIds.Remove(old);
            Inventory.AddArmor(old);
            Inventory.NotifyChanged();
            return true;
        }

        /// <summary>Copies the starting gear into an explicit kit the first time a hero's gear is touched.</summary>
        static Worn KitFor(ActorDefinition actor)
        {
            if (!Kits.TryGetValue(actor.id, out var worn))
            {
                worn = new Worn { weaponId = actor.startingWeaponId };
                worn.armorIds.AddRange(actor.startingArmorIds);
                Kits[actor.id] = worn;
            }

            return worn;
        }

        public static void Reset() => Kits.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Kits.Clear();
    }
}
