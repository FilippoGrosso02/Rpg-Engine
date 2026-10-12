using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Marker component on the arena's ground collider so TargetingInput can tell a
    /// "move here" click on the floor apart from a click on a combatant, without relying
    /// on Unity physics layers.
    /// </summary>
    public class BattleArenaFloor : MonoBehaviour
    {
    }
}
