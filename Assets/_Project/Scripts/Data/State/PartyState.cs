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

        // Slime acid: armor AC lost and weapon damage lost, kept until repaired / sharpened (resting heals them too for now).
        static readonly Dictionary<string, (int armor, int weapon)> Corrosion = new Dictionary<string, (int armor, int weapon)>();

        public static (int armor, int weapon) GetCorrosion(string actorId)
        {
            return !string.IsNullOrEmpty(actorId) && Corrosion.TryGetValue(actorId, out var value) ? value : (0, 0);
        }

        public static void SetCorrosion(string actorId, int armor, int weapon)
        {
            if (string.IsNullOrEmpty(actorId)) return;

            if (armor <= 0 && weapon <= 0) Corrosion.Remove(actorId);
            else Corrosion[actorId] = (Mathf.Max(0, armor), Mathf.Max(0, weapon));
        }

        /// <summary>Forget all remembered HP and corrosion, which means everyone is at full HP and in working order again.</summary>
        public static void HealAll()
        {
            Hp.Clear();
            Corrosion.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Hp.Clear();
            Corrosion.Clear();
        }
    }
}
