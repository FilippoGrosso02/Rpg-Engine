using Game.Data.Registry;
using Game.Data.Rules;

namespace Game.Data.State
{
    /// <summary>Overworld HP changes on the current <see cref="PartyRoster"/>: healing items, traps. Works on the remembered HP in <see cref="PartyState"/>.</summary>
    public static class PartyCare
    {
        /// <summary>Heals the roster member with the lowest HP fraction by up to <paramref name="amount"/>. Returns their name, or null if nobody is hurt.</summary>
        public static string HealMostWounded(int amount, out int healed)
        {
            healed = 0;
            if (amount <= 0) return null;

            HeroSheet worst = null;
            foreach (var id in PartyRoster.Members)
            {
                var sheet = HeroSheet.Build(DatabaseRegistry.GetActor(id));
                if (sheet == null || sheet.currentHp >= sheet.maxHp) continue;
                if (worst == null || sheet.currentHp * worst.maxHp < worst.currentHp * sheet.maxHp) worst = sheet;
            }

            if (worst == null) return null;

            int newHp = System.Math.Min(worst.maxHp, worst.currentHp + amount);
            healed = newHp - worst.currentHp;
            PartyState.SetHp(worst.id, newHp);
            Inventory.NotifyChanged();
            return worst.displayName;
        }

        /// <summary>Uses a healing consumable on the most wounded hero. False (item kept) if it is not a healing item, none is carried, or nobody is hurt.</summary>
        public static bool UseHealingItem(string itemId, out string message)
        {
            var item = DatabaseRegistry.GetItem(itemId);
            if (item == null || item.healAmount <= 0)
            {
                message = "That cannot be used here.";
                return false;
            }

            if (Inventory.GetItemCount(itemId) <= 0)
            {
                message = "You have none left.";
                return false;
            }

            string name = HealMostWounded(item.healAmount, out int healed);
            if (name == null)
            {
                message = "Nobody needs healing.";
                return false;
            }

            Inventory.RemoveItem(itemId);
            message = name + " recovers " + healed + " HP.";
            return true;
        }

        /// <summary>Hurts every roster member by <paramref name="amount"/> but never below 1 HP (a trap hurts, it does not kill).</summary>
        public static void DamageParty(int amount)
        {
            if (amount <= 0) return;

            foreach (var id in PartyRoster.Members)
            {
                var sheet = HeroSheet.Build(DatabaseRegistry.GetActor(id));
                if (sheet == null) continue;
                PartyState.SetHp(id, System.Math.Max(1, sheet.currentHp - amount));
            }

            Inventory.NotifyChanged();
        }
    }
}
