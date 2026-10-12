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
        public List<string> partyActorIds = new List<string>();
        public List<string> enemyIds = new List<string>();
    }
}
