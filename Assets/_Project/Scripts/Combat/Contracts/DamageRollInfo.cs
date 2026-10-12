namespace Game.Combat.Contracts
{
    /// <summary>
    /// The full breakdown behind one damaging hit's number — effective ATK, the raw die
    /// roll(s), effective DEF, whether DEF was ignored, and the final damage — so UI code can
    /// show "ATK + roll - DEF = damage" instead of just reacting to the resulting HP change.
    /// </summary>
    public class DamageRollInfo
    {
        public CombatantRuntime attacker;
        public CombatantRuntime target;
        public int effectiveAtk;
        public int diceRoll;
        public int effectiveDef;
        public bool defIgnored;
        public bool wasCrit;
        public int damage;
    }
}
