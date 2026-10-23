using Game.Combat.Contracts;
using UnityEngine;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Sits on the instantiated battleModelPrefab for one combatant, linking the visible
    /// GameObject back to its CombatantRuntime so TargetingInput's raycasts and the
    /// runner's position sync have something to hang off of.
    /// </summary>
    public class CombatantView : MonoBehaviour
    {
        public CombatantRuntime Runtime { get; set; }

        /// <summary>Head-height point the battle camera follows (child of the view, set up when the view is spawned).</summary>
        public Transform CameraTarget { get; set; }
    }
}
