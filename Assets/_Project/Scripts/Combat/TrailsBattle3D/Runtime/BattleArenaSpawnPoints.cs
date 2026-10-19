using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Lives on the root of a battle arena scene. Exposes fixed formation markers so
    /// EncounterRunner never hardcodes spawn positions in code.
    /// </summary>
    public class BattleArenaSpawnPoints : MonoBehaviour
    {
        [Tooltip("Up to 5 slots (matches PartyRoster.AbsoluteMaxSize) — party formation: a front row of 3, then two behind.")]
        public Transform[] partySpawnPoints;

        [Tooltip("Up to 4 slots — enemy formation.")]
        public Transform[] enemySpawnPoints;
    }
}
