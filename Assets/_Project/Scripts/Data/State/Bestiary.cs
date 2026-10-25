using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.State
{
    /// <summary>
    /// The grimoire's monster list: an enemy type is entered the first time the party wins a fight against it, with a running kill
    /// count. Session-only; cleared on Play start (no save/load yet).
    /// </summary>
    public static class Bestiary
    {
        static readonly List<string> Order = new List<string>();
        static readonly Dictionary<string, int> Kills = new Dictionary<string, int>();

        /// <summary>Enemy ids in the order they were first defeated.</summary>
        public static IReadOnlyList<string> KnownIds => Order;

        public static bool IsKnown(string enemyId) => !string.IsNullOrEmpty(enemyId) && Kills.ContainsKey(enemyId);

        public static int GetKills(string enemyId) =>
            !string.IsNullOrEmpty(enemyId) && Kills.TryGetValue(enemyId, out int count) ? count : 0;

        public static void RecordDefeat(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return;

            if (!Kills.ContainsKey(enemyId))
            {
                Order.Add(enemyId);
                Kills[enemyId] = 0;
            }

            Kills[enemyId]++;
        }

        public static void Reset()
        {
            Order.Clear();
            Kills.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Reset();
    }
}
