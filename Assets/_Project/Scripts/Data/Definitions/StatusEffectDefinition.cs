using UnityEngine;

namespace Game.Data.Definitions
{
    [CreateAssetMenu(fileName = "NewStatusEffect", menuName = "Database/Status Effect")]
    public class StatusEffectDefinition : GameDefinition
    {
        public Sprite icon;

        public int duration; // turns

        public int tickDamage; // 0 = no DoT

        public int atkModifier;
        public int defModifier;
        public int speedModifier;

        public bool preventsAction; // covers Stun/Knockout without a separate disable system
    }
}
