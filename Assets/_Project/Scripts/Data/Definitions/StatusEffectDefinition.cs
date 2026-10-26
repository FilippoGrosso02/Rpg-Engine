using UnityEngine;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewStatusEffect", menuName = "Database/Status Effect")]
    public class StatusEffectDefinition : GameDefinition
    {
        public Sprite icon;

        /// <summary>Counted in the affected unit's own turns; it expires at the end of its last one.</summary>
        public int duration;

        public int tickDamage; // 0 = no DoT

        /// <summary>Added to ATK (negative = worse).</summary>
        public int attackModifier;

        /// <summary>Added to DEF (negative = worse).</summary>
        public int armorClassModifier;

        /// <summary>Stuck in a web: cannot move (it can still act where it stands).</summary>
        public bool preventsMovement;

        public bool preventsAction; // covers Stun/Knockout without a separate disable system
    }
}
