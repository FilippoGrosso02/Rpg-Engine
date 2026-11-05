using System;
using System.Collections.Generic;
using Game.Data.Rules;
using UnityEngine;

namespace Game.Overworld
{
    public enum PartyMode { Attached, Detached }

    /// <summary>
    /// The overworld party rules that outlive any one component: the party is Attached (followers close, leader can be switched, no
    /// stealth) or Detached (followers far behind, leader locked, the leader may hide). Hidden ends when the party attaches, when
    /// the leader attacks or when an enemy spots them. Session-only, like the roster.
    /// </summary>
    public static class PartyRules
    {
        /// <summary>d10 + DEX against this difficulty hides the leader; equal numbers are a coin flip. Open question 1 of the rules: a fixed DV for now.</summary>
        public const int StealthDifficulty = 7;

        public static PartyMode Mode { get; private set; } = PartyMode.Attached;
        public static bool Hidden { get; private set; }

        public static bool CanSwitchLeader => Mode == PartyMode.Attached;
        public static bool CanStealth => Mode == PartyMode.Detached;

        /// <summary>Raised when the mode or the hidden state changes.</summary>
        public static event Action Changed;

        public static void SetMode(PartyMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            if (mode == PartyMode.Attached) Hidden = false; // attaching ends Hidden at once
            Changed?.Invoke();
        }

        /// <summary>Hides the leader. Refused (false) unless the party is Detached.</summary>
        public static bool BecomeHidden()
        {
            if (!CanStealth) return false;
            if (Hidden) return true;
            Hidden = true;
            Changed?.Invoke();
            return true;
        }

        public static void EndHidden()
        {
            if (!Hidden) return;
            Hidden = false;
            Changed?.Invoke();
        }

        /// <summary>A d10 test: the natural roll plus the stat against a difficulty (a natural 1 always fails, a natural 10 always passes).</summary>
        public static bool Check(int naturalRoll, int stat, int difficulty) => OsrRules.TestPasses(naturalRoll, stat, difficulty);

        /// <summary>Indices of the highest score. Several when they tie (any of them may make the check); empty for no scores.</summary>
        public static List<int> HighestScoreIndices(IReadOnlyList<int> scores)
        {
            var best = new List<int>();
            int highest = int.MinValue;
            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i] > highest)
                {
                    highest = scores[i];
                    best.Clear();
                }

                if (scores[i] == highest) best.Add(i);
            }

            return best;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Mode = PartyMode.Attached;
            Hidden = false;
            Changed = null;
        }
    }
}
