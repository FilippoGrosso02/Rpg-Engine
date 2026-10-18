using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.State
{
    /// <summary>
    /// Session-wide HP of party members, carried between fights. Absent id = full HP. Combat reads it when building allies
    /// and writes it back after a won fight; the overworld resets it on defeat or rest. Cleared on Play start (no save/load yet).
    /// </summary>
    public static class PartyState
    {
        static readonly Dictionary<string, int> Hp = new Dictionary<string, int>();

        public static bool TryGetHp(string actorId, out int hp)
        {
            hp = 0;
            return !string.IsNullOrEmpty(actorId) && Hp.TryGetValue(actorId, out hp);
        }

        /// <summary>Current HP to start a fight with: the remembered value clamped to [1, maxHp], or maxHp if none is remembered.</summary>
        public static int GetStartingHp(string actorId, int maxHp)
        {
            return TryGetHp(actorId, out int hp) ? Mathf.Clamp(hp, 1, maxHp) : maxHp;
        }

        /// <summary>Remember HP after a fight. A knocked-out member (0 HP) is stored as 1 so nobody starts the next fight dead.</summary>
        public static void SetHp(string actorId, int hp)
        {
            if (!string.IsNullOrEmpty(actorId)) Hp[actorId] = Mathf.Max(1, hp);
        }

        /// <summary>Forget all remembered HP, which means everyone is at full HP again.</summary>
        public static void HealAll() => Hp.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Hp.Clear();
    }
}
