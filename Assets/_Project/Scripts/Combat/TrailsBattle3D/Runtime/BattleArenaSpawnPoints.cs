using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Lives on the root of a battle arena scene. Exposes fixed formation markers so
    /// EncounterRunner never hardcodes spawn positions in code.
    /// </summary>
    public class BattleArenaSpawnPoints : MonoBehaviour
    {
        [Tooltip("Up to 3 slots, front-to-back or left-to-right — party formation.")]
        public Transform[] partySpawnPoints;

        [Tooltip("Up to 4 slots — enemy formation.")]
        public Transform[] enemySpawnPoints;
    }
}
