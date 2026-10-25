using UnityEngine;

namespace Game.Data.Rules
{
    /// <summary>
    /// The d12 OSR rules that are pure arithmetic on numbers, kept in the Data layer so definitions, combat and
    /// tests all agree on them.
    ///
    /// Derived from the template heroes' sheets: the minor modifier is floor((score - 6) / 2), so 10 -> +2, 8 -> +1,
    /// 6 and 7 -> 0, 4 -> -1. A natural 1 always misses (a guaranteed hit shows as 92%, not 100%), and a natural 12
    /// always hits. A save is d12 + score >= 12.
    /// </summary>
    public static class OsrRules
    {
        public const int DieSides = 12;
        public const int BaseArmorClass = 6;
        public const int SaveTarget = 12;
        public const int GuardArmorClassBonus = 2;

        /// <summary>floor((score - 6) / 2). Used for STR damage, DEX armor class and VIT hit points.</summary>
        public static int Modifier(int score)
        {
            return Mathf.FloorToInt((score - 6) / 2f);
        }

        /// <summary>Level-1 hit points: the hit die at its maximum plus the VIT modifier (at least 1).</summary>
        public static int MaxHitPoints(int hitDieSides, int vitality)
        {
            return Mathf.Max(1, hitDieSides + Modifier(vitality));
        }

        /// <summary>6 + armor. (DEX is deliberately left out for now.)</summary>
        public static int ArmorClass(int armorBonus)
        {
            return BaseArmorClass + armorBonus;
        }

        /// <summary>d12 + attack bonus >= AC, except a natural 1 always misses and a natural 12 always hits.</summary>
        public static bool AttackHits(int naturalRoll, int attackBonus, int armorClass)
        {
            if (naturalRoll <= 1)
            {
                return false;
            }

            if (naturalRoll >= DieSides)
            {
                return true;
            }

            return naturalRoll + attackBonus >= armorClass;
        }

        /// <summary>d12 + score >= 12.</summary>
        public static bool SavePasses(int naturalRoll, int saveBonus)
        {
            return naturalRoll + saveBonus >= SaveTarget;
        }
    }
}
