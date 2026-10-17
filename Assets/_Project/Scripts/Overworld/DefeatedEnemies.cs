using System.Collections.Generic;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>Session-only record of overworld enemies the player has beaten (cleared on Play start; save/load comes later).</summary>
    public static class DefeatedEnemies
    {
        static readonly HashSet<string> Ids = new HashSet<string>();

        public static bool Contains(string enemyId) => !string.IsNullOrEmpty(enemyId) && Ids.Contains(enemyId);

        public static void Add(string enemyId)
        {
            if (!string.IsNullOrEmpty(enemyId)) Ids.Add(enemyId);
        }

        public static void Clear() => Ids.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Ids.Clear();
    }
}
