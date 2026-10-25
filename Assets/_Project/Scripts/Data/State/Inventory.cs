using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.State
{
    /// <summary>
    /// What the party carries: consumable items with counts, plus spare weapons and armor that nobody is wearing (worn gear lives in
    /// <see cref="Loadout"/>). Session-only like PartyState: cleared on Play start, no save/load yet.
    /// </summary>
    public static class Inventory
    {
        static readonly List<string> ItemOrder = new List<string>();
        static readonly Dictionary<string, int> ItemCounts = new Dictionary<string, int>();
        static readonly List<string> Weapons = new List<string>();
        static readonly List<string> Armor = new List<string>();
        static bool s_seeded;

        /// <summary>Raised whenever anything in the inventory (or the worn gear in Loadout) changes, so open menus can redraw.</summary>
        public static event System.Action Changed;

        /// <summary>Item ids in the order they were first picked up.</summary>
        public static IReadOnlyList<string> ItemIds => ItemOrder;

        public static IReadOnlyList<string> SpareWeapons => Weapons;

        public static IReadOnlyList<string> SpareArmor => Armor;

        public static int GetItemCount(string itemId) =>
            !string.IsNullOrEmpty(itemId) && ItemCounts.TryGetValue(itemId, out int count) ? count : 0;

        public static void AddItem(string itemId, int count = 1)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return;

            if (!ItemCounts.ContainsKey(itemId))
            {
                ItemOrder.Add(itemId);
                ItemCounts[itemId] = 0;
            }

            ItemCounts[itemId] += count;
            Changed?.Invoke();
        }

        /// <summary>Takes <paramref name="count"/> of an item; false (and nothing taken) when there are not enough.</summary>
        public static bool RemoveItem(string itemId, int count = 1)
        {
            if (count <= 0 || GetItemCount(itemId) < count) return false;

            ItemCounts[itemId] -= count;
            if (ItemCounts[itemId] == 0)
            {
                ItemCounts.Remove(itemId);
                ItemOrder.Remove(itemId);
            }

            Changed?.Invoke();
            return true;
        }

        public static void AddWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return;
            Weapons.Add(weaponId);
            Changed?.Invoke();
        }

        public static void AddArmor(string armorId)
        {
            if (string.IsNullOrEmpty(armorId)) return;
            Armor.Add(armorId);
            Changed?.Invoke();
        }

        internal static bool TakeWeapon(string weaponId) => Weapons.Remove(weaponId);

        internal static bool TakeArmor(string armorId) => Armor.Remove(armorId);

        /// <summary>Fills the starting stock once per session, so reloading a scene does not hand it out again.</summary>
        public static void EnsureSeeded(IEnumerable<(string id, int count)> items, IEnumerable<string> weapons, IEnumerable<string> armor)
        {
            if (s_seeded) return;
            s_seeded = true;

            if (items != null) foreach (var (id, count) in items) AddItem(id, count);
            if (weapons != null) foreach (var id in weapons) AddWeapon(id);
            if (armor != null) foreach (var id in armor) AddArmor(id);
        }

        /// <summary>Empties everything, including the seeded flag (tests and a new game).</summary>
        public static void Reset()
        {
            ItemOrder.Clear();
            ItemCounts.Clear();
            Weapons.Clear();
            Armor.Clear();
            s_seeded = false;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            ItemOrder.Clear();
            ItemCounts.Clear();
            Weapons.Clear();
            Armor.Clear();
            s_seeded = false;
            Changed = null;
        }

        internal static void NotifyChanged() => Changed?.Invoke();
    }
}
