using UnityEngine;

namespace Game.Combat.Contracts
{
    /// <summary>
    /// Lets a runner tell the UI which camera renders the fight. Needed when the fight happens on the overworld, where the
    /// camera cannot be found by looking inside a dedicated arena scene. Null means "work it out yourself" (the arena flow).
    /// </summary>
    public static class BattleCameraHint
    {
        public static Camera Override { get; set; }
    }
}
