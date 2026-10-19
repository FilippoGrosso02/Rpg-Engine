using System.Collections.Generic;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>
    /// The party the player starts a new game with: an ordered list of heroes picked from the database
    /// (drag ActorDefinition assets in). At runtime the live party is <c>PartyRoster</c>, which starts from this and
    /// can then gain and lose members.
    /// </summary>
    [CreateAssetMenu(fileName = "PartyRoster", menuName = "Database/Party Roster")]
    public class PartyRosterDefinition : ScriptableObject
    {
        [Tooltip("How many heroes the party can hold (1-5). Can also be changed at runtime with PartyRoster.SetMaxSize.")]
        [Range(1, 5)] public int maxPartySize = 3;

        public List<ActorDefinition> startingMembers = new List<ActorDefinition>();
    }
}
