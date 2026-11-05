using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.Contracts
{
    /// <summary>
    /// Declares who fights whom. Ids are resolved against DatabaseRegistry by the runner —
    /// this asset never holds direct references to ActorDefinition/EnemyDefinition so it
    /// stays a plain, data-only description of an encounter.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEncounter", menuName = "Combat/Encounter Definition")]
    public class EncounterDefinition : ScriptableObject
    {
        [Tooltip("Off (default): the fight uses the player's current PartyRoster once one exists. On: always use partyActorIds below (scripted fights, tests).")]
        public bool useFixedParty;

        [Tooltip("Which arena scene a fight in this encounter loads when fights do not play out on the overworld: 'Cave', 'Crypt', 'Lair' (the scene BattleArena_<name>). Empty = the default BattleArena_01.")]
        public string arenaTemplate;

        [Tooltip("Fixed party. Only used when 'Use Fixed Party' is on, or as a fallback when no PartyRoster has been set up.")]
        public List<string> partyActorIds = new List<string>();
        public List<string> enemyIds = new List<string>();
    }
}
