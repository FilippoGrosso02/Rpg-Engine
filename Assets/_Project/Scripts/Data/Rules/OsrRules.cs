using UnityEngine;

namespace Game.Data.Rules
{
    /// <summary>
    /// The 5-stat d10 OSR rules that are pure arithmetic on numbers, kept in the Data layer so definitions, combat and
    /// tests all agree on them.
    ///
    /// Everything resolves as d10 + stat - target >= 6: an attack is ATK against DEF, a skill test or save is a stat
    /// against a difficulty value (DV). Equal numbers are therefore 50%, every point of difference is 10%, a natural 1
    /// always fails and a natural 10 always succeeds (so the chance is clamped to 10-90%). Stats and DEF live on the
    /// same 2-12 scale with 7 as average.
    /// </summary>
    public static class OsrRules
    {
        public const int DieSides = 10;

        /// <summary>d10 + ATK - DEF (or stat - DV) must reach this.</summary>
        public const int SuccessTarget = 6;

        public const int MinScore = 2;
        public const int MaxScore = 12;
        public const int AverageScore = 7;

        public const int BaseDefense = 2;
        public const int MinDefense = 2;

        /// <summary>Not in the written rules: Guard is the "guard" action's defensive half, kept from the previous system.</summary>
        public const int GuardDefenseBonus = 2;

        /// <summary>
        /// The minor modifier (DEX to DEF for evade classes, hit points, initiative, CHARM): 2 -> -3, 3-4 -> -2, 5-6 -> -1,
        /// 7 -> 0, 8-9 -> +1, 10-11 -> +2, 12 -> +3.
        /// </summary>
        public static int Modifier(int score)
        {
            if (score <= 2) return -3;
            if (score <= 4) return -2;
            if (score <= 6) return -1;
            if (score == 7) return 0;
            if (score <= 9) return 1;
            if (score <= 11) return 2;
            return 3;
        }

        /// <summary>The major modifier, added to skill tests: score - 7.</summary>
        public static int MajorModifier(int score)
        {
            return score - AverageScore;
        }

        /// <summary>Level-1 hit points: the hit die at its maximum plus the VIT minor modifier (at least 1).</summary>
        public static int MaxHitPoints(int hitDieSides, int vitality)
        {
            return Mathf.Max(1, hitDieSides + Modifier(vitality));
        }

        /// <summary>
        /// 2 + armor + shield + (DEX minor modifier, evade classes only), never below 2. <paramref name="armorBonus"/> is the sum of
        /// every worn piece, shield included.
        /// </summary>
        public static int Defense(int armorBonus, int dexterity, bool evadesWithDex)
        {
            int dexBonus = evadesWithDex ? Modifier(dexterity) : 0;
            return Mathf.Max(MinDefense, BaseDefense + armorBonus + dexBonus);
        }

        /// <summary>d10 + ATK - DEF >= 6, except a natural 1 always misses and a natural 10 always hits.</summary>
        public static bool AttackHits(int naturalRoll, int attack, int defense)
        {
            return Succeeds(naturalRoll, attack, defense);
        }

        /// <summary>A skill test or save: d10 + stat - DV >= 6, a natural 1 always fails and a natural 10 always succeeds.</summary>
        public static bool TestPasses(int naturalRoll, int stat, int difficulty)
        {
            return Succeeds(naturalRoll, stat, difficulty);
        }

        /// <summary>Chance (10-90, in whole percent) that the gap between a stat and a target number succeeds.</summary>
        public static int ChancePercent(int stat, int target)
        {
            int successes = 0;
            for (int roll = 1; roll <= DieSides; roll++)
            {
                if (Succeeds(roll, stat, target))
                {
                    successes++;
                }
            }

            return successes * 100 / DieSides;
        }

        /// <summary>Initiative: d10 + the DEX minor modifier. Rolled once at the start of a fight and kept.</summary>
        public static int Initiative(int naturalRoll, int dexterity)
        {
            return naturalRoll + Modifier(dexterity);
        }

        private static bool Succeeds(int naturalRoll, int stat, int target)
        {
            if (naturalRoll <= 1)
            {
                return false;
            }

            if (naturalRoll >= DieSides)
            {
                return true;
            }

            return naturalRoll + stat - target >= SuccessTarget;
        }
    }
}
