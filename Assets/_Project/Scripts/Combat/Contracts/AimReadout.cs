namespace Game.Combat.Contracts
{
    /// <summary>
    /// What the player's reticle is on right now and how likely the current action is to work on it, so the HUD can show the
    /// hit-chance badge without knowing any combat rules. A null <see cref="target"/> means nothing is aimed at.
    /// </summary>
    public class AimReadout
    {
        /// <summary>The living enemy under the reticle, or null.</summary>
        public CombatantRuntime target;

        /// <summary>0-100: the chance an attack lands (or, for a save-based spell, that the target fails its save).</summary>
        public int hitChancePercent;

        /// <summary>True when the chance is for the plain Basic Attack (no spell selected).</summary>
        public bool isBasicAttack;

        public static readonly AimReadout None = new AimReadout();
    }
}
