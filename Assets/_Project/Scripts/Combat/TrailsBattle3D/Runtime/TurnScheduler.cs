using System;
using System.Collections.Generic;
using System.Linq;
using Game.Combat.Contracts;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Duelwright turn order: each round, alive combatants act in SPD-descending order.
    /// Equal-SPD ties are broken by rolling 1d6 each, re-rolling among whoever ties for the
    /// highest roll, until a strict order is settled. A new round is built as soon as the
    /// previous one runs out of (still-alive) actors.
    /// </summary>
    public class TurnScheduler
    {
        private readonly List<CombatantRuntime> _combatants;
        private readonly Random _tieBreakRng;

        private List<CombatantRuntime> _roundOrder = new List<CombatantRuntime>();
        private int _roundIndex;

        public TurnScheduler(List<CombatantRuntime> combatants, Random tieBreakRng = null)
        {
            _combatants = combatants;
            _tieBreakRng = tieBreakRng ?? new Random();
        }

        /// <summary>Returns the next alive combatant to act, starting a new SPD-sorted round
        /// whenever the current one is exhausted. Returns null only when nobody is alive.</summary>
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

        /// <summary>Predicts the next <paramref name="count"/> actors for a turn-order display.
        /// Uses the real remainder of the current round, then repeats a freshly-sorted full
        /// round (alive combatants as of now) — exact tie order beyond the current round is
        /// display-only and not guaranteed to match what will actually be rolled later.</summary>
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

            var alive = _combatants.Where(c => !c.IsDefeated).ToList();
            if (result.Count < count && alive.Count > 0)
            {
                var fullRoundOrder = SortBySpeedWithTieBreak(alive);
                int safetyLimit = count * 2 + alive.Count + 1;
                while (result.Count < count && safetyLimit-- > 0)
                {
                    foreach (var combatant in fullRoundOrder)
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
            var alive = _combatants.Where(c => !c.IsDefeated).ToList();
            if (alive.Count == 0)
            {
                return false;
            }

            _roundOrder = SortBySpeedWithTieBreak(alive);
            _roundIndex = 0;
            return true;
        }

        private List<CombatantRuntime> SortBySpeedWithTieBreak(List<CombatantRuntime> alive)
        {
            var ordered = new List<CombatantRuntime>();

            foreach (var speedGroup in alive.GroupBy(c => c.speed).OrderByDescending(g => g.Key))
            {
                var members = speedGroup.ToList();
                ordered.AddRange(members.Count == 1 ? members : BreakTies(members));
            }

            return ordered;
        }

        /// <summary>Rolls 1d6 per tied combatant; whoever rolls highest goes first. Any
        /// sub-group that ties for the highest roll re-rolls among just themselves.</summary>
        private List<CombatantRuntime> BreakTies(List<CombatantRuntime> tied)
        {
            var rolls = tied.ToDictionary(c => c, _ => RollD6());
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

        private int RollD6() => _tieBreakRng.Next(1, 7);
    }
}
