namespace Game.Combat.Contracts
{
    /// <summary>
    /// The full breakdown behind one attack or spell's result — the d12 attack roll against AC, the damage dice, and any
    /// save — so UI code can show "d12 9 + 10 vs AC 12 = HIT, d8+2 = 7" instead of just reacting to the resulting HP change.
    /// </summary>
    public class DamageRollInfo
    {
        public CombatantRuntime attacker;
        public CombatantRuntime target;

        /// <summary>Name of the spell, or null for a Basic Attack.</summary>
        public string spellName;

        /// <summary>The attack skips the roll and always hits (Magic Missile).</summary>
        public bool autoHit;

        /// <summary>The natural d12 (0 for an auto-hit or a spell that has no attack roll).</summary>
        public int attackRoll;
        public int attackBonus;
        public int targetArmorClass;
        public bool hit;
        public bool naturalOne;
        public bool naturalTwelve;

        /// <summary>Damage dice as written, e.g. "d8+2" or "2d6".</summary>
        public string damageText;
        public int diceTotal;
        public int damageBonus;

        /// <summary>Damage lost to acid on the attacker's weapon (cumulative).</summary>
        public int corrosionPenalty;

        public bool hasSave;
        public int saveRoll;
        public int saveBonus;
        public bool savePassed;
        public bool halved;

        /// <summary>A spell with no damage-halving (Web) that the target saved against: nothing happens, including its status.</summary>
        public bool effectNegated;

        /// <summary>HP actually lost (0 on a miss or a negated spell).</summary>
        public int damage;
    }
}
