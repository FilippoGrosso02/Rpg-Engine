using System;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;
using Game.Data.Rules;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Turn order under the d12 rules: at the start of every round each living combatant rolls d12 + its initiative
    /// (a hero's DEX score, a monster's initiative number), and they act highest first. Exact ties re-roll a d12
    /// among just the tied combatants until the order is strict. A new round is built as soon as the previous one
    /// runs out of (still-standing) actors.
    /// </summary>
    public class TurnScheduler
    {
        private readonly List<CombatantRuntime> _combatants;
        private readonly Random _rng;
        private List<CombatantRuntime> _roundOrder = new List<CombatantRuntime>();
        private int _roundIndex;

        public TurnScheduler(List<CombatantRuntime> combatants, Random rng = null)
        {
            _combatants = combatants;
            _rng = rng ?? new Random();
        }

        /// <summary>Returns the next standing combatant to act, rolling a new round whenever the current one is exhausted.
        /// Returns null only when nobody is standing.</summary>
        public CombatantRuntime AdvanceToNextActor()
        {
            while (true)
            {
                while (_roundIndex < _roundOrder.Count)
                {
                    var candidate = _roundOrder[_roundIndex];
                    _roundIndex++;
                    if (!candidate.IsDefeated)
                    {
                        return candidate;
                    }
                }

                if (!BeginNewRound())
                {
                    return null;
                }
            }
        }

        /// <summary>Predicts the next <paramref name="count"/> actors for a turn-order display. Uses the real remainder of the
        /// current round; later rounds have not been rolled yet, so they are shown in initiative-bonus order (display only —
        /// the real order is rolled when each round starts).</summary>
        public List<CombatantRuntime> PreviewUpcomingOrder(int count)
        {
            var result = new List<CombatantRuntime>(count);

            for (int i = _roundIndex; i < _roundOrder.Count && result.Count < count; i++)
            {
                if (!_roundOrder[i].IsDefeated)
                {
                    result.Add(_roundOrder[i]);
                }
            }

            var standing = _combatants.Where(c => !c.IsDefeated).ToList();
            if (result.Count < count && standing.Count > 0)
            {
                var expected = standing.OrderByDescending(c => c.initiative).ToList();
                int safetyLimit = count * 2 + standing.Count + 1;
                while (result.Count < count && safetyLimit-- > 0)
                {
                    foreach (var combatant in expected)
                    {
                        if (result.Count >= count)
                        {
                            break;
                        }

                        result.Add(combatant);
                    }
                }
            }

            return result;
        }

        private bool BeginNewRound()
        {
            var standing = _combatants.Where(c => !c.IsDefeated).ToList();
            if (standing.Count == 0)
            {
                return false;
            }

            _roundOrder = RollOrder(standing);
            _roundIndex = 0;
            return true;
        }

        private List<CombatantRuntime> RollOrder(List<CombatantRuntime> standing)
        {
            var totals = standing.ToDictionary(c => c, c => ActionResolver.RollInitiative(c, _rng));

            var ordered = new List<CombatantRuntime>();
            foreach (var group in standing.GroupBy(c => totals[c]).OrderByDescending(g => g.Key))
            {
                var members = group.ToList();
                ordered.AddRange(members.Count == 1 ? members : BreakTies(members));
            }

            return ordered;
        }

        /// <summary>Rolls a d12 per tied combatant; whoever rolls highest goes first. Any sub-group that ties for the
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
