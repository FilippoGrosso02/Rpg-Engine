using System;
using System.Collections.Generic;
using Game.Data.Registry;

namespace Game.Data.State
{
    [Serializable]
    public class LootItem
    {
        public string itemId;
        public int count = 1;
    }

    /// <summary>Puts a pile of loot into the <see cref="Inventory"/> and describes it for the "Found: ..." message.</summary>
    public static class LootGrant
    {
        public static List<string> Apply(IEnumerable<LootItem> items, IEnumerable<string> weaponIds, IEnumerable<string> armorIds)
        {
            var lines = new List<string>();

            if (items != null)
            {
                foreach (var entry in items)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.itemId) || entry.count <= 0) continue;
                    Inventory.AddItem(entry.itemId, entry.count);
                    var def = DatabaseRegistry.GetItem(entry.itemId);
                    string name = def != null ? def.displayName : entry.itemId;
                    lines.Add(entry.count > 1 ? name + " x" + entry.count : name);
                }
            }

            if (weaponIds != null)
            {
                foreach (var id in weaponIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    Inventory.AddWeapon(id);
                    var def = DatabaseRegistry.GetWeapon(id);
                    lines.Add(def != null ? def.displayName : id);
                }
            }

            if (armorIds != null)
            {
                foreach (var id in armorIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    Inventory.AddArmor(id);
                    var def = DatabaseRegistry.GetArmor(id);
                    lines.Add(def != null ? def.displayName : id);
                }
            }

            return lines;
        }
    }
}
