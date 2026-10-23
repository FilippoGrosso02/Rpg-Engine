using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>The outcome of one monster's morale check, for logging or UI.</summary>
    public class MoraleCheck
    {
        public CombatantRuntime monster;
        public string trigger;
        public int roll;
        public int morale;
        public bool fled;
    }

    /// <summary>
    /// Monster morale: a 2d6 roll higher than the monster's morale number sends it fleeing. Only monsters check (the
    /// player's heroes never do), and a monster with morale 0 never checks (mindless). A monster rolls when one of these
    /// happens for the first time:
    /// the first casualty falls on its side, half its side is down, or it is the last one standing and at half HP or less.
    /// Each trigger fires at most once per monster, win or lose, and several triggers at once are a single roll.
    /// </summary>
    public static class MoraleRuntime
    {
        public static List<MoraleCheck> CheckSide(IEnumerable<CombatantRuntime> combatants, System.Random rng = null)
        {
            rng ??= new System.Random();
            var all = combatants.ToList();
            var results = new List<MoraleCheck>();

            var monsters = all.Where(c => !c.isPlayerSide).ToList();
            if (monsters.Count == 0)
            {
                return results;
            }

            int down = monsters.Count(c => c.IsDefeated);
            int standing = monsters.Count - down;
            bool anySlain = monsters.Any(c => c.IsSlain);

            foreach (var monster in monsters)
            {
                if (monster.IsDefeated || monster.morale <= 0)
                {
                    continue;
                }

                string trigger = null;

                if (anySlain && !monster.moraleCheckedFirstCasualty)
                {
                    monster.moraleCheckedFirstCasualty = true;
                    trigger = "first casualty";
                }

                if (down * 2 >= monsters.Count && !monster.moraleCheckedHalfDown)
                {
                    monster.moraleCheckedHalfDown = true;
                    trigger = trigger != null ? trigger + " and half the group down" : "half the group down";
                }

                if (standing == 1 && monster.currentHp * 2 <= monster.maxHp && !monster.moraleCheckedHalfHp)
                {
                    monster.moraleCheckedHalfHp = true;
                    trigger = trigger != null ? trigger + " and half HP alone" : "half HP alone";
                }

                if (trigger == null)
                {
                    continue;
                }

                int roll = rng.Next(1, 7) + rng.Next(1, 7);
                bool fled = roll > monster.morale;
                if (fled)
                {
                    monster.hasFled = true;
                }

                results.Add(new MoraleCheck { monster = monster, trigger = trigger, roll = roll, morale = monster.morale, fled = fled });
            }

            return results;
        }
    }
}
