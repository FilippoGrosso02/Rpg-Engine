using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.State
{
    /// <summary>
    /// Session-only named flags for things the player changed in the dungeon (chest opened, door unlocked, shrine used), so they stay
    /// that way across the scene being suspended for a fight. Cleared on Play start; save/load comes later.
    /// </summary>
    public static class WorldFlags
    {
        static readonly HashSet<string> Flags = new HashSet<string>();
        static readonly Dictionary<string, int> Counters = new Dictionary<string, int>();

        public static bool Has(string flag) => !string.IsNullOrEmpty(flag) && Flags.Contains(flag);

        public static void Set(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) Flags.Add(flag);
        }

        /// <summary>How many times <paramref name="key"/> has been bumped (a shrine's uses, for instance).</summary>
        public static int GetCount(string key) => !string.IsNullOrEmpty(key) && Counters.TryGetValue(key, out int n) ? n : 0;

        public static int Increment(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            Counters.TryGetValue(key, out int n);
            Counters[key] = ++n;
            return n;
        }

        public static void Clear()
        {
            Flags.Clear();
            Counters.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();
    }
}
