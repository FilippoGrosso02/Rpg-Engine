using System;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Data.Rules;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Turn order under the d10 rules: when the fight starts each combatant rolls d10 + its DEX minor modifier once, and that
    /// order is kept for every round. Ties go to the higher DEX; combatants still tied after that roll a d10 among
    /// themselves until the order is strict. Defeated combatants are skipped as the order repeats.
    /// </summary>
    public class TurnScheduler
    {
        private readonly List<CombatantRuntime> _combatants;
        private readonly Random _rng;
        private List<CombatantRuntime> _order;
        private int _index;

        public TurnScheduler(List<CombatantRuntime> combatants, Random rng = null)
        {
            _combatants = combatants;
            _rng = rng ?? new Random();
        }

        /// <summary>Returns the next standing combatant to act, wrapping into the next round when the order runs out.
        /// Returns null only when nobody is standing.</summary>
        public CombatantRuntime AdvanceToNextActor()
        {
            EnsureOrder();

            for (int checkedCount = 0; checkedCount < _order.Count; checkedCount++)
            {
                var candidate = _order[_index];
                _index = (_index + 1) % _order.Count;
                if (!candidate.IsDefeated)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Predicts the next <paramref name="count"/> actors for a turn-order display. The order is fixed for the whole
        /// fight, so this is exactly who will act, skipping anyone already defeated.</summary>
        public List<CombatantRuntime> PreviewUpcomingOrder(int count)
        {
            EnsureOrder();

            var result = new List<CombatantRuntime>(count);
            if (_order.All(c => c.IsDefeated))
            {
                return result;
            }

            for (int i = 0; result.Count < count; i++)
            {
                var candidate = _order[(_index + i) % _order.Count];
                if (!candidate.IsDefeated)
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

        private void EnsureOrder()
        {
            if (_order != null)
            {
                return;
            }

            _order = RollOrder(_combatants.Where(c => !c.IsDefeated).ToList());
            _index = 0;
        }

        private List<CombatantRuntime> RollOrder(List<CombatantRuntime> standing)
        {
            var totals = standing.ToDictionary(c => c, c => ActionResolver.RollInitiative(c, _rng));

            var ordered = new List<CombatantRuntime>();
            foreach (var group in standing.GroupBy(c => totals[c]).OrderByDescending(g => g.Key))
            {
                foreach (var sameDex in group.GroupBy(c => c.dexterity).OrderByDescending(g => g.Key))
                {
                    var members = sameDex.ToList();
                    ordered.AddRange(members.Count == 1 ? members : BreakTies(members));
                }
            }

            return ordered;
        }

        /// <summary>Rolls a d10 per tied combatant; whoever rolls highest goes first. Any sub-group that ties for the
        /// highest roll re-rolls among just themselves.</summary>
        private List<CombatantRuntime> BreakTies(List<CombatantRuntime> tied)
        {
            var rolls = tied.ToDictionary(c => c, _ => _rng.Next(1, OsrRules.DieSides + 1));
            var highest = rolls.Values.Max();
            var winners = tied.Where(c => rolls[c] == highest).ToList();

            var result = winners.Count == 1 ? winners : BreakTies(winners);
            if (winners.Count < tied.Count)
            {
                var remaining = tied.Except(winners).ToList();
                result = new List<CombatantRuntime>(result);
                result.AddRange(BreakTies(remaining));
            }

            return result;
        }
    }
}
