using System.Collections.Generic;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Registry;
using UnityEngine;

namespace Game.Data.State
{
    /// <summary>
    /// The player's live party: an ordered list of hero ids from the database. Starts from a <see cref="PartyRosterDefinition"/>
    /// asset and can then be changed freely with <see cref="Add"/> / <see cref="Remove"/>. Combat spawns allies from this
    /// (in this order) unless an encounter forces its own party. Cleared on Play start (no save/load yet).
    /// </summary>
    public static class PartyRoster
    {
        /// <summary>Hard ceiling: the battle arena has five party spawn points, so more than this would overlap on screen.</summary>
        public const int AbsoluteMaxSize = 5;

        /// <summary>Party size limit used when no roster asset says otherwise.</summary>
        public const int DefaultMaxSize = 3;

        static int s_maxSize = DefaultMaxSize;

        /// <summary>The current party size limit (1 to <see cref="AbsoluteMaxSize"/>). Change it with <see cref="SetMaxSize"/>.</summary>
        public static int MaxSize => s_maxSize;

        static readonly List<string> Ids = new List<string>();
        static bool s_initialized;

        public static IReadOnlyList<string> Members => Ids;
        public static int Count => Ids.Count;
        public static bool Contains(string actorId) => Ids.Contains(actorId);

        /// <summary>Raised after any change to the roster (add, remove, reset).</summary>
        public static event System.Action Changed;

        /// <summary>Loads the starting party the first time it's called in a session; later calls do nothing, so a scene reload can't undo roster changes.</summary>
        public static void EnsureInitialized(PartyRosterDefinition startingRoster)
        {
            if (s_initialized)
            {
                return;
            }

            Reset(startingRoster);
        }

        /// <summary>Replaces the roster with the given definition's members (unknown, duplicate or surplus entries are skipped).</summary>
        public static void Reset(PartyRosterDefinition startingRoster)
        {
            s_initialized = true;
            Ids.Clear();
            s_maxSize = startingRoster != null ? ClampMaxSize(startingRoster.maxPartySize) : DefaultMaxSize;

            if (startingRoster != null)
            {
                foreach (var member in startingRoster.startingMembers)
                {
                    if (member != null)
                    {
                        TryAddInternal(member.id);
                    }
                }
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Sets how many heroes the party may hold, clamped to 1..<see cref="AbsoluteMaxSize"/>. If the party is already bigger,
        /// the members at the end of the order are dropped (their remembered HP is kept).
        /// </summary>
        public static void SetMaxSize(int maxSize)
        {
            s_maxSize = ClampMaxSize(maxSize);
            if (Ids.Count > s_maxSize)
            {
                Ids.RemoveRange(s_maxSize, Ids.Count - s_maxSize);
            }

            Changed?.Invoke();
        }

        static int ClampMaxSize(int maxSize) => Mathf.Clamp(maxSize, 1, AbsoluteMaxSize);

        /// <summary>Adds a hero from the database to the end of the party. False if the id is unknown, already in the party, or the party is full.</summary>
        public static bool Add(string actorId)
        {
            if (!TryAddInternal(actorId))
            {
                return false;
            }

            s_initialized = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes a hero from the party. False if they weren't in it. Their remembered HP is kept for if they rejoin.</summary>
        public static bool Remove(string actorId)
        {
            if (!Ids.Remove(actorId))
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        static bool TryAddInternal(string actorId)
        {
            if (string.IsNullOrEmpty(actorId) || Ids.Count >= s_maxSize || Ids.Contains(actorId))
            {
                return false;
            }

            if (!DatabaseRegistry.GetAllActors().Any(a => a.id == actorId))
            {
                Debug.LogWarning($"PartyRoster: no hero with id '{actorId}' in the database.");
                return false;
            }

            Ids.Add(actorId);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Ids.Clear();
            s_initialized = false;
            s_maxSize = DefaultMaxSize;
            Changed = null;
        }
    }
}
